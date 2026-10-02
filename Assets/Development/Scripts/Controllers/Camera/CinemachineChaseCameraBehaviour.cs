using FlatOutLike.Data.Configs.Camera;
using Unity.Cinemachine;
using UnityEngine;

namespace FlatOutLike.Controllers.Camera
{
    /// <summary>
    /// Тонкий адаптер над Cinemachine 3 vcam для chase-режима (следование за машиной).
    /// Вся числовая логика — в <see cref="ChaseCameraConfig"/>; здесь только маппинг цифр
    /// конфига на компоненты пайплайна Cinemachine: <see cref="CinemachineFollow"/> (Body)
    /// и <see cref="CinemachineRotationComposer"/> (Aim). Сама математика следования/сглаживания
    /// теперь считается Cinemachine, а не вручную (как раньше в SmoothDamp/Slerp).
    /// </summary>
    public sealed class CinemachineChaseCameraBehaviour : MonoBehaviour, ICameraBehaviour
    {
        // Эмпирический множитель пересчёта PositionSmoothing (SmoothDamp, с) в damping Cinemachine.
        private const float PositionDampingScale = 6f;
        // Числитель инверсии RotationSmoothing → damping (RotationSmoothing=10 даёт damping=1).
        private const float RotationDampingNumerator = 10f;
        // Нижний предел damping (позиция и поворот).
        private const float MinDamping = 0.05f;
        // Верхний предел damping позиции.
        private const float MaxPositionDamping = 3f;
        // Верхний предел damping поворота.
        private const float MaxRotationDamping = 5f;
        // Защитный минимум RotationSmoothing — от деления на ноль.
        private const float MinRotationSmoothing = 0.01f;
        // Верхний предел сглаживания look-ahead.
        private const float MaxLookAheadSmoothing = 30f;

        [SerializeField] private CinemachineCamera _camera;
        [SerializeField] private ChaseCameraConfig _config;
        [SerializeField] private DeoccluderConfig _deoccluderConfig;

        private CinemachineFollow _follow;
        private CinemachineRotationComposer _composer;

        private void Awake()
        {
            if (_camera == null)
            {
                Debug.LogWarning("[CinemachineChaseCameraBehaviour] _camera не назначен.");
                return;
            }

            // CinemachineFollow (Body) и CinemachineRotationComposer (Aim) — компоненты
            // пайплайна Cinemachine 3, лежат на том же GameObject, что и CinemachineCamera.
            _follow = _camera.GetComponent<CinemachineFollow>();
            _composer = _camera.GetComponent<CinemachineRotationComposer>();

            ApplyConfig();

            // Антиклиппинг применяется отдельно от ApplyConfig: он не должен гейтиться
            // ранним return при отсутствующем ChaseCameraConfig — настройки независимы.
            DeoccluderApplier.Apply(_camera.GetComponent<CinemachineDeoccluder>(), _deoccluderConfig);
        }

        private void ApplyConfig()
        {
            if (_config == null) return;

            // FOV: 0 в конфиге исторически означает «не трогать текущее значение линзы».
            if (_config.FieldOfView > 0f)
            {
                var lens = _camera.Lens;
                lens.FieldOfView = _config.FieldOfView;
                _camera.Lens = lens;
            }

            if (_follow != null)
            {
                // Offset «сзади и выше» машины. LockToTargetWithWorldUp вращает offset вслед
                // за yaw цели (классический chase), но игнорирует крен/тангаж машины —
                // аналог старой ручной формулы (target.position - target.forward * dist + up * h),
                // где камера не наклонялась вместе с машиной.
                _follow.FollowOffset = new Vector3(0f, _config.Height, -_config.FollowDistance);
                _follow.TrackerSettings.BindingMode = Unity.Cinemachine.TargetTracking.BindingMode.LockToTargetWithWorldUp;

                // Маппинг PositionSmoothing (время SmoothDamp в секундах, наш дефолт 0.09) в
                // Cinemachine damping (тоже примерно время сходления к цели, но модель другая —
                // экспоненциальная, а не критически задемпфированная SmoothDamp). Эмпирический
                // множитель x6 подобран так, чтобы дефолт 0.09с давал damping ~0.5 — заметный,
                // но не вялый чейз. Поле одно число на все 3 оси (изотропный damping).
                float positionDamping = Mathf.Clamp(_config.PositionSmoothing * PositionDampingScale, MinDamping, MaxPositionDamping);
                _follow.TrackerSettings.PositionDamping = Vector3.one * positionDamping;
            }

            if (_composer != null)
            {
                _composer.TargetOffset = Vector3.up * _config.LookAtHeightOffset;

                // RotationSmoothing в старой системе — множитель скорости Slerp (больше значение =
                // быстрее доворот). Damping в RotationComposer — обратная по смыслу величина
                // (больше значение = медленнее), поэтому инвертируем: damping = k / RotationSmoothing.
                // k=10 подобран так, что дефолтный RotationSmoothing=10 даёт damping=1
                // (умеренная отзывчивость, аналог старого поведения).
                float rotationDamping = Mathf.Clamp(
                    RotationDampingNumerator / Mathf.Max(_config.RotationSmoothing, MinRotationSmoothing),
                    MinDamping, MaxRotationDamping);
                _composer.Damping = new Vector2(rotationDamping, rotationDamping);

                // Lookahead: прямого аналога LookAheadFactor/MaxLookAhead в CM3 нет — там был
                // клэмп смещения точки взгляда по величине скорости в метрах. Аппроксимируем:
                // Time — «на сколько секунд вперёд» предсказываем позицию цели (переиспользуем
                // LookAheadFactor как есть — тот же порядок величин 0..1, что и раньше);
                // Smoothing — сглаживание предсказания, переиспользуем MaxLookAhead как есть.
                // Это приближение, а не точный перенос старой формулы.
                _composer.Lookahead.Enabled = _config.LookAheadFactor > 0f;
                _composer.Lookahead.Time = Mathf.Clamp01(_config.LookAheadFactor);
                _composer.Lookahead.Smoothing = Mathf.Clamp(_config.MaxLookAhead, 0f, MaxLookAheadSmoothing);
            }

            // MaxLagDistance не мигрирован: в damping-модели Cinemachine нет жёсткого клэмпа
            // отставания камеры по метрам (только скорость сходления) — прямого аналога нет.
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

            // Приоритет — единственный переключатель между chase/orbit; Brain сам блендит
            // по приоритету, компонент camera/adapter'а не отключаем (enabled не трогаем).
            _camera.Priority = new PrioritySettings { Enabled = true, Value = active ? CameraPriorities.Active : CameraPriorities.Inactive };
        }
    }
}
