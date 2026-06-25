using FlatOutLike.Models;

namespace FlatOutLike.Core.Events
{
    /// <summary>
    /// Unified reward event published by every reward source (Derby, HighJump, missions).
    /// XpAmount may be 0 until the Player Level / XP system exists (§15.3 safeguard) —
    /// future progression services subscribe here without touching reward publishers.
    /// </summary>
    public readonly struct RewardEarnedEvent
    {
        public readonly RewardSource Source;
        public readonly int CoinAmount;
        public readonly int XpAmount;

        public RewardEarnedEvent(RewardSource source, int coinAmount, int xpAmount)
        {
            Source = source;
            CoinAmount = coinAmount;
            XpAmount = xpAmount;
        }
    }
}
