using System;
using FlatOutLike.Data.Configs.Enemies;
using FlatOutLike.Models;
using UnityEngine; // Vector2, Vector3 math only — no MonoBehaviour/GameObject/Transform

namespace FlatOutLike.Services.AI
{
    /// <summary>
    /// Stateless FSM AI brain for enemy cars.
    ///
    /// State machine:
    ///   Idle → Pursue → Ram → Recover → Pursue → …
    ///
    /// All mutable state lives in <see cref="AIBrainState"/> (passed ref by the caller).
    /// Pure C# — no MonoBehaviour, no Unity lifecycle methods.
    /// </summary>
    public sealed class EnemyAIService : IEnemyAIService
    {
        public AIDecision Tick(ref AIBrainState brain, in AIPerception p,
            EnemyPersonalityConfig profile, float deltaTime)
        {
            // ── No target → force Idle and stop ──────────────────────────────────
            if (!p.HasTarget)
            {
                brain.State = AIState.Idle;
                return new AIDecision(Vector2.zero, false, false);
            }

            // ── Geometry ─────────────────────────────────────────────────────────
            Vector3 toTarget = p.TargetPosition - p.SelfPosition;
            float   dist     = toTarget.magnitude;

            // Guard: avoid division by zero when positions coincide
            Vector3 dir = dist > 0.001f ? toTarget / dist : p.SelfForward;

            float forwardDot = Vector3.Dot(p.SelfForward, dir);       // +1 = target dead ahead
            float crossY     = Vector3.Cross(p.SelfForward, dir).y;   // positive = target to the right
            float steer      = ClampSigned(crossY * profile.SteerSharpness);

            // ── Optional retreat (short-circuits FSM) ─────────────────────────────
            // Aggressor profile has RetreatHpThreshold = 0 → this branch never fires.
            if (profile.RetreatHpThreshold > 0f && p.SelfHpRatio < profile.RetreatHpThreshold)
            {
                brain.State = AIState.Recover;
                if (brain.RecoverTimer <= 0f)
                    brain.RecoverTimer = profile.RecoverDuration;
            }

            // ── FSM ──────────────────────────────────────────────────────────────
            switch (brain.State)
            {
                // Idle: do nothing; transition to Pursue once target enters range.
                // On small arenas PursueStartDistance = 60 m → almost immediately Pursue.
                case AIState.Idle:
                    if (dist <= profile.PursueStartDistance)
                        brain.State = AIState.Pursue;
                    return new AIDecision(Vector2.zero, false, false);

                // Pursue: full throttle toward target; watch for ram window
                case AIState.Pursue:
                    return TickPursue(ref brain, profile, dist, forwardDot, steer);

                // Ram: close-quarters charge; disengage if target escapes or car is stuck
                case AIState.Ram:
                    return TickRam(ref brain, profile, dist, steer, p.SelfSpeed);

                // Recover: reverse out of stuck position, then resume pursuit
                case AIState.Recover:
                    return TickRecover(ref brain, profile, steer, deltaTime);

                default:
                    return new AIDecision(Vector2.zero, false, false);
            }
        }

        // ── State handlers ────────────────────────────────────────────────────────

        /// <summary>
        /// Pursue: steer toward target at full throttle.
        /// → Ram when close (dist &lt;= RamTriggerDistance) and target is roughly ahead (forwardDot &gt; profile.RamAlignmentDot).
        /// </summary>
        private static AIDecision TickPursue(ref AIBrainState brain, EnemyPersonalityConfig p,
            float dist, float forwardDot, float steer)
        {
            if (dist <= p.RamTriggerDistance && forwardDot > p.RamAlignmentDot)
            {
                // → Ram
                brain.State = AIState.Ram;
                return new AIDecision(new Vector2(steer, 1f), false, p.NitroOnRam);
            }

            return new AIDecision(new Vector2(steer, 1f), false, false);
        }

        /// <summary>
        /// Ram: charge at full speed with optional nitro.
        /// → Pursue when dist &gt; RamExitDistance (hysteresis prevents rapid flip).
        /// → Recover when forward speed drops below RecoverTriggerSpeed (stuck/wedged).
        /// </summary>
        private static AIDecision TickRam(ref AIBrainState brain, EnemyPersonalityConfig p,
            float dist, float steer, float selfSpeed)
        {
            // → Pursue: target escaped
            if (dist > p.RamExitDistance)
            {
                brain.State = AIState.Pursue;
                return new AIDecision(new Vector2(steer, 1f), false, false);
            }

            // → Recover: wedged against target or wall
            if (selfSpeed < p.RecoverTriggerSpeed)
            {
                brain.State        = AIState.Recover;
                brain.RecoverTimer = p.RecoverDuration;
                // Begin reversing immediately this tick
                return new AIDecision(new Vector2(-steer, -1f), false, false);
            }

            return new AIDecision(new Vector2(steer, 1f), false, p.NitroOnRam);
        }

        /// <summary>
        /// Recover: reverse while counter-steering so the car can pivot away.
        /// → Pursue when RecoverTimer reaches zero.
        /// </summary>
        private static AIDecision TickRecover(ref AIBrainState brain, EnemyPersonalityConfig p,
            float steer, float dt)
        {
            brain.RecoverTimer -= dt;

            // → Pursue: recovery window elapsed
            if (brain.RecoverTimer <= 0f)
            {
                brain.State = AIState.Pursue;
                return new AIDecision(new Vector2(steer, 1f), false, false);
            }

            // Counter-steer while reversing to pivot away from the obstacle
            return new AIDecision(new Vector2(-steer, -1f), false, false);
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Clamps <paramref name="value"/> to [-1, 1].
        /// Uses System.Math (not UnityEngine.Mathf) following DamageService precedent.
        /// </summary>
        private static float ClampSigned(float value) =>
            (float)Math.Max(-1.0, Math.Min(1.0, (double)value));
    }
}
