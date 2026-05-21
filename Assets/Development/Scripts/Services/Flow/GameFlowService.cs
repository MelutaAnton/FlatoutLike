using System.Collections.Generic;
using FlatOutLike.Core.Events;
using FlatOutLike.Core.StateMachine;
using FlatOutLike.Models;

namespace FlatOutLike.Services.Flow
{
    public sealed class GameFlowService : IGameFlowService
    {
        private readonly IEventBus _eventBus;
        private readonly StateMachine<GameState> _sm;

        public GameState Current => _sm.Current;

        public GameFlowService(IEventBus eventBus)
        {
            _eventBus = eventBus;

            // Placeholder states — real implementations added in steps 2+
            var emptyState = new EmptyState();
            var states = new Dictionary<GameState, IState>
            {
                { GameState.MainMenu,                 emptyState },
                { GameState.GarageMeta,               emptyState },
                { GameState.StuntsHub,                emptyState },
                { GameState.DerbyMatch,               emptyState },
                { GameState.StuntsHighJumpDriving,    emptyState },
                { GameState.StuntsHighJumpFlight,     emptyState },
                { GameState.StuntsHighJumpScoring,    emptyState },
                { GameState.MatchEnd,                 emptyState },
                { GameState.Paused,                   emptyState }
            };

            _sm = new StateMachine<GameState>(states, GameState.MainMenu);
        }

        public void ChangeState(GameState next)
        {
            var from = Current;
            _sm.ChangeState(next);
            _eventBus.Publish(new GameStateChangedEvent(from, next));
        }

        public void Push(GameState next)
        {
            var from = Current;
            _sm.Push(next);
            _eventBus.Publish(new GameStateChangedEvent(from, next));

            if (next == GameState.Paused)
                _eventBus.Publish(new GamePausedEvent());
        }

        public void Pop()
        {
            var leaving = Current;
            _sm.Pop();
            var to = Current;
            _eventBus.Publish(new GameStateChangedEvent(leaving, to));

            if (leaving == GameState.Paused)
                _eventBus.Publish(new GameResumedEvent());
        }

        public void Tick()
        {
            _sm.Tick();
        }

        // -------------------------------------------------------------------------
        // Placeholder — replaced by real state classes in subsequent steps
        // -------------------------------------------------------------------------
        private sealed class EmptyState : IState
        {
            public void Enter() { }
            public void Exit() { }
            public void Tick() { }
        }
    }
}
