using System;
using FlatOutLike.Core.Events;
using FlatOutLike.Services.Meta;

namespace FlatOutLike.Services.Match
{
    /// <summary>
    /// Зачисляет монеты из <see cref="RewardEarnedEvent"/> в профиль игрока через
    /// <see cref="IEconomyService"/> (единственная точка мутации Coins — иначе HUD/Garage
    /// не увидят <see cref="CurrencyChangedEvent"/> от наград).
    /// XP пока не обрабатывается — система прокачки появится позже (§15.3).
    /// </summary>
    public sealed class RewardCrediterService : IDisposable
    {
        private readonly IEventBus _eventBus;
        private readonly IEconomyService _economy;

        public RewardCrediterService(IEventBus eventBus, IEconomyService economy)
        {
            _eventBus = eventBus;
            _economy  = economy;

            _eventBus.Subscribe<RewardEarnedEvent>(OnRewardEarned);
        }

        private void OnRewardEarned(RewardEarnedEvent e)
        {
            _economy.Credit(e.CoinAmount);
        }

        public void Dispose()
        {
            _eventBus.Unsubscribe<RewardEarnedEvent>(OnRewardEarned);
        }
    }
}
