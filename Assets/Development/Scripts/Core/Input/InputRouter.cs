using System;
using FlatOutLike.Core.Events;
using FlatOutLike.Models;

namespace FlatOutLike.Core.Input
{
    /// <summary>
    /// Listens to GameStateChangedEvent and switches IInputProvider.CurrentContext accordingly.
    /// Call Tick() once per frame to forward to the provider.
    /// </summary>
    public sealed class InputRouter : IDisposable
    {
        private readonly IInputProvider _provider;
        private readonly IEventBus _eventBus;

        public InputRouter(IInputProvider provider, IEventBus eventBus)
        {
            _provider = provider;
            _eventBus = eventBus;
            _eventBus.Subscribe<GameStateChangedEvent>(OnGameStateChanged);
        }

        /// <summary>
        /// Явная синхронизация контекста с текущим состоянием GameFlow. Нужна при старте
        /// per-scene scope: StateMachine входит в начальное состояние в конструкторе
        /// GameFlowService БЕЗ публикации GameStateChangedEvent (да и роутер на тот момент
        /// ещё не подписан), поэтому свежая сцена иначе остаётся на дефолтном контексте
        /// провайдера, что верно только для Driving-сцен.
        /// </summary>
        public void SyncTo(GameState state)
        {
            _provider.CurrentContext = ResolveContext(state);
        }

        public void Tick()
        {
            _provider.Tick();
        }

        public void Dispose()
        {
            _eventBus.Unsubscribe<GameStateChangedEvent>(OnGameStateChanged);
        }

        private void OnGameStateChanged(GameStateChangedEvent evt)
        {
            _provider.CurrentContext = ResolveContext(evt.To);
        }

        private static InputContext ResolveContext(GameState state)
        {
            switch (state)
            {
                case GameState.DerbyMatch:
                case GameState.StuntsHighJumpDriving:
                    return InputContext.Driving;

                case GameState.StuntsHighJumpFlight:
                    return InputContext.Flight;

                case GameState.MainMenu:
                case GameState.GarageMeta:
                case GameState.StuntsHub:
                case GameState.StuntsHighJumpScoring:
                case GameState.MatchEnd:
                case GameState.Paused:
                default:
                    return InputContext.UI;
            }
        }
    }
}
