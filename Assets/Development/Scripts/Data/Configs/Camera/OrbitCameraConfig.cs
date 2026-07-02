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

        public float Distance => _distance;
        public float Height => _height;
        public float OrbitSpeed => _orbitSpeed;
        public float FollowDamping => _followDamping;
    }
}
