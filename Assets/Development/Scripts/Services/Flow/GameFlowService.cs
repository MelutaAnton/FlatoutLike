using System;
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

        public GameFlowService(IEventBus eventBus, ISceneLoaderService sceneLoader, GameState initialState)
        {
            _eventBus = eventBus;

            // Словарь состояний строится по единственному источнику соответствия
            // GameState ↔ сцена (GameStateSceneMap): состояния со сценой получают
            // SceneEntryState (Enter грузит сцену через guard), остальные — тонкий
            // PassiveState без побочных эффектов.
            var states = new Dictionary<GameState, IState>();
            foreach (GameState state in Enum.GetValues(typeof(GameState)))
            {
                var sceneKey = GameStateSceneMap.SceneFor(state);
                states[state] = sceneKey != null
                    ? new SceneEntryState(state, sceneKey, sceneLoader)
                    : new PassiveState(state);
            }

            _sm = new StateMachine<GameState>(states, initialState);
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
    }
}
