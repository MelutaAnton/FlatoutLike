using FlatOutLike.Core.StateMachine;
using UnityEngine;

namespace FlatOutLike.Services.Flow
{
    /// <summary>
    /// Состояние GameFlow после завершения матча. Намеренно тонкое — результаты матча
    /// и награды публикуются <c>IDerbyMatchService</c> до перехода сюда через события.
    /// Без зависимостей, чтобы не создавать цикл GameFlow ↔ Match.
    /// </summary>
    public sealed class MatchEndState : IState
    {
        public void Enter() => Debug.Log("[Flow] Enter MatchEnd");

        public void Exit() { }

        public void Tick() { }
    }
}
