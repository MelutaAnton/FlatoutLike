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
            Assert.AreEqual(StarterCarId, _service.SelectedCarId, "Пустой SelectedCarId в профиле → первая entry каталога.");
        }

        // ── Тесты: апгрейды ─────────────────────────────────────────────────────

        [Test]
        public void TryBuyUpgrade_BeyondMaxLevel_Fails()
        {
            _meta.Profile.Coins = 10000;
            _meta.Profile.SetUpgradeLevel(UpgradeCategory.Armor, 2); // уже максимум (2 уровня в каталоге)

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
            private readonly Dictionary<UpgradeCategory, int> _upgradeLevels = new Dictionary<UpgradeCategory, int>();

            public IReadOnlyCollection<string> OwnedCarIds => _ownedCarIds;

            public bool OwnsCar(string carId) => _ownedCarIds.Contains(carId);
            public void AddOwnedCar(string carId) => _ownedCarIds.Add(carId);

            public int GetUpgradeLevel(UpgradeCategory category) =>
                _upgradeLevels.TryGetValue(category, out var level) ? level : 0;

            public void SetUpgradeLevel(UpgradeCategory category, int level) => _upgradeLevels[category] = level;
        }

        private sealed class FakeMetaProgressService : IMetaProgressService
        {
            public IPlayerProfile Profile { get; } = new FakePlayerProfile();
            public int SaveCallCount { get; private set; }

            public void Save() => SaveCallCount++;
        }
    }
}
