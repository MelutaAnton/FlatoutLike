using UnityEngine;

namespace ithappy.Battle_Stadium {
    public class MoveAnimation : AnimationNode {
        [SerializeField]
        private Transform m_StartPoint;
        [SerializeField]
        private Transform m_EndPoint;
        [SerializeField]
        private AnimationCurve m_Curve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        private Transform m_Transform;

        private void Awake() {
            m_Transform = transform;
        }

        protected override void AnimateFrame(float time) {
            var t = m_Curve.Evaluate(time);

            var position = Vector3.Lerp(m_StartPoint.position, m_EndPoint.position, t);
            var rotation = Quaternion.Lerp(m_StartPoint.rotation, m_EndPoint.rotation, t);

            m_Transform.SetPositionAndRotation(position, rotation);
        }
    }
}