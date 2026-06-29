using System.Collections.Generic;
using FlatOutLike.Controllers.Vfx;
using FlatOutLike.Core.Pool;
using FlatOutLike.Data.Configs.Vfx;
using FlatOutLike.Models;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace FlatOutLike.Factories.Vfx
{
    /// <summary>
    /// Фабрика VFX-эффектов. Внутренние пулы делегированы в <see cref="IPoolManager"/>;
    /// контракт <see cref="IVfxFactory"/> не изменился — damage-VFX цепочка работает прежде.
    /// </summary>
    public sealed class VfxFactory : IVfxFactory
    {
        private readonly IObjectResolver _resolver;
        private readonly VfxConfig       _config;
        private readonly IPoolManager    _pool;

        // Маппинг type → prefab; нужен чтобы Get знал, какой prefab передать IPoolManager.
        private readonly Dictionary<VfxEffectType, GameObject> _prefabs
            = new Dictionary<VfxEffectType, GameObject>();

        public VfxFactory(IObjectResolver resolver, VfxConfig config, IPoolManager pool)
        {
            _resolver = resolver;
            _config   = config;
            _pool     = pool;
            Prewarm();
        }

        private void Prewarm()
        {
            if (_config.Entries == null) return;

            foreach (var entry in _config.Entries)
            {
                if (entry.prefab == null) continue;

                _prefabs[entry.type] = entry.prefab;
                _pool.Prewarm(entry.prefab, entry.poolSize);
            }
        }

        /// <summary>
        /// Берёт VFX-объект из пула, инжектирует зависимости, запускает эффект.
        /// После окончания <see cref="ParticleController"/> автоматически возвращает объект в пул.
        /// </summary>
        public ParticleController Spawn(VfxEffectType type, Vector3 pos, Quaternion rot)
        {
            if (!_prefabs.TryGetValue(type, out var prefab))
            {
                Debug.LogWarning($"[VfxFactory] Нет префаба для {type}. Проверьте VfxConfig.");
                return null;
            }

            var go = _pool.Get(prefab);

            // Каждый раз инжектируем — после возврата в пул зависимости могут измениться
            // (теоретически). На практике это no-op после первого Construct,
            // но гарантирует корректность при переиспользовании.
            _resolver.InjectGameObject(go);

            var pc = go.GetComponent<ParticleController>();
            if (pc == null)
            {
                Debug.LogWarning($"[VfxFactory] Префаб {prefab.name} не содержит ParticleController. Объект возвращён в пул.");
                _pool.Return(go);
                return null;
            }

            // Колбэк возвращает объект в пул через IPoolManager — никаких прямых манипуляций с очередью.
            pc.Launch(pos, rot, c => _pool.Return(c.gameObject));
            return pc;
        }
    }
}
