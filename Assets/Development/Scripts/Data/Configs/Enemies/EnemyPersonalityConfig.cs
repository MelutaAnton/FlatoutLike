using UnityEngine;

namespace FlatOutLike.Data.Configs.Enemies
{
    /// <summary>
    /// Tuning knobs for one enemy archetype.
    /// Default values define the <b>Aggressor</b> profile: full aggression, no retreat.
    /// Assign different instances to different <c>EnemyCarController</c>s for varied behaviour.
    /// </summary>
    [CreateAssetMenu(fileName = "EnemyPersonalityConfig", menuName = "FlatOutLike/Enemies/EnemyPersonalityConfig")]
    public sealed class EnemyPersonalityConfig : ScriptableObject
    {
        [Header("Behaviour")]
        [SerializeField, Range(0f, 1f)] private float _ramAggression      = 1f;
        [SerializeField, Range(0f, 1f)] private float _retreatHpThreshold = 0f;   // Aggressor never retreats
        [SerializeField, Range(0f, 1f)] private float _pickupPriority     = 0.5f; // reserved — Step 5
        [SerializeField, Min(0f)]       private float _targetSwitchCooldown = 2f; // reserved — Step 5

        [Header("Engagement Distances")]
        [SerializeField] private float _pursueStartDistance  = 60f;  // beyond this → Idle / hold Pursue
        [SerializeField] private float _ramTriggerDistance   = 12f;  // inside → switch to Ram
        [SerializeField] private float _ramExitDistance      = 18f;  // hysteresis: beyond this in Ram → back to Pursue

        [Header("Recovery")]
        [SerializeField] private float _recoverDuration      = 1.2f; // seconds reversing after getting stuck
        [SerializeField] private float _recoverTriggerSpeed  = 1.5f; // m/s — below this in Ram state → stuck, enter Recover

        [Header("Control Tuning")]
        [SerializeField] private bool  _nitroOnRam    = true;
        [SerializeField] private float _steerSharpness = 1.5f; // heading error → steer input multiplier
        /// <summary>cos угла: насколько цель должна быть по курсу, чтобы войти в Ram.</summary>
        [SerializeField, Range(-1f, 1f)] private float _ramAlignmentDot = 0.3f;

        public float RamAggression       => _ramAggression;
        public float RetreatHpThreshold  => _retreatHpThreshold;
        public float PickupPriority      => _pickupPriority;
        public float TargetSwitchCooldown => _targetSwitchCooldown;
        public float PursueStartDistance => _pursueStartDistance;
        public float RamTriggerDistance  => _ramTriggerDistance;
        public float RamExitDistance     => _ramExitDistance;
        public float RecoverDuration     => _recoverDuration;
        public float RecoverTriggerSpeed => _recoverTriggerSpeed;
        public bool  NitroOnRam          => _nitroOnRam;
        public float SteerSharpness      => _steerSharpness;
        public float RamAlignmentDot     => _ramAlignmentDot;
    }
}
