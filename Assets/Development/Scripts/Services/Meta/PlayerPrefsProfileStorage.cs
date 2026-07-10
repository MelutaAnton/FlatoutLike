using System;
using FlatOutLike.Models;
using UnityEngine;

namespace FlatOutLike.Services.Meta
{
    public sealed class PlayerPrefsProfileStorage : IProfileStorage
    {
        // Single source of truth — never duplicated elsewhere
        private const string CoinsKey = "FlatOutLike.Profile.Coins";
        private const string BestHighJumpHeightKey = "FlatOutLike.Profile.BestHighJumpHeight";
        private const string SelectedCarIdKey = "FlatOutLike.Profile.SelectedCarId";
        private const string OwnedCarIdsKey = "FlatOutLike.Profile.OwnedCarIds";

        // Per-car ключи апгрейдов (§8/§14.3 game-design.md): "FlatOutLike.Profile.Upgrade.<carId>.<Category>".
        // PlayerPrefs не итерируем по ключам — список carId'ов хранится отдельно, в индексе.
        private const string UpgradeKeyPrefix = "FlatOutLike.Profile.Upgrade.";
        private const string UpgradeIndexKey = "FlatOutLike.Profile.Upgrade.Index";

        // Legacy-ключи (глобальные уровни апгрейдов до перехода на per-car). Больше не пишутся —
        // используются только для одноразовой миграции старого сейва в TryLoad.
        private const string LegacyUpgradeArmorKey = "FlatOutLike.Profile.Upgrade.Armor";
        private const string LegacyUpgradePowerKey = "FlatOutLike.Profile.Upgrade.Power";
        private const string LegacyUpgradeNitroKey = "FlatOutLike.Profile.Upgrade.Nitro";

        // ИНВАРИАНТ: carId каталога — kebab-case и НЕ содержит ';' (иначе оба индекса
        // ниже разъедутся на загрузке). Ids задаются editor-скриптом каталога.
        private const char OwnedCarIdsSeparator = ';';
        private const char UpgradeIndexSeparator = ';';

        // Единственный источник перечня категорий — не дублировать список в другом месте файла.
        private static readonly UpgradeCategory[] AllCategories =
            (UpgradeCategory[])Enum.GetValues(typeof(UpgradeCategory));

        public void Save(IPlayerProfile profile)
        {
            PlayerPrefs.SetInt(CoinsKey, profile.Coins);
            PlayerPrefs.SetFloat(BestHighJumpHeightKey, profile.BestHighJumpHeight);
            PlayerPrefs.SetString(SelectedCarIdKey, profile.SelectedCarId ?? string.Empty);
            PlayerPrefs.SetString(OwnedCarIdsKey, string.Join(OwnedCarIdsSeparator.ToString(), profile.OwnedCarIds));

            SaveUpgradeLevels(profile);

            // Legacy-ключи больше не актуальны — удаляем их при первом же Save после миграции
            // (см. MigrateLegacyUpgradeLevels). Удаление отсутствующего ключа безопасно (no-op).
            PlayerPrefs.DeleteKey(LegacyUpgradeArmorKey);
            PlayerPrefs.DeleteKey(LegacyUpgradePowerKey);
            PlayerPrefs.DeleteKey(LegacyUpgradeNitroKey);

            PlayerPrefs.Save();
        }

        private static void SaveUpgradeLevels(IPlayerProfile profile)
        {
            var levels = profile.GetAllUpgradeLevels();
            var carIds = new System.Collections.Generic.HashSet<string>();

            foreach (var entry in levels)
            {
                if (string.IsNullOrEmpty(entry.CarId))
                    continue;

                carIds.Add(entry.CarId);
                PlayerPrefs.SetInt(MakeUpgradeKey(entry.CarId, entry.Category), entry.Level);
            }

            PlayerPrefs.SetString(UpgradeIndexKey, string.Join(UpgradeIndexSeparator.ToString(), carIds));
        }

