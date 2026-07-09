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
        private const string UpgradeArmorKey = "FlatOutLike.Profile.Upgrade.Armor";
        private const string UpgradePowerKey = "FlatOutLike.Profile.Upgrade.Power";
        private const string UpgradeNitroKey = "FlatOutLike.Profile.Upgrade.Nitro";

        private const char OwnedCarIdsSeparator = ';';

        public void Save(IPlayerProfile profile)
        {
            PlayerPrefs.SetInt(CoinsKey, profile.Coins);
            PlayerPrefs.SetFloat(BestHighJumpHeightKey, profile.BestHighJumpHeight);
            PlayerPrefs.SetString(SelectedCarIdKey, profile.SelectedCarId ?? string.Empty);
            PlayerPrefs.SetString(OwnedCarIdsKey, string.Join(OwnedCarIdsSeparator.ToString(), profile.OwnedCarIds));
            PlayerPrefs.SetInt(UpgradeArmorKey, profile.GetUpgradeLevel(UpgradeCategory.Armor));
            PlayerPrefs.SetInt(UpgradePowerKey, profile.GetUpgradeLevel(UpgradeCategory.Power));
            PlayerPrefs.SetInt(UpgradeNitroKey, profile.GetUpgradeLevel(UpgradeCategory.Nitro));
            PlayerPrefs.Save();
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

            // Старые сохранения не содержат ключи апгрейдов — дефолт 0 (без прокачки).
            loaded.SetUpgradeLevel(UpgradeCategory.Armor, PlayerPrefs.GetInt(UpgradeArmorKey, 0));
            loaded.SetUpgradeLevel(UpgradeCategory.Power, PlayerPrefs.GetInt(UpgradePowerKey, 0));
            loaded.SetUpgradeLevel(UpgradeCategory.Nitro, PlayerPrefs.GetInt(UpgradeNitroKey, 0));

            profile = loaded;
            return true;
        }
    }
}
