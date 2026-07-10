using FlatOutLike.Core.StateMachine;
using FlatOutLike.Models;
using UnityEngine;

namespace FlatOutLike.Services.Flow
{
    /// <summary>
    /// Тонкое лог-состояние для GameState, не привязанных к отдельной сцене
    /// (Flight/Scoring/MatchEnd/Paused) — они живут внутри текущей сцены, вся логика
    /// в соответствующих сервисах (DerbyMatchService, HighJumpSessionService и т.п.),
    /// подписанных на события напрямую через EventBus.
    /// </summary>
    public sealed class PassiveState : IState
    {
        private readonly GameState _state;

        public PassiveState(GameState state)
        {
            _state = state;
        }

        public void Enter() => Debug.Log($"[Flow] Enter {_state}");

        public void Exit() { }

        public void Tick() { }
    }
}
