using UnityEngine;

namespace FlatOutLike.Data.Configs.Camera
{
    /// <summary>
    /// Камера BaseHub (game-design §11): вид сверху-сзади на персонажа в мировых осях
    /// (камера не вращается по yaw вслед за персонажем).
    /// </summary>
    [CreateAssetMenu(fileName = "HubCameraConfig", menuName = "FlatOutLike/Camera/HubCameraConfig")]
    public sealed class HubCameraConfig : ScriptableObject
    {
        [Tooltip("Смещение камеры от персонажа в мировых осях, м.")]
        [SerializeField] private Vector3 _followOffset = new Vector3(0f, 14f, -10f);

        [Tooltip("FOV камеры; 0 — не трогать текущее значение линзы.")]
        [SerializeField] private float _fieldOfView = 50f;

        [Tooltip("Damping следования (Cinemachine), одинаковый для всех осей.")]
        [SerializeField] private float _positionDamping = 0.5f;

        public Vector3 FollowOffset => _followOffset;

        /// <summary>FOV камеры; 0 означает «не трогать текущее значение».</summary>
        public float FieldOfView => _fieldOfView;

        public float PositionDamping => _positionDamping;
    }
}
