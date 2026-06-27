using System;
using FlatOutLike.Data.Configs.Enemies;
using FlatOutLike.Models;
using UnityEngine; // Vector2, Vector3 math only — no MonoBehaviour/GameObject/Transform

namespace FlatOutLike.Services.AI
{
    /// <summary>
    /// Stateless FSM AI brain for enemy cars.
    ///
    /// State machine: Idle → Pursue → Ram → Recover → Pursue → …
    ///
    /// Movement philosophy: FULL throttle while engaged. A car's minimum turn radius is
    /// fixed by its steering geometry, so crawling does NOT let it out-turn a tight orbit —
    /// instead, orbiting is broken by an explicit back-off (Recover): once the car has been
    /// close to the target but unable to aim at it for CircleBreakTime seconds (OffAxisTimer),
    /// or wedged on geometry for StuckTime seconds (StuckTimer), it reverses to gain
    /// separation and swing its nose onto the target, then makes a fresh straight-in run.
    /// A GraceTime window after each Recover forces a committed forward run, so the car
    /// never jitters back-and-forth in place.
    ///
    /// All mutable state lives in <see cref="AIBrainState"/> (passed ref by the caller).
    /// Pure C# — no MonoBehaviour, no Unity lifecycle methods.
    /// </summary>
    public sealed class EnemyAIService : IEnemyAIService
    {
        public AIDecision Tick(ref AIBrainState brain, in AIPerception p,
            EnemyPersonalityConfig profile, float deltaTime)
        {
            // ── No target → Idle and stop ────────────────────────────────────────
            if (!p.HasTarget)
            {
                brain.State        = AIState.Idle;
                brain.OffAxisTimer = 0f;
                brain.StuckTimer   = 0f;
                return new AIDecision(Vector2.zero, false, false);
            }

            // ── Geometry ─────────────────────────────────────────────────────────
            Vector3 toTarget = p.TargetPosition - p.SelfPosition;
            float   dist     = toTarget.magnitude;
            Vector3 dir      = dist > 0.001f ? toTarget / dist : p.SelfForward;

            float forwardDot = Vector3.Dot(p.SelfForward, dir);       // +1 = target dead ahead
            float crossY     = Vector3.Cross(p.SelfForward, dir).y;   // positive = target to the right
            float steer      = ClampSigned(crossY * profile.SteerSharpness);

            // ── Grace countdown (commit-to-forward window after a back-off) ───────
            if (brain.GraceTimer > 0f)
                brain.GraceTimer -= deltaTime;

            // ── Optional retreat (short-circuits FSM) ────────────────────────────
            // Aggressor profile has RetreatHpThreshold = 0 → never fires.
            if (profile.RetreatHpThreshold > 0f && p.SelfHpRatio < profile.RetreatHpThreshold
                && brain.State != AIState.Recover)
            {
                EnterRecover(ref brain, profile);
                return Reverse(steer);
            }

            // ── Global back-off triggers (cover Pursue AND Ram uniformly) ────────
            bool engaged = brain.State == AIState.Pursue || brain.State == AIState.Ram;

            // Wedged: full throttle is always commanded while engaged, so near-zero speed
            // MAGNITUDE unambiguously means stuck on a wall/obstacle. Use |SelfSpeed| (not
            // signed) so a reversing/shoved-back car is NOT misread as stuck (no moonwalk).
            if (engaged && Math.Abs(p.SelfSpeed) < profile.RecoverTriggerSpeed)
                brain.StuckTimer += deltaTime;
            else
                brain.StuckTimer = 0f;

            // Orbiting: close enough to engage but can't get the nose on the target.
            // Gated on GraceTimer<=0 so the post-back-off commit window is a FULL forward
            // run: we only start counting toward the next back-off after grace expires.
            // Without this the orbit timer fills during grace and re-triggers the instant
            // grace ends, producing a reverse / short-forward / reverse rhythm.
            if (brain.GraceTimer <= 0f && engaged
                && dist <= profile.RamExitDistance && forwardDot < profile.RamAlignmentDot)
                brain.OffAxisTimer += deltaTime;
            else
                brain.OffAxisTimer = 0f;

            // Trigger the back-off maneuver — suppressed during the grace window.
            if (brain.GraceTimer <= 0f && engaged
                && (brain.StuckTimer >= profile.StuckTime || brain.OffAxisTimer >= profile.CircleBreakTime))
            {
                EnterRecover(ref brain, profile);
                return Reverse(steer);
            }

            // ── FSM ──────────────────────────────────────────────────────────────
            switch (brain.State)
            {
                case AIState.Idle:
                    if (dist <= profile.PursueStartDistance)
                        brain.State = AIState.Pursue;
                    return new AIDecision(Vector2.zero, false, false);

                case AIState.Pursue:
                    // → Ram: close and aimed
                    if (dist <= profile.RamTriggerDistance && forwardDot > profile.RamAlignmentDot)
                    {
                        brain.State = AIState.Ram;
                        return Drive(steer, profile.NitroOnRam);
                    }
                    return Drive(steer, false);

                case AIState.Ram:
                    // → Pursue: target escaped (hysteresis RamExit > RamTrigger)
                    if (dist > profile.RamExitDistance)
                    {
                        brain.State = AIState.Pursue;
                        return Drive(steer, false);
                    }
                    // Nitro only while actually aimed at the target — boosting off-axis
                    // just widens the turn radius and feeds the orbit.
                    return Drive(steer, profile.NitroOnRam && forwardDot > profile.RamAlignmentDot);

                case AIState.Recover:
                    return TickRecover(ref brain, profile, steer, deltaTime);

                default:
                    return new AIDecision(Vector2.zero, false, false);
            }
        }

        // ── Recover ─────────────────────────────────────────────────────────────
        /// <summary>
        /// Reverse while counter-steering: backing up with -steer rotates the nose toward
        /// the target while the car moves away, gaining separation for a straight-in run.
        /// → Pursue when RecoverTimer elapses, opening a GraceTime commit window.
        /// </summary>
        private static AIDecision TickRecover(ref AIBrainState brain, EnemyPersonalityConfig p,
            float steer, float dt)
        {
            brain.RecoverTimer -= dt;

            if (brain.RecoverTimer <= 0f)
            {
                brain.State        = AIState.Pursue;
                brain.GraceTimer   = p.GraceTime;
                brain.OffAxisTimer = 0f;
                brain.StuckTimer   = 0f;
                return Drive(steer, false);
            }

            return Reverse(steer);
        }

        // ── Helpers ─────────────────────────────────────────────────────────────
        private static AIDecision Drive(float steer, bool nitro) =>
            new AIDecision(new Vector2(steer, 1f), false, nitro);

        private static AIDecision Reverse(float steer) =>
            new AIDecision(new Vector2(-steer, -1f), false, false);

        private static void EnterRecover(ref AIBrainState brain, EnemyPersonalityConfig p)
        {
            brain.State        = AIState.Recover;
            brain.RecoverTimer = p.RecoverDuration;
            brain.StuckTimer   = 0f;
            brain.OffAxisTimer = 0f;
        }

        /// <summary>Clamps to [-1, 1] via System.Math (not Mathf), per DamageService precedent.</summary>
        private static float ClampSigned(float value) =>
            (float)Math.Max(-1.0, Math.Min(1.0, (double)value));
    }
}
