using System;
using UnityEngine;

namespace FlatOutLike.Controllers.Vfx
{
    // EDITOR: префабу требуется ParticleSystem.Stop Action = Callback.
    // Выставляется через SetupVfxPrefabsEditor, чтобы не потерять настройку при реимпорте префаба.
    public sealed class ParticleController : MonoBehaviour
    {
        [SerializeField] private ParticleSystem _particleSystem;

        private Action<ParticleController> _onFinished;

        public void Launch(Vector3 pos, Quaternion rot, Action<ParticleController> onFinished)
        {
            if (_particleSystem == null)
            {
                Debug.LogWarning($"[ParticleController] {name} has no ParticleSystem assigned. Returning to pool immediately.");
                onFinished?.Invoke(this);
                return;
            }

            transform.SetPositionAndRotation(pos, rot);
            _onFinished = onFinished;
            _particleSystem.Play();
        }

        // Вызывается Unity, когда ParticleSystem.stopAction == Callback
        private void OnParticleSystemStopped()
        {
            var callback = _onFinished;
            _onFinished = null;
            callback?.Invoke(this);
        }
    }
}
