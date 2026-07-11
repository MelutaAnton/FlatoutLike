using System;
using FlatOutLike.Core.Events;
using FlatOutLike.Models;
using FlatOutLike.Services.Flow;

namespace FlatOutLike.Views.UI.Overlay
{
    /// <summary>
    /// Presenter оверлея паузы (Шаг 10a, pure C#, образец MatchEndPresenter). Живёт в геймплейных
    /// сценах — зависит только от IEventBus/IGameFlowService (регистрируются безусловно).
    /// </summary>
    public sealed class PauseOverlayPresenter : IDisposable
    {
        private readonly PauseOverlayView _view;
        private readonly IGameFlowService _gameFlow;
        private readonly IEventBus _eventBus;

        public PauseOverlayPresenter(PauseOverlayView view, IGameFlowService gameFlow, IEventBus eventBus)
        {
            _view = view;
            _gameFlow = gameFlow;
            _eventBus = eventBus;

            _view.OnResumeClicked += HandleResumeClicked;
            _view.OnLeaveClicked += HandleLeaveClicked;
            _eventBus.Subscribe<GamePausedEvent>(OnPaused);
            _eventBus.Subscribe<GameResumedEvent>(OnResumed);
        }

        public void Dispose()
        {
            _view.OnResumeClicked -= HandleResumeClicked;
            _view.OnLeaveClicked -= HandleLeaveClicked;
            _eventBus.Unsubscribe<GamePausedEvent>(OnPaused);
            _eventBus.Unsubscribe<GameResumedEvent>(OnResumed);
        }

        private void OnPaused(GamePausedEvent e) => _view.Show();

        private void OnResumed(GameResumedEvent e) => _view.Hide();

        private void HandleResumeClicked() => _gameFlow.Pop();

        private void HandleLeaveClicked()
        {
            // Pop ОБЯЗАТЕЛЕН первым: публикует GameResumedEvent и восстанавливает timeScale
            // до того, как ChangeState выгрузит сцену — иначе следующая сцена может
            // на мгновение открыться с timeScale=0.
            _gameFlow.Pop();
            _gameFlow.ChangeState(GameState.MainMenu);
        }
    }
}
