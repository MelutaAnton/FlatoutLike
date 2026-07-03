using UnityEngine;

namespace FlatOutLike.Data.Configs.Driver
{
    /// <summary>
    /// Параметры управления водителем в свободном полёте (Шаг 7, «Stunts: High Jump»).
    /// Steer-коррекция и буст прикладываются через Rigidbody.AddForce на тазовую кость ragdoll —
    /// см. <c>FlatOutLike.Controllers.Driver.DriverFlightController</c>.
    /// </summary>
    [CreateAssetMenu(fileName = "DriverFlightConfig", menuName = "FlatOutLike/Driver/DriverFlightConfig")]
    public sealed class DriverFlightConfig : ScriptableObject
    {
        [SerializeField] private float _steerInfluence = 6f;
        [SerializeField, Range(0f, 1f)] private float _steerSpeedPenalty = 0.15f;
        [SerializeField] private float _boostImpulse = 8f;
        [SerializeField] private int   _boostCount = 1;
        [SerializeField] private float _rotationBonusThreshold = 180f;

        /// <summary>Ускорение (м/с²) боковой коррекции полёта по вводу Look.</summary>
        public float SteerInfluence => _steerInfluence;

        /// <summary>Доля торможения вдоль скорости при активной steer-коррекции (0..1).</summary>
        public float SteerSpeedPenalty => _steerSpeedPenalty;

        /// <summary>Импульс буста (м/с, ForceMode.VelocityChange) по направлению текущей скорости.</summary>
        public float BoostImpulse => _boostImpulse;

        /// <summary>Максимальное число бустов за один полёт.</summary>
        public int BoostCount => _boostCount;

        /// <summary>
        /// Порог угловой скорости (град/с) по оси, ниже которого вращение не аккумулируется
        /// во флип — отсекает дрожание ragdoll, которое не является намеренным трюком.
        /// </summary>
        public float RotationBonusThreshold => _rotationBonusThreshold;
    }
}
