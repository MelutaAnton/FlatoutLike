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
    /// GaragePresenter требует живой GarageView — MonoBehaviour с SerializeField-ссылками на
    /// Button/TMP_Text, чей Awake обращается к этим полям (регистрирует Button.onClick). Собрать
    /// корректный экземпляр в EditMode-тесте без реальной сцены/префаба нельзя, а заводить
    /// IView-интерфейс исключительно ради тестируемости запрещено заданием Шага 8c — GarageView
    /// остаётся простым MVP-View без абстракций для тестов.
    ///
    /// Поэтому здесь напрямую воспроизводится РЕШЕНИЕ, которое принимает
    /// GaragePresenter.HandleCarCardClicked по клику карточки:
    ///   не-owned → TryBuyCar, при успехе сразу TrySelectCar;
    ///   owned → сразу TrySelectCar.
    /// Тестируется через настоящий EconomyService (тот же паттерн, что и EconomyServiceTests) —
    /// GaragePresenter лишь вызывает эти же методы IEconomyService в этом же порядке, отдельного
    /// ветвления в нём больше нет.
    /// </summary>
    [TestFixture]
    public class GaragePresenterTests
    {
        private EventBus _bus;
        private FakeMetaProgressService _meta;
        private CarCatalogConfig _carCatalog;
        private UpgradeCatalogConfig _upgradeCatalog;
        private EconomyService _economy;

        private readonly List<ScriptableObject> _createdSOs = new List<ScriptableObject>();

        private const string StarterCarId = "starter"; // цена 0 — владеема без покупки
        private const string SportCarId = "sport";      // цена 500

        [SetUp]
        public void SetUp()
        {
            _bus = new EventBus();
            _meta = new FakeMetaProgressService();
            _carCatalog = MakeCarCatalog((StarterCarId, 0), (SportCarId, 500));

            _upgradeCatalog = ScriptableObject.CreateInstance<UpgradeCatalogConfig>();
            _createdSOs.Add(_upgradeCatalog);

            _economy = new EconomyService(_bus, _meta, _carCatalog, _upgradeCatalog);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var so in _createdSOs)
                if (so != null) Object.DestroyImmediate(so);
            _createdSOs.Clear();
        }

        /// <summary>Воспроизводит GaragePresenter.HandleCarCardClicked ровно в той же последовательности.</summary>
        private void SimulateCardClick(string carId)
        {
            if (!_economy.IsCarOwned(carId))
            {
                if (_economy.TryBuyCar(carId))
                    _economy.TrySelectCar(carId);
            }
            else
            {
                _economy.TrySelectCar(carId);
            }
        }

        [Test]
        public void ClickNotOwned_WithNotEnoughCoins_DoesNotBuy_AndDoesNotSelect()
        {
            _meta.Profile.Coins = 100; // меньше цены sport (500)

            SimulateCardClick(SportCarId);

            Assert.IsFalse(_economy.IsCarOwned(SportCarId), "Нехватка монет — покупки быть не должно.");
            Assert.AreNotEqual(SportCarId, _meta.Profile.SelectedCarId, "Не купленная машина не должна выбираться.");
            Assert.AreEqual(100, _economy.Coins, "Монеты не должны списаться при неудачной покупке.");
        }

        [Test]
        public void ClickOwned_SelectsCar_WithoutSpendingCoins()
        {
            _meta.Profile.Coins = 1000;

            SimulateCardClick(StarterCarId); // бесплатная — владеема сразу

            Assert.AreEqual(StarterCarId, _meta.Profile.SelectedCarId);
            Assert.AreEqual(1000, _economy.Coins, "Выбор уже владеемой машины не тратит монеты.");
        }

        [Test]
        public void ClickNotOwned_WithEnoughCoins_BuysAndImmediatelySelects()
        {
            _meta.Profile.Coins = 1000;

            SimulateCardClick(SportCarId);

            Assert.IsTrue(_economy.IsCarOwned(SportCarId));
            Assert.AreEqual(SportCarId, _meta.Profile.SelectedCarId, "Успешная покупка сразу выбирает машину — второй клик не нужен.");
            Assert.AreEqual(500, _economy.Coins);
        }

        // ── Хелперы (паттерн EconomyServiceTests) ──────────────────────────────

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
