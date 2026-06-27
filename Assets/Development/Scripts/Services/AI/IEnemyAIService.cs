using FlatOutLike.Data.Configs.Enemies;
using FlatOutLike.Models;
using UnityEngine;

namespace FlatOutLike.Services.AI
{
    /// <summary>
    /// Snapshot of the world as seen by the AI brain for one physics tick.
    /// All data is pre-computed by <c>EnemyCarController</c> from Unity API
    /// (Transform, Rigidbody, IDamageService) so the service stays pure C#.
    /// </summary>
    public readonly struct AIPerception
    {
        public readonly bool    HasTarget;
        public readonly Vector3 SelfPosition;
        /// <summary>Normalised forward direction of the enemy car.</summary>
        public readonly Vector3 SelfForward;
        /// <summary>Forward speed in m/s (positive = moving forward).</summary>
        public readonly float   SelfSpeed;
        /// <summary>0..1 HP fraction of the enemy car.</summary>
        public readonly float   SelfHpRatio;
        public readonly Vector3 TargetPosition;
        /// <summary>0..1 HP fraction of the target car.</summary>
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
    /// Mutable per-enemy FSM state. Held by <c>EnemyCarController</c>;
    /// passed <c>ref</c> to <see cref="IEnemyAIService.Tick"/> so the service is stateless.
    /// </summary>
    public struct AIBrainState
    {
        public AIState State;
        /// <summary>Counts down to 0 during the Recover phase.</summary>
        public float   RecoverTimer;
        /// <summary>Accumulates seconds spent wedged (low speed under throttle).</summary>
        public float   StuckTimer;
        /// <summary>Seconds spent close to the target but unable to aim at it (orbit detection).</summary>
        public float   OffAxisTimer;
        /// <summary>Post-Recover commit window; suppresses re-triggering back-off.</summary>
        public float   GraceTimer;
    }

    /// <summary>
    /// Control decision produced by one AI tick: the inputs forwarded to <c>AIInputProvider</c>.
    /// </summary>
    public readonly struct AIDecision
    {
        /// <summary>x = steer [-1, 1], y = throttle [-1, 1].</summary>
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
        /// Runs one FSM tick and returns the resulting car control inputs.
        /// Mutates <paramref name="brain"/> in-place (state transitions, timer countdown).
        /// </summary>
        AIDecision Tick(ref AIBrainState brain, in AIPerception perception,
            EnemyPersonalityConfig profile, float deltaTime);
    }
}
