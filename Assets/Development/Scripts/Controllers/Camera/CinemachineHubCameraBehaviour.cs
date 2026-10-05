using FlatOutLike.Data.Configs.Camera;
using Unity.Cinemachine;
using UnityEngine;

namespace FlatOutLike.Controllers.Camera
{
    /// <summary>
    /// Тонкий адаптер над Cinemachine 3 vcam для BaseHub (game-design §11): вид сверху-сзади
    /// на персонажа в МИРОВЫХ осях — камера не вращается вслед за персонажем, поэтому ввод
    /// стика/WASD однозначно мапится в мировые X/Z. Числа — в <see cref="HubCameraConfig"/>;
    /// здесь только их маппинг на <see cref="CinemachineFollow"/> (паттерн
    /// <see cref="CinemachineChaseCameraBehaviour"/>, без DI — конфиг прошивается в инспекторе).
    /// </summary>
    public sealed class CinemachineHubCameraBehaviour : MonoBehaviour, ICameraBehaviour
    {
        [SerializeField] private CinemachineCamera _camera;
        [SerializeField] private HubCameraConfig _config;

        private CinemachineFollow _follow;

        private void Awake()
        {
            if (_camera == null)
            {
                Debug.LogWarning("[CinemachineHubCameraBehaviour] _camera не назначен.");
                return;
            }

            // CinemachineFollow (Body) — компонент пайплайна CM3 на том же GameObject, что и vcam.
            _follow = _camera.GetComponent<CinemachineFollow>();

            ApplyConfig();
        }

        private void ApplyConfig()
        {
            if (_config == null)
            {
                Debug.LogWarning("[CinemachineHubCameraBehaviour] _config не назначен — используются текущие настройки vcam.");
                return;
            }

            // FOV: 0 в конфиге означает «не трогать текущее значение линзы».
            if (_config.FieldOfView > 0f)
            {
                var lens = _camera.Lens;
                lens.FieldOfView = _config.FieldOfView;
                _camera.Lens = lens;
            }

            if (_follow != null)
            {
                // WorldSpace: offset в мировых осях, поворот цели игнорируется — камера хаба
                // не крутится по yaw, когда персонаж разворачивается.
                _follow.FollowOffset = _config.FollowOffset;
                _follow.TrackerSettings.BindingMode = Unity.Cinemachine.TargetTracking.BindingMode.WorldSpace;
                _follow.TrackerSettings.PositionDamping = Vector3.one * _config.PositionDamping;
            }
            else
            {
                Debug.LogWarning("[CinemachineHubCameraBehaviour] На vcam нет CinemachineFollow — offset/damping не применены.");
            }
        }

        public void SetTarget(Transform target)
        {
            if (_camera == null) return;

            _camera.Follow = target;
            _camera.LookAt = target;
        }

        public void SetBehaviourActive(bool active)
        {
            if (_camera == null) return;

            _camera.Priority = new PrioritySettings
            {
                Enabled = true,
                Value = active ? CameraPriorities.Active : CameraPriorities.Inactive
            };
        }
    }
}
