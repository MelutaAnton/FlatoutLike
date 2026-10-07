using System;
using FlatOutLike.Core.Events;
using FlatOutLike.Models;
using FlatOutLike.Services.Flow;

namespace FlatOutLike.Views.UI.Overlay
{
    /// <summary>
    /// Presenter оверлея паузы (Шаг 10a, pure C#, образец MatchEndPresenter). Живёт в геймплейных
    /// сценах и в BaseHub — зависит только от IEventBus/IGameFlowService (регистрируются безусловно).
    /// </summary>
    public sealed class PauseOverlayPresenter : IDisposable
    {
        private readonly PauseOverlayView _view;
        private readonly IGameFlowService _gameFlow;
        private readonly IEventBus _eventBus;

        // Ушли ли в паузу из BaseHub. GameFlowService.Push публикует сначала
        // GameStateChangedEvent(from, Paused), потом GamePausedEvent — к моменту OnPaused
        // флаг уже выставлен. Булев флаг, а не GameState: дефолт GameState = BaseHub (0)
        // молча прятал бы Leave, если бы GamePausedEvent пришёл без предшествующего перехода.
        private bool _pausedFromHub;

        public PauseOverlayPresenter(PauseOverlayView view, IGameFlowService gameFlow, IEventBus eventBus)
        {
            _view = view;
            _gameFlow = gameFlow;
            _eventBus = eventBus;

            _view.OnResumeClicked += HandleResumeClicked;
            _view.OnLeaveClicked += HandleLeaveClicked;
            _eventBus.Subscribe<GameStateChangedEvent>(OnGameStateChanged);
            _eventBus.Subscribe<GamePausedEvent>(OnPaused);
            _eventBus.Subscribe<GameResumedEvent>(OnResumed);
        }

        public void Dispose()
        {
            _view.OnResumeClicked -= HandleResumeClicked;
            _view.OnLeaveClicked -= HandleLeaveClicked;
            _eventBus.Unsubscribe<GameStateChangedEvent>(OnGameStateChanged);
            _eventBus.Unsubscribe<GamePausedEvent>(OnPaused);
            _eventBus.Unsubscribe<GameResumedEvent>(OnResumed);
        }

        private void OnGameStateChanged(GameStateChangedEvent e)
        {
            if (e.To == GameState.Paused)
                _pausedFromHub = e.From == GameState.BaseHub;
        }

        private void OnPaused(GamePausedEvent e)
        {
            // Из BaseHub уходить некуда (Leave вёл бы в ту же сцену) — оставляем только Resume.
            _view.SetLeaveVisible(!_pausedFromHub);
            _view.Show();
        }

        private void OnResumed(GameResumedEvent e) => _view.Hide();

        private void HandleResumeClicked() => _gameFlow.Pop();

        private void HandleLeaveClicked()
        {
            // Pop ОБЯЗАТЕЛЕН первым: публикует GameResumedEvent и восстанавливает timeScale
            // до того, как ChangeState выгрузит сцену — иначе следующая сцена может
            // на мгновение открыться с timeScale=0.
            _gameFlow.Pop();
            _gameFlow.ChangeState(GameState.BaseHub);
        }
    }
}
