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

        public float ForwardImpulse => _forwardImpulse;
        public float UpImpulse => _upImpulse;
    }
}
