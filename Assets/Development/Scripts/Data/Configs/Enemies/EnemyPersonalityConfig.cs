using UnityEngine;

namespace FlatOutLike.Data.Configs.Enemies
{
    /// <summary>
    /// Параметры тюнинга одного архетипа врага.
    /// Значения по умолчанию задают профиль <b>Aggressor</b>: максимальная агрессия, без отступления.
    /// Назначай разные инстансы разным <c>EnemyCarController</c> для разнообразного поведения.
    /// </summary>
    [CreateAssetMenu(fileName = "EnemyPersonalityConfig", menuName = "FlatOutLike/Enemies/EnemyPersonalityConfig")]
    public sealed class EnemyPersonalityConfig : ScriptableObject
    {
        [Header("Behaviour")]
        [SerializeField, Range(0f, 1f)] private float _ramAggression      = 1f;
        [SerializeField, Range(0f, 1f)] private float _retreatHpThreshold = 0f;   // Aggressor никогда не отступает
        [SerializeField, Range(0f, 1f)] private float _pickupPriority     = 0.5f; // зарезервировано — шаг 5
        [SerializeField, Min(0f)]       private float _targetSwitchCooldown = 2f; // зарезервировано — шаг 5

        [Header("Engagement Distances")]
        [SerializeField] private float _pursueStartDistance  = 60f;  // за пределами → Idle / держим Pursue
        [SerializeField] private float _ramTriggerDistance   = 12f;  // внутри → переход в Ram
        [SerializeField] private float _ramExitDistance      = 18f;  // гистерезис: за пределами в Ram → обратно в Pursue

        [Header("Recovery")]
        [SerializeField] private float _recoverDuration      = 1.2f; // секунды реверса после застревания
        [SerializeField] private float _recoverTriggerSpeed  = 1.5f; // м/с — ниже этого в Ram → застряли, вход в Recover

        [Header("Control Tuning")]
        [SerializeField] private bool  _nitroOnRam    = true;
        [SerializeField] private float _steerSharpness = 1.5f; // ошибка курса → множитель ввода steer
        /// <summary>cos угла: насколько цель должна быть по курсу, чтобы войти в Ram.</summary>
        [SerializeField, Range(-1f, 1f)] private float _ramAlignmentDot = 0.5f;
        /// <summary>Сколько секунд подряд скорость ниже RecoverTriggerSpeed (при подаче газа) до срабатывания Recover.</summary>
        [SerializeField, Min(0f)]        private float _stuckTime = 0.6f;
        /// <summary>Секунды рядом с целью без возможности прицелиться, после которых машина откатывается для нового прямого захода (анти-орбита).</summary>
        [SerializeField, Min(0f)] private float _circleBreakTime = 1f;
        /// <summary>После отката (Recover) едем вперёд хотя бы столько секунд, прежде чем может сработать новый откат — защита от дёрганья.</summary>
        [SerializeField, Min(0f)] private float _graceTime = 1.5f;
        /// <summary>Минимальная скорость сближения с целью (м/с): forward-velocity, спроецированная на направление к цели. Ниже = считаем, что не приближаемся.</summary>
        [SerializeField, Min(0f)] private float _minApproachSpeed = 1f;
        /// <summary>Сколько секунд подряд скорость сближения ниже MinApproachSpeed до срабатывания Recover. Ловит широкую орбиту вне зоны OffAxisTimer.</summary>
        [SerializeField, Min(0f)] private float _approachTime = 1.5f;
        /// <summary>Дистанция до препятствия по центральному forward-лучу, при которой включается обход. Меньше — машина свернёт в сторону более чистого бокового луча.</summary>
        [SerializeField, Min(0f)] private float _obstacleAvoidDistance = 8f;
        /// <summary>Дистанция до препятствия по перпендикулярному side-лучу, при которой запрещаем target-steer заворачивать в эту сторону (защита от тычка боком).</summary>
        [SerializeField, Min(0f)] private float _obstacleSideAvoidDistance = 1.5f;

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
        public float StuckTime           => _stuckTime;
        public float CircleBreakTime     => _circleBreakTime;
        public float GraceTime           => _graceTime;
        public float MinApproachSpeed    => _minApproachSpeed;
        public float ApproachTime        => _approachTime;
        public float ObstacleAvoidDistance     => _obstacleAvoidDistance;
        public float ObstacleSideAvoidDistance => _obstacleSideAvoidDistance;
    }
}
