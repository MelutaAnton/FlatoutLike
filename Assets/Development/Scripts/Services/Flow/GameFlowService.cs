using System;
using System.Collections.Generic;
using FlatOutLike.Core.Events;
using FlatOutLike.Core.StateMachine;
using FlatOutLike.Models;
using UnityEngine;

namespace FlatOutLike.Services.Flow
{
    public sealed class GameFlowService : IGameFlowService
    {
        private readonly IEventBus _eventBus;
        private readonly ISceneLoaderService _sceneLoader;
        private readonly StateMachine<GameState> _sm;

        public GameState Current => _sm.Current;

        public GameFlowService(IEventBus eventBus, ISceneLoaderService sceneLoader, GameState initialState)
        {
            _eventBus = eventBus;
            _sceneLoader = sceneLoader;

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
            if (IsTransitionBlocked(next))
                return;

            var from = Current;
            _sm.ChangeState(next);
            _eventBus.Publish(new GameStateChangedEvent(from, next));
        }

        public void Push(GameState next)
        {
            if (IsTransitionBlocked(next))
                return;

            var from = Current;
            _sm.Push(next);
            _eventBus.Publish(new GameStateChangedEvent(from, next));

            if (next == GameState.Paused)
                _eventBus.Publish(new GamePausedEvent());
        }

        public void Pop()
        {
            // Pop из Paused во время загрузки опубликовал бы GameResumedEvent — он снял бы
            // AudioListener.pause и вернул timeScale = 1 в замороженной старой сцене.
            if (IsTransitionBlocked($"Pop из {Current}"))
                return;

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

        /// <summary>
        /// Пока идёт загрузка сцены, FSM уже в состоянии назначения, а старая сцена доживает под
        /// экраном загрузки — любой новый переход (двойной тап, событие гибнущей сцены) отбрасывается
        /// целиком: без смены FSM и без GameStateChangedEvent.
        /// </summary>
        private bool IsTransitionBlocked(GameState next) => IsTransitionBlocked($"Переход в {next}");

        // transition — описание перехода для лога («Переход в X» / «Pop из X»).
        private bool IsTransitionBlocked(string transition)
        {
            if (!_sceneLoader.IsLoading)
                return false;

            Debug.Log($"[Flow] {transition} отброшен — идёт загрузка сцены.");
            return true;
        }
    }
}
