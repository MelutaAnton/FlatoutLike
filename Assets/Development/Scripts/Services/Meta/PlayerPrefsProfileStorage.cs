using UnityEngine;

namespace FlatOutLike.Services.Meta
{
    public sealed class PlayerPrefsProfileStorage : IProfileStorage
    {
        // Single source of truth — never duplicated elsewhere
        private const string CoinsKey = "FlatOutLike.Profile.Coins";
        private const string BestHighJumpHeightKey = "FlatOutLike.Profile.BestHighJumpHeight";

        public void Save(IPlayerProfile profile)
        {
            PlayerPrefs.SetInt(CoinsKey, profile.Coins);
            PlayerPrefs.SetFloat(BestHighJumpHeightKey, profile.BestHighJumpHeight);
            PlayerPrefs.Save();
        }

        public bool TryLoad(out IPlayerProfile profile)
        {
            if (!PlayerPrefs.HasKey(CoinsKey))
            {
                profile = null;
                return false;
            }

            profile = new PlayerProfile
            {
                Coins = PlayerPrefs.GetInt(CoinsKey),
                BestHighJumpHeight = PlayerPrefs.GetFloat(BestHighJumpHeightKey, 0f)
            };
            return true;
        }
    }
}
