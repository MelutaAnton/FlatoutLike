using System;
using FlatOutLike.Core.Events;
using FlatOutLike.Services.Meta;

namespace FlatOutLike.Services.Match
{
    /// <summary>
    /// Зачисляет монеты из <see cref="RewardEarnedEvent"/> в профиль игрока и сохраняет прогресс.
    /// XP пока не обрабатывается — система прокачки появится позже (§15.3).
    /// </summary>
    public sealed class RewardCrediterService : IDisposable
    {
        private readonly IEventBus          _eventBus;
        private readonly IMetaProgressService _meta;

        public RewardCrediterService(IEventBus eventBus, IMetaProgressService meta)
        {
            _eventBus = eventBus;
            _meta     = meta;

            _eventBus.Subscribe<RewardEarnedEvent>(OnRewardEarned);
        }

        private void OnRewardEarned(RewardEarnedEvent e)
        {
            _meta.Profile.Coins += e.CoinAmount;
            _meta.Save();
        }

        public void Dispose()
        {
            _eventBus.Unsubscribe<RewardEarnedEvent>(OnRewardEarned);
        }
    }
}
