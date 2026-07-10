using System.Collections.Generic;
using FlatOutLike.Data.Configs.Car;
using FlatOutLike.Data.Configs.Upgrades;
using FlatOutLike.Models;
using FlatOutLike.Services.Car;
using FlatOutLike.Services.Meta;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FlatOutLike.Tests
{
    /// <summary>
    /// Юнит-тесты <see cref="CarStatsService"/>: суммирование effectValue купленных уровней,
    /// перевод в множитель (1 + sum/100) и клонирование через <see cref="CarStatsConfig.CreateModifiedClone"/>.
    /// Fake IMetaProgressService/IPlayerProfile — простые нест-классы (без PlayerPrefs),
    /// паттерн см. HighJumpScoreServiceTests.
    /// </summary>
    [TestFixture]
    public class CarStatsServiceTests
    {
        private FakeMetaProgressService _meta;
        private UpgradeCatalogConfig _upgradeCatalog;
        private CarStatsConfig _baseStats;
        private CarStatsService _service;

        private readonly List<ScriptableObject> _createdSOs = new List<ScriptableObject>();

        [SetUp]
        public void SetUp()
        {
            _meta = new FakeMetaProgressService();

            // Armor: 1 уровень (+10%). Power: 2 уровня (+5% каждый). Nitro: 1 уровень (+8%).
            _upgradeCatalog = MakeUpgradeCatalog(
                (UpgradeCategory.Armor, new[] { (1, 10f, 100) }),
                (UpgradeCategory.Power, new[] { (1, 5f, 150), (2, 5f, 150) }),
                (UpgradeCategory.Nitro, new[] { (1, 8f, 120) }));

            // Дефолтные значения CarStatsConfig (см. поля класса): Acceleration=3500, TopSpeed=30,
            // MaxArmor=100, NitroCapacity=100, NitroForce=2000 — используем как есть, без SerializedObject.
            _baseStats = ScriptableObject.CreateInstance<CarStatsConfig>();
            _createdSOs.Add(_baseStats);

            _service = new CarStatsService(_meta, _upgradeCatalog);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var so in _createdSOs)
                if (so != null) Object.DestroyImmediate(so);
            _createdSOs.Clear();
        }

        // ── Тесты ───────────────────────────────────────────────────────────────

        private const string CarAId = "car-a";
        private const string CarBId = "car-b";

        [Test]
        public void GetEffectiveStats_WithZeroLevels_ReturnsSameReference()
        {
            var result = _service.GetEffectiveStats(CarAId, _baseStats);

            Assert.AreSame(_baseStats, result,
                "Без купленных уровней (множители 1.0) должен вернуться исходный конфиг, без клона.");
        }

        [Test]
        public void GetEffectiveStats_WithLevels_AppliesMultipliers_AndDoesNotMutateSource()
        {
            _meta.Profile.SetUpgradeLevel(CarAId, UpgradeCategory.Armor, 1);  // +10%
            _meta.Profile.SetUpgradeLevel(CarAId, UpgradeCategory.Power, 2);  // +5% + +5% = +10%
            _meta.Profile.SetUpgradeLevel(CarAId, UpgradeCategory.Nitro, 1);  // +8%

            var result = _service.GetEffectiveStats(CarAId, _baseStats);
            _createdSOs.Add(result); // клон — тоже ScriptableObject, подчищаем в TearDown

            Assert.AreNotSame(_baseStats, result, "С купленными уровнями должен вернуться клон.");

            Assert.AreEqual(110f, result.MaxArmor, 0.001f, "MaxArmor: 100 * 1.10.");
            Assert.AreEqual(3850f, result.Acceleration, 0.001f, "Acceleration: 3500 * 1.10.");
            Assert.AreEqual(33f, result.TopSpeed, 0.001f, "TopSpeed: 30 * 1.10.");
            Assert.AreEqual(108f, result.NitroCapacity, 0.001f, "NitroCapacity: 100 * 1.08.");
            Assert.AreEqual(2160f, result.NitroForce, 0.001f, "NitroForce: 2000 * 1.08.");

            // Исходный конфиг не тронут клонированием.
            Assert.AreEqual(100f, _baseStats.MaxArmor, 0.001f);
            Assert.AreEqual(3500f, _baseStats.Acceleration, 0.001f);
            Assert.AreEqual(30f, _baseStats.TopSpeed, 0.001f);
            Assert.AreEqual(100f, _baseStats.NitroCapacity, 0.001f);
            Assert.AreEqual(2000f, _baseStats.NitroForce, 0.001f);
        }

        [Test]
        public void GetEffectiveStats_WithNullBaseStats_ReturnsNull()
        {
            Assert.IsNull(_service.GetEffectiveStats(CarAId, null));
        }

        [Test]
        public void GetEffectiveStats_WithEmptyCarId_ReturnsSameReference()
        {
            // Пустой carId — статы применить некому (нет выбранной машины), фоллбэк на baseStats.
            var result = _service.GetEffectiveStats(string.Empty, _baseStats);

            Assert.AreSame(_baseStats, result);
        }

        [Test]
        public void GetEffectiveStats_IsPerCar_LevelsOnOneCarDoNotAffectAnother()
        {
            _meta.Profile.SetUpgradeLevel(CarAId, UpgradeCategory.Armor, 1); // +10%

            var resultB = _service.GetEffectiveStats(CarBId, _baseStats);

            Assert.AreSame(_baseStats, resultB,
                "Уровни машины A не должны влиять на статы машины B — множители остаются 1.0.");
        }

        // ── Хелперы ─────────────────────────────────────────────────────────────

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
