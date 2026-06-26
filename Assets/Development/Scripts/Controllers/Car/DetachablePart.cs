using UnityEngine;

namespace FlatOutLike.Controllers.Car
{
    public sealed class DetachablePart : MonoBehaviour
    {
        [SerializeField] private Rigidbody _rigidbody;
        [SerializeField] private FixedJoint _joint;
        [SerializeField] private float _breakForce = 2000f;

        public void SetBreakable(bool breakable)
        {
            if (_joint == null) return;
            _joint.breakForce = breakable ? _breakForce : float.PositiveInfinity;
        }

        private void OnJointBreak(float force)
        {
            Debug.Log($"[DetachablePart] {name} detached at force {force:F0}");
        }
    }
}
