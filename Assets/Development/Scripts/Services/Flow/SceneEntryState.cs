using FlatOutLike.Core.StateMachine;
using FlatOutLike.Models;
using UnityEngine;

namespace FlatOutLike.Services.Flow
{
    /// <summary>
    /// Единственная реализация IState для всех состояний, привязанных к отдельной сцене
    /// (BaseHub, GarageMeta, DerbyMatch, StuntsHighJumpDriving). Межэкранный
    /// переход = загрузка сцены; guard на ActiveSceneName не даёт перезагрузить сцену,
    /// в которой scope уже стартовал в этом состоянии.
    /// </summary>
    public sealed class SceneEntryState : IState
    {
        private readonly GameState _state;
        private readonly string _sceneKey;
        private readonly ISceneLoaderService _loader;

        public SceneEntryState(GameState state, string sceneKey, ISceneLoaderService loader)
        {
            _state = state;
            _sceneKey = sceneKey;
            _loader = loader;
        }

        public void Enter()
        {
            Debug.Log($"[Flow] Enter {_state}");

            // Guard: если сцена уже активна (scope стартовал прямо в ней — обычный путь
            // при старте GameFlowService в собственной сцене), повторная загрузка не нужна.
            if (_loader.ActiveSceneName != _sceneKey)
                _loader.Load(_sceneKey);
        }

        public void Exit() { }

        public void Tick() { }
    }
}
