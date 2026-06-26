using System;
using UnityEngine;

namespace FlatOutLike.Controllers.Vfx
{
    // EDITOR: This prefab requires ParticleSystem.Stop Action = Callback.
    // Set via SetupVfxPrefabsEditor to avoid losing the setting on prefab reimport.
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

        // Called by Unity when ParticleSystem.stopAction == Callback
        private void OnParticleSystemStopped()
        {
            var callback = _onFinished;
            _onFinished = null;
            callback?.Invoke(this);
        }
    }
}
