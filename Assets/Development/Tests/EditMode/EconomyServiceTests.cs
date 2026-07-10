using System.Collections.Generic;
using FlatOutLike.Core.Events;
using FlatOutLike.Data.Configs.Car;
using FlatOutLike.Data.Configs.Upgrades;
using FlatOutLike.Models;
using FlatOutLike.Services.Meta;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FlatOutLike.Tests
{
    /// <summary>
    /// Юнит-тесты <see cref="EconomyService"/>: покупка машин/апгрейдов, доступность по цене,
    /// владение стартовой машиной без покупки, начисление наград.
    /// Использует настоящий <see cref="EventBus"/> (pure C#, тестируем без Unity) и
    /// fake IMetaProgressService/IPlayerProfile — паттерн см. HighJumpScoreServiceTests.
    /// </summary>
    [TestFixture]
    public class EconomyServiceTests
    {
        private EventBus _bus;
        private FakeMetaProgressService _meta;
        private CarCatalogConfig _carCatalog;
        private UpgradeCatalogConfig _upgradeCatalog;
        private EconomyService _service;

        private readonly List<ScriptableObject> _createdSOs = new List<ScriptableObject>();
        private readonly List<CurrencyChangedEvent> _currencyEvents = new List<CurrencyChangedEvent>();
        private readonly List<CarPurchasedEvent> _carPurchasedEvents = new List<CarPurchasedEvent>();
        private readonly List<UpgradePurchasedEvent> _upgradePurchasedEvents = new List<UpgradePurchasedEvent>();

        private const string StarterCarId = "starter"; // цена 0 — владеема без покупки
        private const string SportCarId = "sport";      // цена 500

        [SetUp]
        public void SetUp()
        {
            _bus = new EventBus();
            _meta = new FakeMetaProgressService();

            _carCatalog = MakeCarCatalog(
                (StarterCarId, 0),
                (SportCarId, 500));

            // Armor: 2 уровня (макс. 2). Остальные категории в каталоге отсутствуют —
            // GetMaxLevel для них должен быть 0.
            _upgradeCatalog = MakeUpgradeCatalog(
                (UpgradeCategory.Armor, new[] { (1, 10f, 100), (2, 15f, 200) }));

            _service = new EconomyService(_bus, _meta, _carCatalog, _upgradeCatalog);

            _currencyEvents.Clear();
            _carPurchasedEvents.Clear();
            _upgradePurchasedEvents.Clear();
            _bus.Subscribe<CurrencyChangedEvent>(_currencyEvents.Add);
            _bus.Subscribe<CarPurchasedEvent>(_carPurchasedEvents.Add);
            _bus.Subscribe<UpgradePurchasedEvent>(_upgradePurchasedEvents.Add);
        }

        [TearDown]
        public void TearDown()
        {
            // Отписка не нужна: шина пересоздаётся в каждом SetUp, а Unsubscribe по
            // method group был бы no-op (каждое обращение — новый инстанс делегата).
            foreach (var so in _createdSOs)
                if (so != null) Object.DestroyImmediate(so);
            _createdSOs.Clear();
        }

        // ── Тесты: машины ───────────────────────────────────────────────────────

        [Test]
        public void FreeStarterCar_IsOwned_WithoutPurchase()
        {
            Assert.IsTrue(_service.IsCarOwned(StarterCarId), "Стартовая машина (цена 0) владеема без покупки.");
            Assert.IsFalse(_service.CanBuyCar(StarterCarId), "Уже владеемую машину покупать нельзя.");
            Assert.IsFalse(_service.TryBuyCar(StarterCarId));
            Assert.AreEqual(0, _carPurchasedEvents.Count, "Покупка стартовой машины не должна публиковать событие.");
        }

        [Test]
        public void TryBuyCar_WithNotEnoughCoins_Fails_AndPublishesNoEvents()
        {
            _meta.Profile.Coins = 100; // меньше цены sport (500)

            Assert.IsFalse(_service.CanBuyCar(SportCarId));
            Assert.IsFalse(_service.TryBuyCar(SportCarId));

            Assert.AreEqual(100, _service.Coins, "Монеты не должны списаться при неудачной покупке.");
            Assert.IsFalse(_service.IsCarOwned(SportCarId));
            Assert.AreEqual(0, _carPurchasedEvents.Count);
            Assert.AreEqual(0, _currencyEvents.Count);
        }

        [Test]
        public void TryBuyCar_WithEnoughCoins_Succeeds_SpendsCoins_AndPublishesEvents()
        {
            _meta.Profile.Coins = 1000;

            bool bought = _service.TryBuyCar(SportCarId);

            Assert.IsTrue(bought);
            Assert.AreEqual(500, _service.Coins, "1000 - 500 (цена sport) = 500.");
            Assert.IsTrue(_service.IsCarOwned(SportCarId));

            Assert.AreEqual(1, _carPurchasedEvents.Count);
            Assert.AreEqual(SportCarId, _carPurchasedEvents[0].CarId);

            Assert.AreEqual(1, _currencyEvents.Count);
            Assert.AreEqual(1000, _currencyEvents[0].OldValue);
            Assert.AreEqual(500, _currencyEvents[0].NewValue);
            Assert.AreEqual(-500, _currencyEvents[0].Delta);
        }

        [Test]
        public void TryBuyCar_AlreadyOwned_Fails_OnSecondAttempt()
        {
            _meta.Profile.Coins = 1000;
            Assert.IsTrue(_service.TryBuyCar(SportCarId));

            _currencyEvents.Clear();
            _carPurchasedEvents.Clear();

            Assert.IsFalse(_service.CanBuyCar(SportCarId), "Машина уже куплена — повторная покупка недоступна.");
            Assert.IsFalse(_service.TryBuyCar(SportCarId), "Повторная покупка owned-машины должна быть отклонена.");
            Assert.AreEqual(500, _service.Coins, "Монеты не должны списаться повторно.");
            Assert.AreEqual(0, _carPurchasedEvents.Count);
            Assert.AreEqual(0, _currencyEvents.Count);
        }

        [Test]
        public void TrySelectCar_NotOwned_Fails()
        {
            bool selected = _service.TrySelectCar(SportCarId);

            Assert.IsFalse(selected, "Нельзя выбрать невладеемую машину.");
            Assert.AreEqual(string.Empty, _meta.Profile.SelectedCarId, "SelectedCarId в профиле не должен измениться.");
            Assert.AreEqual(0, _meta.SaveCallCount, "Save не должен вызываться при неудачном выборе.");
        }

        [Test]
        public void TrySelectCar_Owned_Succeeds()
        {
            bool selected = _service.TrySelectCar(StarterCarId); // бесплатная — владеема сразу

            Assert.IsTrue(selected);
            Assert.AreEqual(StarterCarId, _meta.Profile.SelectedCarId);
        }

        [Test]
        public void SelectedCarId_WhenProfileEmpty_ReturnsFirstCatalogEntry()
        {
            Assert.AreEqual(string.Empty, _meta.Profile.SelectedCarId);
            Assert.AreEqual(StarterCarId, _service.SelectedCarId, "Пустой SelectedCarId в профиле → первая ВЛАДЕЕМАЯ entry (тут это же первая по каталогу, т.к. она бесплатная).");
        }

        [Test]
        public void SelectedCarId_WhenProfileEmpty_AndFirstEntryNotOwned_ReturnsFirstOwnedEntry()
        {
            // premium (не куплена) → sport (куплена) → starter (бесплатная). Первая entry
            // каталога (premium) невладеема — дефолт обязан пропустить её и вернуть sport,
            // а не свалиться до starter или упасть на невладеемой premium (gap Шага 8a).
            const string premiumCarId = "premium"; // цена 1000, не куплена
            var catalog = MakeCarCatalog(
                (premiumCarId, 1000),
                (SportCarId, 500),
                (StarterCarId, 0));
            var service = new EconomyService(_bus, _meta, catalog, _upgradeCatalog);

            _meta.Profile.AddOwnedCar(SportCarId);

            Assert.AreEqual(string.Empty, _meta.Profile.SelectedCarId);
            Assert.AreEqual(SportCarId, service.SelectedCarId,
                "Первая невладеемая entry (premium) должна быть пропущена в пользу первой владеемой (sport).");
        }

        [Test]
        public void SelectedCarId_WhenProfileEmpty_AndNoOwnedEntries_ReturnsFirstCatalogEntry()
        {
            // Все машины платные и ничего не куплено — фоллбэк на первую entry каталога, как раньше.
            const string premiumCarId = "premium"; // цена 1000
            var catalog = MakeCarCatalog(
                (premiumCarId, 1000),
                (SportCarId, 500));
            var service = new EconomyService(_bus, _meta, catalog, _upgradeCatalog);

            Assert.AreEqual(premiumCarId, service.SelectedCarId,
                "Владеемых entries нет вообще — дефолт остаётся первой entry каталога.");
        }

        // ── Тесты: апгрейды ─────────────────────────────────────────────────────

        [Test]
        public void TryBuyUpgrade_BeyondMaxLevel_Fails()
        {
            _meta.Profile.Coins = 10000;
            // Уровень пишем под ТЕКУЩУЮ выбранную машину (по умолчанию — первая владеемая, starter),
            // т.к. GetUpgradeLevel/TryBuyUpgrade теперь per-car (§8/§14.3 game-design.md).
            _meta.Profile.SetUpgradeLevel(_service.SelectedCarId, UpgradeCategory.Armor, 2); // уже максимум (2 уровня в каталоге)

            Assert.IsFalse(_service.CanBuyUpgrade(UpgradeCategory.Armor));
            Assert.IsFalse(_service.TryBuyUpgrade(UpgradeCategory.Armor));
            Assert.AreEqual(10000, _service.Coins, "Монеты не должны списаться сверх максимального уровня.");
            Assert.AreEqual(0, _upgradePurchasedEvents.Count);
        }

        [Test]
        public void TryBuyUpgrade_UnknownCategory_Fails_MaxLevelIsZero()
        {
            // Power отсутствует в каталоге (см. SetUp) — GetMaxLevel == 0, покупка недоступна.
            Assert.IsFalse(_service.CanBuyUpgrade(UpgradeCategory.Power));
            Assert.IsFalse(_service.TryBuyUpgrade(UpgradeCategory.Power));
        }

        [Test]
        public void TryBuyUpgrade_NextLevel_Succeeds_SpendsCoins_AndPublishesEvents()
        {
            _meta.Profile.Coins = 1000;

            bool bought = _service.TryBuyUpgrade(UpgradeCategory.Armor);

            Assert.IsTrue(bought);
            Assert.AreEqual(1, _service.GetUpgradeLevel(UpgradeCategory.Armor));
            Assert.AreEqual(900, _service.Coins, "1000 - 100 (цена уровня 1) = 900.");

            Assert.AreEqual(1, _upgradePurchasedEvents.Count);
            Assert.AreEqual(UpgradeCategory.Armor, _upgradePurchasedEvents[0].Category);
            Assert.AreEqual(1, _upgradePurchasedEvents[0].NewLevel);

            Assert.AreEqual(1, _currencyEvents.Count);
            Assert.AreEqual(-100, _currencyEvents[0].Delta);
        }

        [Test]
        public void GetUpgradeLevel_IsPerCar_LevelOnOneCarNotVisibleOnAnother()
        {
            // Уровень Nitro, купленный под машиной A, не должен быть виден при выборе машины B
            // (per-car прогресс, §8/§14.3 game-design.md) — ключевая регрессия перехода с глобального.
            _meta.Profile.Coins = 1000;
            Assert.IsTrue(_service.TryBuyCar(SportCarId));
            Assert.IsTrue(_service.TrySelectCar(StarterCarId));

            _meta.Profile.SetUpgradeLevel(StarterCarId, UpgradeCategory.Armor, 1);

            Assert.AreEqual(1, _service.GetUpgradeLevel(UpgradeCategory.Armor), "На машине A (starter) уровень должен быть виден.");

            Assert.IsTrue(_service.TrySelectCar(SportCarId));

            Assert.AreEqual(0, _service.GetUpgradeLevel(UpgradeCategory.Armor), "На машине B (sport) уровень машины A виден быть не должен.");
        }

        [Test]
        public void TryBuyUpgrade_WritesLevel_UnderSelectedCarId()
        {
            _meta.Profile.Coins = 1000;
            Assert.IsTrue(_service.TryBuyCar(SportCarId));
            Assert.IsTrue(_service.TrySelectCar(SportCarId));

            Assert.IsTrue(_service.TryBuyUpgrade(UpgradeCategory.Armor));

            Assert.AreEqual(1, _meta.Profile.GetUpgradeLevel(SportCarId, UpgradeCategory.Armor),
                "Уровень должен быть записан именно под SelectedCarId (sport).");
            Assert.AreEqual(0, _meta.Profile.GetUpgradeLevel(StarterCarId, UpgradeCategory.Armor),
                "Другая машина (starter) уровня получить не должна.");
        }

        [Test]
        public void TryBuyUpgrade_PublishesEvent_WithCurrentCarId()
        {
            _meta.Profile.Coins = 1000;
            Assert.IsTrue(_service.TryBuyCar(SportCarId));
            Assert.IsTrue(_service.TrySelectCar(SportCarId));

            Assert.IsTrue(_service.TryBuyUpgrade(UpgradeCategory.Armor));

            Assert.AreEqual(1, _upgradePurchasedEvents.Count);
            Assert.AreEqual(SportCarId, _upgradePurchasedEvents[0].CarId, "UpgradePurchasedEvent должен нести CarId купленной машины.");
        }

        // ── Тесты: начисление наград ────────────────────────────────────────────

        [Test]
        public void Credit_PublishesCurrencyChangedEvent_WithCorrectDelta()
        {
            _meta.Profile.Coins = 50;

            _service.Credit(30);

            Assert.AreEqual(80, _service.Coins);
            Assert.AreEqual(1, _currencyEvents.Count);
            Assert.AreEqual(50, _currencyEvents[0].OldValue);
            Assert.AreEqual(80, _currencyEvents[0].NewValue);
            Assert.AreEqual(30, _currencyEvents[0].Delta);
        }

        [Test]
        public void Credit_WithNonPositiveAmount_IsIgnored()
        {
            _meta.Profile.Coins = 50;

            _service.Credit(0);
            _service.Credit(-10);

            Assert.AreEqual(50, _service.Coins);
            Assert.AreEqual(0, _currencyEvents.Count);
        }

        // ── Хелперы ─────────────────────────────────────────────────────────────

        private CarCatalogConfig MakeCarCatalog(params (string id, int price)[] cars)
        {
            var cfg = ScriptableObject.CreateInstance<CarCatalogConfig>();
            _createdSOs.Add(cfg);

            var so = new SerializedObject(cfg);
            var entriesProp = so.FindProperty("_entries");
            entriesProp.arraySize = cars.Length;

            for (int i = 0; i < cars.Length; i++)
            {
                var el = entriesProp.GetArrayElementAtIndex(i);
                el.FindPropertyRelative("_id").stringValue = cars[i].id;
                el.FindPropertyRelative("_displayName").stringValue = cars[i].id;
                el.FindPropertyRelative("_price").intValue = cars[i].price;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            return cfg;
        }

        private UpgradeCatalogConfig MakeUpgradeCatalog(
            params (UpgradeCategory category, (int level, float effectValue, int price)[] levels)[] categories)
        {
            var cfg = ScriptableObject.CreateInstance<UpgradeCatalogConfig>();
            _createdSOs.Add(cfg);

            var so = new SerializedObject(cfg);
            var categoriesProp = so.FindProperty("_categories");
            categoriesProp.arraySize = categories.Length;

            for (int i = 0; i < categories.Length; i++)
            {
                var categoryProp = categoriesProp.GetArrayElementAtIndex(i);
                categoryProp.FindPropertyRelative("_category").enumValueIndex = (int)categories[i].category;

                var levelsProp = categoryProp.FindPropertyRelative("_levels");
                var levels = categories[i].levels;
                levelsProp.arraySize = levels.Length;

                for (int j = 0; j < levels.Length; j++)
                {
                    var levelProp = levelsProp.GetArrayElementAtIndex(j);
                    levelProp.FindPropertyRelative("_level").intValue = levels[j].level;
                    levelProp.FindPropertyRelative("_effectValue").floatValue = levels[j].effectValue;
                    levelProp.FindPropertyRelative("_price").intValue = levels[j].price;
                }
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            return cfg;
        }

        // ── Фейки ───────────────────────────────────────────────────────────────

        private sealed class FakePlayerProfile : IPlayerProfile
        {
            public int Coins { get; set; }
            public float BestHighJumpHeight { get; set; }
            public string SelectedCarId { get; set; } = string.Empty;

            private readonly HashSet<string> _ownedCarIds = new HashSet<string>();
            private readonly Dictionary<(string, UpgradeCategory), int> _upgradeLevels = new Dictionary<(string, UpgradeCategory), int>();

            public IReadOnlyCollection<string> OwnedCarIds => _ownedCarIds;

            public bool OwnsCar(string carId) => _ownedCarIds.Contains(carId);
            public void AddOwnedCar(string carId) => _ownedCarIds.Add(carId);

            public int GetUpgradeLevel(string carId, UpgradeCategory category)
            {
                if (string.IsNullOrEmpty(carId))
                    return 0;
                return _upgradeLevels.TryGetValue((carId, category), out var level) ? level : 0;
            }

            public void SetUpgradeLevel(string carId, UpgradeCategory category, int level)
            {
                if (string.IsNullOrEmpty(carId))
                    return;
                _upgradeLevels[(carId, category)] = level;
            }

            public IReadOnlyCollection<(string CarId, UpgradeCategory Category, int Level)> GetAllUpgradeLevels()
            {
                var result = new List<(string CarId, UpgradeCategory Category, int Level)>(_upgradeLevels.Count);
                foreach (var kvp in _upgradeLevels)
                    result.Add((kvp.Key.Item1, kvp.Key.Item2, kvp.Value));
                return result;
            }
        }

        private sealed class FakeMetaProgressService : IMetaProgressService
        {
            public IPlayerProfile Profile { get; } = new FakePlayerProfile();
            public int SaveCallCount { get; private set; }

            public void Save() => SaveCallCount++;
        }
    }
}
