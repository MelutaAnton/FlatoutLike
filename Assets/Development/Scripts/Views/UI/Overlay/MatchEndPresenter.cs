using System;
using FlatOutLike.Core.Events;
using FlatOutLike.Models;
using FlatOutLike.Services.Flow;
using FlatOutLike.Services.Match;

namespace FlatOutLike.Views.UI.Overlay
{
    /// <summary>
    /// Presenter оверлея результата Derby-матча (pure C#). Живёт в Derby-сцене — зависит
    /// только от IEventBus/ICarRegistry/IGameFlowService (все регистрируются безусловно),
    /// мета-каталоги ему не нужны.
    ///
    /// DerbyMatchService.EndMatch публикует DerbyMatchEndedEvent и сразу вслед за ним, синхронно,
    /// RewardEarnedEvent(Source=Derby) — сумма награды в момент DerbyMatchEndedEvent ещё
    /// неизвестна. Поэтому Show() вызывается из обработчика RewardEarnedEvent, когда данные
    /// матча уже закэшированы, а не из обработчика DerbyMatchEndedEvent напрямую.
    /// </summary>
    public sealed class MatchEndPresenter : IDisposable
    {
        private readonly MatchEndView _view;
        private readonly IEventBus _eventBus;
        private readonly ICarRegistry _carRegistry;
        private readonly IGameFlowService _gameFlow;

        private bool _matchEndPending;
        private CarId _pendingWinner;
        private int _pendingKills;

        public MatchEndPresenter(MatchEndView view, IEventBus eventBus, ICarRegistry carRegistry, IGameFlowService gameFlow)
        {
            _view = view;
            _eventBus = eventBus;
            _carRegistry = carRegistry;
            _gameFlow = gameFlow;

            _view.OnContinueClicked += HandleContinueClicked;
            _eventBus.Subscribe<DerbyMatchEndedEvent>(OnMatchEnded);
            _eventBus.Subscribe<RewardEarnedEvent>(OnRewardEarned);
        }

        public void Dispose()
        {
            _view.OnContinueClicked -= HandleContinueClicked;
            _eventBus.Unsubscribe<DerbyMatchEndedEvent>(OnMatchEnded);
            _eventBus.Unsubscribe<RewardEarnedEvent>(OnRewardEarned);
        }

        private void OnMatchEnded(DerbyMatchEndedEvent e)
        {
            _pendingWinner = e.Winner;
            _pendingKills = e.PlayerKills;
            _matchEndPending = true;
        }

        private void OnRewardEarned(RewardEarnedEvent e)
        {
            // Награды могут прийти и не из Derby (например, из будущих миссий) — реагируем
            // только на ту, что относится к матчу, чьё завершение уже закэшировано.
            if (e.Source != RewardSource.Derby || !_matchEndPending)
                return;

            _matchEndPending = false;

            // Через TryGetPlayerCarId — presenter (pure C#) не касается MonoBehaviour-контроллера.
            bool isVictory = _carRegistry.TryGetPlayerCarId(out var playerCarId) &&
                             playerCarId == _pendingWinner;

            _view.Show(isVictory, _pendingKills, e.CoinAmount);
        }

        private void HandleContinueClicked()
        {
            _view.Hide();
            _gameFlow.ChangeState(GameState.GarageMeta);
        }
    }
}
