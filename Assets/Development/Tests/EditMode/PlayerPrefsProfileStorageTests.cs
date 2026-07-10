using FlatOutLike.Models;
using FlatOutLike.Services.Meta;
using NUnit.Framework;
using UnityEngine;

namespace FlatOutLike.Tests
{
    /// <summary>
    /// Юнит-тесты <see cref="PlayerPrefsProfileStorage"/>: roundtrip per-car уровней апгрейдов
    /// (§8/§14.3 game-design.md) и миграция legacy-сейва (глобальные уровни до перехода на
    /// per-car) на <see cref="IPlayerProfile.SelectedCarId"/>.
    /// PlayerPrefs реально доступен в EditMode-тестах и ДЕЛИТ хранилище с живым профилем
    /// разработчика (те же ключи!). Поэтому SetUp снимает бэкап реальных значений,
    /// TearDown удаляет тестовые ключи и восстанавливает бэкап — прогон тестов не должен
    /// стирать монеты/машины/апгрейды, накопленные в плейтестах.
    /// </summary>
    [TestFixture]
    public class PlayerPrefsProfileStorageTests
    {
        // Дублируют private-константы PlayerPrefsProfileStorage осознанно: тест проверяет
        // ЧЁРНЫЙ ЯЩИК хранения (реальные ключи PlayerPrefs), а не внутреннюю структуру класса.
        private const string CoinsKey = "FlatOutLike.Profile.Coins";
        private const string BestHighJumpHeightKey = "FlatOutLike.Profile.BestHighJumpHeight";
        private const string SelectedCarIdKey = "FlatOutLike.Profile.SelectedCarId";
        private const string OwnedCarIdsKey = "FlatOutLike.Profile.OwnedCarIds";
        private const string UpgradeIndexKey = "FlatOutLike.Profile.Upgrade.Index";
        private const string LegacyUpgradeArmorKey = "FlatOutLike.Profile.Upgrade.Armor";
        private const string LegacyUpgradePowerKey = "FlatOutLike.Profile.Upgrade.Power";
        private const string LegacyUpgradeNitroKey = "FlatOutLike.Profile.Upgrade.Nitro";

        private const string CarAId = "car-a";
        private const string CarBId = "car-b";

        private PlayerPrefsProfileStorage _storage;

        // Бэкап живого dev-профиля: ключ → (тип, значение). Отсутствующий ключ — null-значение.
        private readonly System.Collections.Generic.Dictionary<string, object> _realProfileBackup =
            new System.Collections.Generic.Dictionary<string, object>();

        [SetUp]
        public void SetUp()
        {
            _storage = new PlayerPrefsProfileStorage();
            BackupRealProfile();
            DeleteAllTestKeys();
        }

        [TearDown]
        public void TearDown()
        {
            DeleteAllTestKeys();
            RestoreRealProfile();
        }

        // ── Бэкап/восстановление реального профиля ──────────────────────────────

        private void BackupRealProfile()
        {
            _realProfileBackup.Clear();

            BackupInt(CoinsKey);
            BackupFloat(BestHighJumpHeightKey);
            BackupString(SelectedCarIdKey);
            BackupString(OwnedCarIdsKey);
            BackupString(UpgradeIndexKey);
            BackupInt(LegacyUpgradeArmorKey);
            BackupInt(LegacyUpgradePowerKey);
            BackupInt(LegacyUpgradeNitroKey);

            // Per-car ключи реальных машин перечисляем по живому индексу.
            string realIndex = PlayerPrefs.GetString(UpgradeIndexKey, string.Empty);
            foreach (var carId in realIndex.Split(new[] { ';' }, System.StringSplitOptions.RemoveEmptyEntries))
            foreach (UpgradeCategory category in System.Enum.GetValues(typeof(UpgradeCategory)))
                BackupInt($"FlatOutLike.Profile.Upgrade.{carId}.{category}");
        }

        private void RestoreRealProfile()
        {
            foreach (var pair in _realProfileBackup)
            {
                if (pair.Value == null)
                    PlayerPrefs.DeleteKey(pair.Key);
                else if (pair.Value is int i)
                    PlayerPrefs.SetInt(pair.Key, i);
                else if (pair.Value is float f)
                    PlayerPrefs.SetFloat(pair.Key, f);
                else
                    PlayerPrefs.SetString(pair.Key, (string)pair.Value);
            }

            PlayerPrefs.Save();
        }

        private void BackupInt(string key) =>
            _realProfileBackup[key] = PlayerPrefs.HasKey(key) ? (object)PlayerPrefs.GetInt(key) : null;

        private void BackupFloat(string key) =>
            _realProfileBackup[key] = PlayerPrefs.HasKey(key) ? (object)PlayerPrefs.GetFloat(key) : null;

