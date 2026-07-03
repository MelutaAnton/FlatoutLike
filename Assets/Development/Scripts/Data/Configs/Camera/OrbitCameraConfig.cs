using UnityEngine;

namespace FlatOutLike.Data.Configs.Camera
{
    /// <summary>
    /// Параметры орбитальной камеры вокруг катапультированного водителя (ragdoll-таз).
    /// </summary>
    [CreateAssetMenu(fileName = "OrbitCameraConfig", menuName = "FlatOutLike/Camera/OrbitCameraConfig")]
    public sealed class OrbitCameraConfig : ScriptableObject
    {
        [SerializeField] private float _distance = 6f;
        [SerializeField] private float _height = 2.5f;
        [SerializeField] private float _orbitSpeed = 30f;
        [SerializeField] private float _followDamping = 0.2f;

        /// <summary>
        /// Стартовый угол орбиты (град, мировой yaw). 0 = камера с +Z от цели.
        /// 180 + OrbitSpeed=0 = фиксированный ракурс «сзади по ходу трассы +Z»,
        /// как chase-камера при езде (используется HighJump-конфигом).
        /// </summary>
        [SerializeField] private float _startAngleDegrees = 0f;

        public float Distance => _distance;
        public float Height => _height;
        public float OrbitSpeed => _orbitSpeed;
        public float FollowDamping => _followDamping;
        public float StartAngleDegrees => _startAngleDegrees;
    }
}
