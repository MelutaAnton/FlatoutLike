using FlatOutLike.Core.StateMachine;
using UnityEngine;

namespace FlatOutLike.Services.Flow
{
    /// <summary>
    /// Состояние GameFlow во время Derby-матча. Намеренно тонкое — вся логика матча
    /// (спавн, отслеживание живых машин, награды) находится в <c>IDerbyMatchService</c>,
    /// подписанном на события напрямую через EventBus. Без зависимостей, чтобы не создавать
    /// цикл GameFlow ↔ Match.
    /// </summary>
    public sealed class DerbyMatchState : IState
    {
        public void Enter() => Debug.Log("[Flow] Enter DerbyMatch");

        public void Exit() { }

        public void Tick() { }
    }
}
