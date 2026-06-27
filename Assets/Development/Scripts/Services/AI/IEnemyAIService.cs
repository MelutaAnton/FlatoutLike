using FlatOutLike.Data.Configs.Enemies;
using FlatOutLike.Models;
using UnityEngine;

namespace FlatOutLike.Services.AI
{
    /// <summary>
    /// Снимок мира, каким его видит AI-мозг в одном физическом тике.
    /// Все данные предварительно вычисляются <c>EnemyCarController</c> из Unity API
    /// (Transform, Rigidbody, IDamageService), чтобы Service оставался pure C#.
    /// </summary>
    public readonly struct AIPerception
    {
        public readonly bool    HasTarget;
        public readonly Vector3 SelfPosition;
        /// <summary>Нормализованное направление вперёд для машины врага.</summary>
        public readonly Vector3 SelfForward;
        /// <summary>Скорость вперёд в м/с (положительное = движется вперёд).</summary>
        public readonly float   SelfSpeed;
        /// <summary>Доля HP машины врага, 0..1.</summary>
        public readonly float   SelfHpRatio;
        public readonly Vector3 TargetPosition;
        /// <summary>Доля HP машины цели, 0..1.</summary>
        public readonly float   TargetHpRatio;

        public AIPerception(bool hasTarget, Vector3 selfPosition, Vector3 selfForward,
            float selfSpeed, float selfHpRatio, Vector3 targetPosition, float targetHpRatio)
        {
            HasTarget      = hasTarget;
            SelfPosition   = selfPosition;
            SelfForward    = selfForward;
            SelfSpeed      = selfSpeed;
            SelfHpRatio    = selfHpRatio;
            TargetPosition = targetPosition;
            TargetHpRatio  = targetHpRatio;
        }
    }

    /// <summary>
    /// Изменяемое FSM-состояние для одного врага. Хранится в <c>EnemyCarController</c>;
    /// передаётся <c>ref</c> в <see cref="IEnemyAIService.Tick"/>, чтобы Service оставался stateless.
    /// </summary>
    public struct AIBrainState
    {
        public AIState State;
        /// <summary>Отсчитывает до 0 в фазе Recover.</summary>
        public float   RecoverTimer;
        /// <summary>Копит секунды простоя в упоре (низкая скорость при газе).</summary>
        public float   StuckTimer;
        /// <summary>Секунды рядом с целью без возможности прицелиться (детекция орбиты).</summary>
        public float   OffAxisTimer;
        /// <summary>Окно фиксации курса после Recover; подавляет повторный запуск отката.</summary>
        public float   GraceTimer;
    }

    /// <summary>
    /// Решение об управлении за один AI-тик: инпуты, передаваемые в <c>AIInputProvider</c>.
    /// </summary>
    public readonly struct AIDecision
    {
        /// <summary>x = руль [-1, 1], y = газ [-1, 1].</summary>
        public readonly Vector2 Move;
        public readonly bool    Handbrake;
        public readonly bool    Nitro;

        public AIDecision(Vector2 move, bool handbrake, bool nitro)
        {
            Move      = move;
            Handbrake = handbrake;
            Nitro     = nitro;
        }
    }

    public interface IEnemyAIService
    {
        /// <summary>
        /// Прогоняет один FSM-тик и возвращает итоговые инпуты управления машиной.
        /// Изменяет <paramref name="brain"/> на месте (переходы состояний, отсчёт таймеров).
        /// </summary>
        AIDecision Tick(ref AIBrainState brain, in AIPerception perception,
            EnemyPersonalityConfig profile, float deltaTime);
    }
}
