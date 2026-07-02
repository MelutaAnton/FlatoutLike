using UnityEngine;

namespace FlatOutLike.Data.Configs.Driver
{
    /// <summary>
    /// Параметры импульса катапультирования водителя. Импульс прикладывается через
    /// ForceMode.VelocityChange, поэтому значения — м/с, а не сила.
    /// </summary>
    [CreateAssetMenu(fileName = "DriverEjectConfig", menuName = "FlatOutLike/Driver/DriverEjectConfig")]
    public sealed class DriverEjectConfig : ScriptableObject
    {
        [SerializeField] private float _forwardImpulse = 8f;
        [SerializeField] private float _upImpulse = 6f;

        /// <summary>
        /// Доля скорости машины (0..1), наследуемая водителем в момент вылета:
        /// у быстро едущей машины водитель летит по ходу движения, у стоящей — только вверх-вперёд.
        /// </summary>
        [SerializeField, Range(0f, 1f)] private float _carVelocityInheritance = 0.6f;

        public float ForwardImpulse => _forwardImpulse;
        public float UpImpulse => _upImpulse;
        public float CarVelocityInheritance => _carVelocityInheritance;
    }
}
