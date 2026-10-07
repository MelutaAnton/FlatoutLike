using System;
using FlatOutLike.Core.Events;
using FlatOutLike.Models;
using FlatOutLike.Services.Flow;
using FlatOutLike.Services.Meta;

namespace FlatOutLike.Views.UI.Hub
{
    /// <summary>
    /// Presenter HUD хаба (pure C#, Шаг 11b): показывает баланс монет (начальный — из
    /// IEconomyService, далее по CurrencyChangedEvent) и по PLAY запускает Derby-матч.
    /// </summary>
    public sealed class HubPresenter : IDisposable
    {
        private readonly HubHudView _view;
        private readonly IEventBus _eventBus;
        private readonly IGameFlowService _gameFlow;

        public HubPresenter(HubHudView view, IEconomyService economy, IEventBus eventBus, IGameFlowService gameFlow)
        {
            _view = view;
            _eventBus = eventBus;
            _gameFlow = gameFlow;

            _view.SetCoins(economy.Coins);

            _view.OnPlayClicked += HandlePlayClicked;
            _eventBus.Subscribe<CurrencyChangedEvent>(OnCurrencyChanged);
        }

        public void Dispose()
        {
            _view.OnPlayClicked -= HandlePlayClicked;
            _eventBus.Unsubscribe<CurrencyChangedEvent>(OnCurrencyChanged);
        }

        private void OnCurrencyChanged(CurrencyChangedEvent e) => _view.SetCoins(e.NewValue);

        private void HandlePlayClicked() => _gameFlow.ChangeState(GameState.DerbyMatch);
    }
}
