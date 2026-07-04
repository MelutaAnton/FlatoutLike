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
        /// Стартовый угол орбиты (град, мировой yaw, конвенция Cinemachine WorldSpace):
        /// 0 = камера в −Z от цели, смотрит по +Z — фиксированный ракурс «сзади по ходу
        /// трассы», как chase при езде (с OrbitSpeed=0 используется HighJump-конфигом).
        /// ВНИМАНИЕ: до миграции на Cinemachine этому ракурсу соответствовало 180.
        /// </summary>
        [SerializeField] private float _startAngleDegrees = 0f;

        /// <summary>
        /// FOV линзы орбитальной vcam; 0 = не трогать (останется дефолт Cinemachine 40°,
        /// что заметно «ближе», чем 66° у chase — задавать одинаково для бесшовного бленда).
        /// </summary>
        [SerializeField] private float _fieldOfView = 66f;

        public float Distance => _distance;
        public float Height => _height;
        public float OrbitSpeed => _orbitSpeed;
        public float FollowDamping => _followDamping;
        public float StartAngleDegrees => _startAngleDegrees;
        public float FieldOfView => _fieldOfView;
    }
}
