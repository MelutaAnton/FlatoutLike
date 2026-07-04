using FlatOutLike.Data.Configs.Camera;
using UnityEngine;

namespace FlatOutLike.Controllers.Camera
{
    public sealed class ChaseCarCameraBehaviour : MonoBehaviour, ICameraBehaviour
    {
        [SerializeField] private Transform _target;
        [SerializeField] private ChaseCameraConfig _config;

        private Vector3 _velocity;
        private Rigidbody _targetRigidbody;
        // Полное имя UnityEngine.Camera: короткое `Camera` в этом файле резолвится
        // в namespace FlatOutLike.Controllers.Camera (CS0118).
        private UnityEngine.Camera _camera;

        private void Awake()
        {
            if (_target != null)
                _targetRigidbody = _target.GetComponentInParent<Rigidbody>();

            // Компонент камеры кэшируем один раз — behaviour живёт на объекте камеры,
            // но GetComponent может вернуть null (fake-null Unity), поэтому проверяем явно.
            _camera = GetComponent<UnityEngine.Camera>();
        }

        public void SetTarget(Transform target)
        {
            _target = target;
            _targetRigidbody = target != null ? target.GetComponentInParent<Rigidbody>() : null;
        }

        public void SetBehaviourActive(bool active) => enabled = active;

        private void LateUpdate()
        {
            if (_target == null || _config == null) return;

            // Позиция камеры БЕЗ look-ahead: на скорости look-ahead в позиции съедал
            // FollowDistance и камера утыкалась в корму машины (баг «прилипания»).
            Vector3 desired = _target.position
                              - _target.forward * _config.FollowDistance
                              + Vector3.up * _config.Height;

            transform.position = Vector3.SmoothDamp(
                transform.position, desired, ref _velocity, _config.PositionSmoothing);

            // Look-ahead применяем только к точке взгляда — камера не приближается
            // к машине на скорости, а лишь смотрит чуть дальше по ходу движения.
            Vector3 velocity = _targetRigidbody != null ? _targetRigidbody.linearVelocity : Vector3.zero;
            Vector3 lookAhead = Vector3.ClampMagnitude(velocity * _config.LookAheadFactor, _config.MaxLookAhead);
            Vector3 lookAt = _target.position + Vector3.up * _config.LookAtHeightOffset + lookAhead;

            Vector3 toLookAt = lookAt - transform.position;
            if (toLookAt.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(toLookAt);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation, targetRot, _config.RotationSmoothing * Time.deltaTime);
            }

            // FOV задаётся конфигом; 0 означает «не трогать камеру».
            if (_camera != null && _config.FieldOfView > 0f)
                _camera.fieldOfView = _config.FieldOfView;
        }
    }
}
