using FlatOutLike.Data.Configs.Camera;
using UnityEngine;

namespace FlatOutLike.Controllers.Camera
{
    /// <summary>
    /// Орбитальная камера вокруг катапультированного водителя. Угол непрерывно нарастает
    /// со скоростью <see cref="OrbitCameraConfig.OrbitSpeed"/> (град/сек), позиция сглаживается
    /// SmoothDamp, камера всегда смотрит на target.
    /// </summary>
    public sealed class OrbitDriverCameraBehaviour : MonoBehaviour, ICameraBehaviour
    {
        [SerializeField] private OrbitCameraConfig _config;

        private Transform _target;
        private float _angleDegrees;
        private Vector3 _velocity;

        public void SetTarget(Transform target)
        {
            _target = target;
            // Стартовый угол из конфига: с OrbitSpeed=0 даёт фиксированный ракурс
            // (180° = «сзади по ходу трассы», как chase при езде — HighJump-режим).
            if (_config != null)
                _angleDegrees = _config.StartAngleDegrees;
        }

        public void SetBehaviourActive(bool active) => enabled = active;

        private void Awake()
        {
            // По умолчанию выключена: активацию делает CameraRig через SetBehaviourActive,
            // чтобы chase и orbit не работали одновременно до первого переключения.
            enabled = false;
        }

        private void LateUpdate()
        {
            if (_target == null || _config == null) return;

            _angleDegrees += _config.OrbitSpeed * Time.deltaTime;
            float rad = _angleDegrees * Mathf.Deg2Rad;

            Vector3 offset = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad)) * _config.Distance
                            + Vector3.up * _config.Height;

            Vector3 desired = _target.position + offset;

            transform.position = Vector3.SmoothDamp(
                transform.position, desired, ref _velocity, _config.FollowDamping);

            transform.LookAt(_target.position);
        }
    }
}
