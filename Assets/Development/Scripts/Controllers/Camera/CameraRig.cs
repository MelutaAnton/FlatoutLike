using UnityEngine;

namespace FlatOutLike.Controllers.Camera
{
    /// <summary>
    /// Единственная точка переключения активной камеры арены между chase (следование за машиной)
    /// и orbit (вокруг катапультированного водителя). Активирует одно поведение, выключает другое.
    /// </summary>
    public sealed class CameraRig : MonoBehaviour, ICameraRig
    {
        [SerializeField] private CinemachineChaseCameraBehaviour _chase;
        [SerializeField] private CinemachineOrbitDriverCameraBehaviour _orbit;

        public void ActivateChase(Transform target)
        {
            if (_chase != null)
            {
                _chase.SetTarget(target);
                _chase.SetBehaviourActive(true);
            }

            if (_orbit != null)
                _orbit.SetBehaviourActive(false);
        }

        public void ActivateOrbit(Transform target)
        {
            if (_orbit != null)
            {
                _orbit.SetTarget(target);
                _orbit.SetBehaviourActive(true);
            }

            if (_chase != null)
                _chase.SetBehaviourActive(false);
        }
    }
}
