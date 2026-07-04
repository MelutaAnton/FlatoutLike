using FlatOutLike.Data.Configs.Camera;
using Unity.Cinemachine;
using UnityEngine;

namespace FlatOutLike.Controllers.Camera
{
    /// <summary>
    /// Тонкий адаптер над Cinemachine 3 vcam для orbit-режима (вокруг катапультированного
    /// водителя). Вращение — через <see cref="CinemachineOrbitalFollow.HorizontalAxis"/>,
    /// которое либо непрерывно крутится в Update (OrbitSpeed &gt; 0), либо стоит на месте
    /// (OrbitSpeed = 0, фиксированный ракурс — HighJump-режим).
    /// </summary>
    public sealed class CinemachineOrbitDriverCameraBehaviour : MonoBehaviour, ICameraBehaviour
    {
        [SerializeField] private CinemachineCamera _camera;
        [SerializeField] private OrbitCameraConfig _config;

        private CinemachineOrbitalFollow _orbital;

        private void Awake()
        {
            if (_camera == null)
            {
                Debug.LogWarning("[CinemachineOrbitDriverCameraBehaviour] _camera не назначен.");
                return;
            }

            // CinemachineOrbitalFollow (Body) — компонент пайплайна Cinemachine 3,
            // лежит на том же GameObject, что и CinemachineCamera.
            _orbital = _camera.GetComponent<CinemachineOrbitalFollow>();
            ApplyConfig();

            // По умолчанию неактивна (Priority=0): активацию делает CameraRig через
            // SetBehaviourActive, чтобы chase и orbit не боролись за приоритет до переключения.
            SetBehaviourActive(false);
        }

        private void ApplyConfig()
        {
            if (_config == null || _orbital == null) return;

            // FOV: выравниваем с chase-камерой (66°) — дефолтная линза CM 40° после бленда
            // читается как резкий «зум» на водителя. 0 в конфиге = не трогать.
            if (_config.FieldOfView > 0f)
            {
                var lens = _camera.Lens;
                lens.FieldOfView = _config.FieldOfView;
                _camera.Lens = lens;
            }

            // WorldSpace-биндинг ОБЯЗАТЕЛЕН: цель орбиты — кувыркающийся таз ragdoll,
            // дефолтный LockToTargetWithWorldUp вращает смещение за yaw цели, и камера
            // хаотично облетает водителя во время флипов. В WorldSpace угол — мировой.
            _orbital.TrackerSettings.BindingMode = Unity.Cinemachine.TargetTracking.BindingMode.WorldSpace;

            // Sphere-стиль держит камеру на сфере фиксированного радиуса вокруг цели —
            // ближайший аналог старой ручной орбиты (Distance = радиус в плоскости XZ).
            _orbital.OrbitStyle = CinemachineOrbitalFollow.OrbitStyles.Sphere;
            _orbital.Radius = _config.Distance;

            // Высота над целью в Sphere-стиле задаётся не отдельным полем, а VerticalAxis —
            // углом наклона (град, вращение вокруг right-вектора). Пересчитываем Height/Distance
            // в угол через atan2: при 0° камера на уровне цели, при 90° — прямо сверху.
            float verticalAngle = Mathf.Atan2(_config.Height, _config.Distance) * Mathf.Rad2Deg;
            _orbital.VerticalAxis.Range = new Vector2(-89f, 89f);
            _orbital.VerticalAxis.Center = verticalAngle;
            _orbital.VerticalAxis.Value = verticalAngle;

            // FollowDamping в старой системе — время SmoothDamp в секундах (дефолт 0.2).
            // TrackerSettings.PositionDamping — та же по смыслу величина (время сходления к
            // цели), хоть модели и отличаются (SmoothDamp vs экспоненциальный damping);
            // порядок величин совпадает, поэтому используем значение как есть, без масштабирования.
            _orbital.TrackerSettings.PositionDamping = Vector3.one * _config.FollowDamping;
        }

        public void SetTarget(Transform target)
        {
            if (_camera == null) return;

            _camera.Follow = target;
            _camera.LookAt = target;

            // Стартовый угол из конфига: с OrbitSpeed=0 даёт фиксированный ракурс
            // (180° = «сзади по ходу трассы», как chase при езде — HighJump-режим).
            if (_orbital != null && _config != null)
                _orbital.HorizontalAxis.Value = _config.StartAngleDegrees;
        }

        public void SetBehaviourActive(bool active)
        {
            if (_camera == null) return;

            // Приоритет — единственный переключатель между chase/orbit, как у chase-адаптера;
            // enabled компонента не трогаем (Update ниже продолжает крутить угол и в фоне —
            // это безопасно, т.к. orbit активируется один раз за матч и назад не переключается).
            _camera.Priority = new PrioritySettings { Enabled = true, Value = active ? 10 : 0 };
        }

        private void Update()
        {
            if (_orbital == null || _config == null) return;
            if (_config.OrbitSpeed == 0f) return;

            _orbital.HorizontalAxis.Value += _config.OrbitSpeed * Time.deltaTime;
        }
    }
}