        public bool TryLoad(out IPlayerProfile profile)
        {
            if (!PlayerPrefs.HasKey(CoinsKey))
            {
                profile = null;
                return false;
            }

            var loaded = new PlayerProfile
            {
                Coins = PlayerPrefs.GetInt(CoinsKey),
                BestHighJumpHeight = PlayerPrefs.GetFloat(BestHighJumpHeightKey, 0f),
                // Пустая строка по умолчанию — старые сохранения без этого ключа считаются
                // «машина не выбрана», потребители берут первую entry каталога.
                SelectedCarId = PlayerPrefs.GetString(SelectedCarIdKey, string.Empty)
            };

            // Старые сохранения не содержат ключ владения машинами — дефолт "" даёт пустой список.
            var ownedRaw = PlayerPrefs.GetString(OwnedCarIdsKey, string.Empty);
            if (!string.IsNullOrEmpty(ownedRaw))
                loaded.SetOwnedCarIds(ownedRaw.Split(OwnedCarIdsSeparator));

            // Индекс есть — сейв уже per-car, читаем как есть. Индекса нет — либо чистый
            // профиль, либо legacy-сейв с глобальными уровнями (пробуем смигрировать).
            if (PlayerPrefs.HasKey(UpgradeIndexKey))
                LoadUpgradeLevels(loaded);
            else
                MigrateLegacyUpgradeLevels(loaded);

            profile = loaded;
            return true;
        }

        private static void LoadUpgradeLevels(PlayerProfile profile)
        {
            var indexRaw = PlayerPrefs.GetString(UpgradeIndexKey, string.Empty);
            if (string.IsNullOrEmpty(indexRaw))
                return;

            var carIds = indexRaw.Split(UpgradeIndexSeparator);
            foreach (var carId in carIds)
            {
                if (string.IsNullOrEmpty(carId))
                    continue;

                foreach (var category in AllCategories)
                {
                    var key = MakeUpgradeKey(carId, category);
                    if (PlayerPrefs.HasKey(key))
                        profile.SetUpgradeLevel(carId, category, PlayerPrefs.GetInt(key));
                }
            }
        }

        /// <summary>
        /// Миграция legacy-сейва (глобальные уровни апгрейдов до Шага 8/§14.3): переносит их на
        /// <see cref="IPlayerProfile.SelectedCarId"/>, если он непуст — раньше апгрейды были одни
        /// на профиль, и это была именно та машина, для которой игрок их фактически покупал.
        /// Если машина не выбрана — привязать уровни не к чему, они отбрасываются с логом.
        /// Новые per-car ключи здесь не пишутся — появятся при ближайшем <see cref="Save"/>,
        /// тогда же будут удалены и legacy-ключи.
        /// </summary>
        private static void MigrateLegacyUpgradeLevels(PlayerProfile profile)
        {
            bool hasLegacy = PlayerPrefs.HasKey(LegacyUpgradeArmorKey)
                || PlayerPrefs.HasKey(LegacyUpgradePowerKey)
                || PlayerPrefs.HasKey(LegacyUpgradeNitroKey);

            if (!hasLegacy)
                return;

            if (string.IsNullOrEmpty(profile.SelectedCarId))
            {
                Debug.Log("[PlayerPrefsProfileStorage] Найдены legacy-уровни апгрейдов, но SelectedCarId пуст — привязать их не к чему, уровни отброшены.");
                return;
            }

            // Нулевые уровни не переносим — иначе частичный legacy-сейв (например, только Nitro)
            // засоряет словарь и per-car ключи «шумовыми» нулями по остальным категориям.
            MigrateLegacyLevelIfPositive(profile, UpgradeCategory.Armor, LegacyUpgradeArmorKey);
            MigrateLegacyLevelIfPositive(profile, UpgradeCategory.Power, LegacyUpgradePowerKey);
            MigrateLegacyLevelIfPositive(profile, UpgradeCategory.Nitro, LegacyUpgradeNitroKey);

            Debug.Log($"[PlayerPrefsProfileStorage] Legacy-уровни апгрейдов перенесены на машину '{profile.SelectedCarId}'.");
        }

        private static void MigrateLegacyLevelIfPositive(PlayerProfile profile, UpgradeCategory category, string legacyKey)
        {
            int level = PlayerPrefs.GetInt(legacyKey, 0);
            if (level > 0)
                profile.SetUpgradeLevel(profile.SelectedCarId, category, level);
        }

        private static string MakeUpgradeKey(string carId, UpgradeCategory category) =>
            $"{UpgradeKeyPrefix}{carId}.{category}";
    }
}
