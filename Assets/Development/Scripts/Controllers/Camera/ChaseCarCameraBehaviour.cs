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

        private void Awake()
        {
            if (_target != null)
                _targetRigidbody = _target.GetComponentInParent<Rigidbody>();
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

            Vector3 lookAhead = Vector3.zero;
            if (_targetRigidbody != null)
                lookAhead = _targetRigidbody.linearVelocity * _config.LookAheadFactor;

            Vector3 desired = _target.position
                              - _target.forward * _config.FollowDistance
                              + Vector3.up * _config.Height
                              + lookAhead;

            transform.position = Vector3.SmoothDamp(
                transform.position, desired, ref _velocity, _config.PositionSmoothing);

            Vector3 toTarget = _target.position - transform.position;
            if (toTarget.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(toTarget);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation, targetRot, _config.RotationSmoothing * Time.deltaTime);
            }
        }
    }
}
