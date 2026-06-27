using System.Collections.Generic;
using FlatOutLike.Controllers.Vfx;
using FlatOutLike.Data.Configs.Vfx;
using FlatOutLike.Models;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace FlatOutLike.Factories.Vfx
{
    public sealed class VfxFactory : IVfxFactory
    {
        private readonly IObjectResolver _resolver;
        private readonly VfxConfig _config;

        private readonly Dictionary<VfxEffectType, Queue<ParticleController>> _pools
            = new Dictionary<VfxEffectType, Queue<ParticleController>>();
        private readonly Dictionary<VfxEffectType, GameObject> _prefabs
            = new Dictionary<VfxEffectType, GameObject>();
        // Отслеживает, к какому VfxEffectType принадлежит каждый контроллер, для корректного возврата в Pool.
        private readonly Dictionary<ParticleController, VfxEffectType> _controllerTypes
            = new Dictionary<ParticleController, VfxEffectType>();

        public VfxFactory(IObjectResolver resolver, VfxConfig config)
        {
            _resolver = resolver;
            _config = config;
            Prewarm();
        }

        private void Prewarm()
        {
            if (_config.Entries == null) return;

            foreach (var entry in _config.Entries)
            {
                if (entry.prefab == null) continue;

                _prefabs[entry.type] = entry.prefab;
                var queue = new Queue<ParticleController>();

                for (int i = 0; i < entry.poolSize; i++)
                {
                    var instance = CreateInstance(entry.prefab, entry.type);
                    if (instance != null)
                        queue.Enqueue(instance);
                }

                _pools[entry.type] = queue;
            }
        }

        public ParticleController Spawn(VfxEffectType type, Vector3 pos, Quaternion rot)
        {
            if (!_pools.TryGetValue(type, out var queue))
            {
                Debug.LogWarning($"[VfxFactory] No pool registered for {type}. Check VfxConfig.");
                return null;
            }

            ParticleController controller = queue.Count > 0
                ? queue.Dequeue()
                : Instantiate(type);

            if (controller == null) return null;

            controller.gameObject.SetActive(true);
            _resolver.InjectGameObject(controller.gameObject);
            controller.Launch(pos, rot, ReturnToPool);
            return controller;
        }

        private ParticleController Instantiate(VfxEffectType type)
        {
            if (!_prefabs.TryGetValue(type, out var prefab) || prefab == null)
            {
                Debug.LogWarning($"[VfxFactory] No prefab registered for {type}.");
                return null;
            }
            return CreateInstance(prefab, type);
        }

        private ParticleController CreateInstance(GameObject prefab, VfxEffectType type)
        {
            var go = Object.Instantiate(prefab);
            go.SetActive(false);
            var pc = go.GetComponent<ParticleController>();
            if (pc == null)
            {
                Debug.LogWarning($"[VfxFactory] Prefab {prefab.name} is missing ParticleController component.");
                Object.Destroy(go);
                return null;
            }
            _controllerTypes[pc] = type;
            return pc;
        }

        private void ReturnToPool(ParticleController controller)
        {
            if (controller == null) return;

            controller.gameObject.SetActive(false);

            if (!_controllerTypes.TryGetValue(controller, out var type))
            {
                Object.Destroy(controller.gameObject);
                return;
            }

            if (!_pools.TryGetValue(type, out var queue))
            {
                queue = new Queue<ParticleController>();
                _pools[type] = queue;
            }
            queue.Enqueue(controller);
        }
    }
}
