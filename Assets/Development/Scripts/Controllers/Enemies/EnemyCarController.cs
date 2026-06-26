using FlatOutLike.Controllers.Car;
using FlatOutLike.Core.Input;
using FlatOutLike.Data.Configs.Car;
using FlatOutLike.Data.Configs.Enemies;
using FlatOutLike.Models;
using FlatOutLike.Services.AI;
using FlatOutLike.Services.Combat;
using UnityEngine;
using VContainer;

namespace FlatOutLike.Controllers.Enemies
{
    /// <summary>
    /// Drives the AI FSM tick each FixedUpdate and pushes the resulting inputs into
    /// the sibling <see cref="CarController"/> via an <see cref="AIInputProvider"/>.
    ///
    /// Ordering note: EnemyCarController writes to <c>_aiInput</c> in its own FixedUpdate;
    /// CarController reads those values in its own FixedUpdate. Unity does not guarantee
    /// execution order across components on different GameObjects, so up to 1 physics-frame
    /// lag is possible — imperceptible at 50 Hz and acceptable for arcade AI.
    /// </summary>
    // Invariant: this component, its CarController (_car) and Rigidbody (_rigidbody)
    // live on the SAME GameObject. Perception is computed from _car's transform so
    // the AI reasons about the controlled vehicle, not this component's transform.
    public sealed class EnemyCarController : MonoBehaviour
    {
        [SerializeField] private CarController         _car;        // sibling car being controlled
        [SerializeField] private CarController         _target;     // player car (reassignable via SetTarget)
        [SerializeField] private CarStatsConfig        _statsConfig; // reserved for future per-enemy stat overrides
        [SerializeField] private EnemyPersonalityConfig _profile;
        [SerializeField] private Rigidbody             _rigidbody;

        private IEnemyAIService _ai;
        private IDamageService  _damage;
        private AIInputProvider _aiInput;
        private AIBrainState    _brain;
        private bool            _initialized;

        [Inject]
        public void Construct(IEnemyAIService ai, IDamageService damage)
        {
            _ai     = ai;
            _damage = damage;

            _aiInput = new AIInputProvider();
            _brain   = new AIBrainState { State = AIState.Idle, RecoverTimer = 0f };

            // Replace the keyboard provider with the AI-writable one.
            // OnEnable may have already fired before Construct — the guard inside
            // CarController is irrelevant here since we call the public API directly.
            if (_car != null)
                _car.SetInputSource(_aiInput);

            _initialized = true;
        }

        /// <summary>
        /// Replaces the current target. Called by the factory / targeting service (Step 5).
        /// Safe to call before Construct — target is only read inside FixedUpdate.
        /// </summary>
        public void SetTarget(CarController target) => _target = target;

        private void FixedUpdate()
        {
            if (!_initialized || _car == null || _profile == null || _rigidbody == null) return;

            bool hasTarget = _target != null;

            // ── Self HP ratio ─────────────────────────────────────────────────────
            float selfMaxArmor = _car.MaxArmor;
            float selfHp       = _damage.GetHp(_car.CarId);
            float selfRatio    = selfMaxArmor > 0f ? selfHp / selfMaxArmor : 1f;

            // ── Target HP ratio ───────────────────────────────────────────────────
            float targetRatio = 1f;
            if (hasTarget)
            {
                float targetMaxArmor = _target.MaxArmor;
                float targetHp       = _damage.GetHp(_target.CarId);
                targetRatio = targetMaxArmor > 0f ? targetHp / targetMaxArmor : 1f;
            }

            // ── Forward speed (Unity 6: linearVelocity, NOT legacy .velocity) ─────
            // Perception uses _car.transform so the AI reasons about the controlled vehicle.
            Vector3 selfPos = _car.transform.position;
            Vector3 selfFwd = _car.transform.forward;
            float selfSpeed = _rigidbody != null ? Vector3.Dot(_rigidbody.linearVelocity, selfFwd) : 0f;

            // ── Build perception snapshot ─────────────────────────────────────────
            var perception = new AIPerception(
                hasTarget:      hasTarget,
                selfPosition:   selfPos,
                selfForward:    selfFwd,
                selfSpeed:      selfSpeed,
                selfHpRatio:    selfRatio,
                targetPosition: hasTarget ? _target.transform.position : Vector3.zero,
                targetHpRatio:  targetRatio
            );

            // ── AI tick ───────────────────────────────────────────────────────────
            AIDecision decision = _ai.Tick(ref _brain, perception, _profile, Time.fixedDeltaTime);

            // ── Push result to the input source that CarController reads ──────────
            _aiInput.Set(decision.Move, decision.Handbrake, decision.Nitro);
        }
    }
}
