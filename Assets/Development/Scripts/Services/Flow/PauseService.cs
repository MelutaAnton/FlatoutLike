using FlatOutLike.Core.Events;
using FlatOutLike.Core.Input;
using FlatOutLike.Models;
using UnityEngine;

namespace FlatOutLike.Services.Flow
{
    /// <summary>
    /// Пауза (Шаг 10a), pure C#. Push/Pop состояния Paused уже публикуют GamePausedEvent/
    /// GameResumedEvent из GameFlowService — здесь только опрос ввода (Tick) и реакция
    /// на эти события (timeScale).
    ///
    /// Time.timeScale — глобальный static, переживает загрузку сцены. Если предыдущая сцена
    /// выгрузилась из паузы раньше, чем сработал GameResumedEvent (или вовсе без него —
    /// например, дев-переход по F-клавише), новый scope должен сбросить его сам:
    /// конструктор всегда ставит 1f, плюс подписка на GameStateChangedEvent — при переходе
    /// в любое состояние, кроме Paused, timeScale принудительно восстанавливается.
    /// </summary>
    public sealed class PauseService : IPauseService
    {
        private readonly IInputProvider _input;
        private readonly IGameFlowService _flow;
        private readonly IEventBus _eventBus;

        public PauseService(IInputProvider input, IGameFlowService flow, IEventBus eventBus)
        {
            _input = input;
            _flow = flow;
            _eventBus = eventBus;

            // Страховка на старте нового per-scene scope — см. class-comment.
            Time.timeScale = 1f;

            _eventBus.Subscribe<GamePausedEvent>(OnPaused);
            _eventBus.Subscribe<GameResumedEvent>(OnResumed);
            _eventBus.Subscribe<GameStateChangedEvent>(OnStateChanged);
        }

        public void Dispose()
        {
            _eventBus.Unsubscribe<GamePausedEvent>(OnPaused);
            _eventBus.Unsubscribe<GameResumedEvent>(OnResumed);
            _eventBus.Unsubscribe<GameStateChangedEvent>(OnStateChanged);
        }

        public void Tick()
        {
            if (!_input.PausePressed)
                return;

            if (_flow.Current == GameState.Paused)
            {
                _flow.Pop();
                return;
            }

            if (IsPausable(_flow.Current))
                _flow.Push(GameState.Paused);
        }

        /// <summary>
        /// Состояния, из которых разрешена пауза — активный геймплей и BaseHub (персонаж бегает
        /// по базе; оверлей там без кнопки Leave — уходить некуда, см. PauseOverlayPresenter).
        /// </summary>
        private static bool IsPausable(GameState state) =>
            state == GameState.BaseHub ||
            state == GameState.DerbyMatch ||
            state == GameState.StuntsHighJumpDriving ||
            state == GameState.StuntsHighJumpFlight;

        private void OnPaused(GamePausedEvent e) => Time.timeScale = 0f;

        private void OnResumed(GameResumedEvent e) => Time.timeScale = 1f;

        private void OnStateChanged(GameStateChangedEvent e)
        {
            if (e.To != GameState.Paused)
                Time.timeScale = 1f;
        }
    }
}