        private void BackupString(string key) =>
            _realProfileBackup[key] = PlayerPrefs.HasKey(key) ? (object)PlayerPrefs.GetString(key) : null;

        private static void DeleteAllTestKeys()
        {
            PlayerPrefs.DeleteKey(CoinsKey);
            PlayerPrefs.DeleteKey(BestHighJumpHeightKey);
            PlayerPrefs.DeleteKey(SelectedCarIdKey);
            PlayerPrefs.DeleteKey(OwnedCarIdsKey);
            PlayerPrefs.DeleteKey(UpgradeIndexKey);
            PlayerPrefs.DeleteKey(LegacyUpgradeArmorKey);
            PlayerPrefs.DeleteKey(LegacyUpgradePowerKey);
            PlayerPrefs.DeleteKey(LegacyUpgradeNitroKey);

            foreach (var carId in new[] { CarAId, CarBId })
            foreach (UpgradeCategory category in System.Enum.GetValues(typeof(UpgradeCategory)))
                PlayerPrefs.DeleteKey($"FlatOutLike.Profile.Upgrade.{carId}.{category}");
        }

        [Test]
        public void SaveThenLoad_RoundtripsPerCarUpgradeLevels()
        {
            var profile = new PlayerProfile { Coins = 100, SelectedCarId = CarAId };
            profile.SetUpgradeLevel(CarAId, UpgradeCategory.Armor, 2);
            profile.SetUpgradeLevel(CarAId, UpgradeCategory.Nitro, 1);
            profile.SetUpgradeLevel(CarBId, UpgradeCategory.Power, 3);

            _storage.Save(profile);

            Assert.IsTrue(_storage.TryLoad(out var loaded));
            Assert.AreEqual(2, loaded.GetUpgradeLevel(CarAId, UpgradeCategory.Armor));
            Assert.AreEqual(1, loaded.GetUpgradeLevel(CarAId, UpgradeCategory.Nitro));
            Assert.AreEqual(0, loaded.GetUpgradeLevel(CarAId, UpgradeCategory.Power), "Не сохранённая категория — уровень 0.");
            Assert.AreEqual(3, loaded.GetUpgradeLevel(CarBId, UpgradeCategory.Power));
            Assert.AreEqual(0, loaded.GetUpgradeLevel(CarBId, UpgradeCategory.Armor), "Уровень машины A не должен утечь на машину B.");
        }

        [Test]
        public void Save_DeletesLegacyKeys()
        {
            // Симулируем legacy-сейв напрямую в PlayerPrefs (до перехода на per-car).
            PlayerPrefs.SetInt(CoinsKey, 50);
            PlayerPrefs.SetInt(LegacyUpgradeArmorKey, 1);
            PlayerPrefs.Save();

            var profile = new PlayerProfile { Coins = 50 };
            _storage.Save(profile);

            Assert.IsFalse(PlayerPrefs.HasKey(LegacyUpgradeArmorKey), "Legacy-ключ должен удаляться при первом же Save.");
        }

        [Test]
        public void TryLoad_WithLegacySave_AndNonEmptySelectedCarId_MigratesLevelsToSelectedCar()
        {
            PlayerPrefs.SetInt(CoinsKey, 200);
            PlayerPrefs.SetString(SelectedCarIdKey, CarAId);
            PlayerPrefs.SetInt(LegacyUpgradeArmorKey, 2);
            PlayerPrefs.SetInt(LegacyUpgradePowerKey, 1);
            PlayerPrefs.SetInt(LegacyUpgradeNitroKey, 3);
            PlayerPrefs.Save();

            Assert.IsTrue(_storage.TryLoad(out var loaded));

            Assert.AreEqual(2, loaded.GetUpgradeLevel(CarAId, UpgradeCategory.Armor));
            Assert.AreEqual(1, loaded.GetUpgradeLevel(CarAId, UpgradeCategory.Power));
            Assert.AreEqual(3, loaded.GetUpgradeLevel(CarAId, UpgradeCategory.Nitro));
        }

        [Test]
        public void TryLoad_WithLegacySave_AndEmptySelectedCarId_DiscardsLevels()
        {
            PlayerPrefs.SetInt(CoinsKey, 200);
            // SelectedCarId не задан — дефолт string.Empty, привязать legacy-уровни не к чему.
            PlayerPrefs.SetInt(LegacyUpgradeArmorKey, 2);
            PlayerPrefs.Save();

            Assert.IsTrue(_storage.TryLoad(out var loaded));

            Assert.AreEqual(string.Empty, loaded.SelectedCarId);
            Assert.AreEqual(0, loaded.GetAllUpgradeLevels().Count, "Уровни без привязки к машине должны быть отброшены целиком.");
        }
    }
}
