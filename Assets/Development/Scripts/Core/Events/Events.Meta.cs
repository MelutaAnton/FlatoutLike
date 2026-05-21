using FlatOutLike.Models;

namespace FlatOutLike.Core.Events
{
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
