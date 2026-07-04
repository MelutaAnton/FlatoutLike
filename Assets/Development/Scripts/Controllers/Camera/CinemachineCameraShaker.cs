using Unity.Cinemachine;
using UnityEngine;

namespace FlatOutLike.Controllers.Camera
{
    /// <summary>
    /// Реализация <see cref="ICameraShaker"/> поверх Cinemachine Impulse (CM 3.1).
    /// Висит на Main Camera (рядом с CinemachineBrain); <c>_impulseSource</c> назначается
    /// editor-скриптом (см. SetupCinemachineCameraRigEditor.EnsureImpulse).
    /// </summary>
    public sealed class CinemachineCameraShaker : MonoBehaviour, ICameraShaker
    {
        [SerializeField] private CinemachineImpulseSource _impulseSource;

        public void Shake(Vector3 position, float force)
        {
            if (_impulseSource == null) return;

            // Направление импульса — классический «удар вниз» (Vector3.down), сила — из
            // расчёта сервиса. Позиция передаётся явно (не transform.position риga),
            // чтобы дальние удары на арене затухали по DissipationDistance импульса.
            _impulseSource.GenerateImpulseAtPositionWithVelocity(position, Vector3.down * force);
        }
    }
}
