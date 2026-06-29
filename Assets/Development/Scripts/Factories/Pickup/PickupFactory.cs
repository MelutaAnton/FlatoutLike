using System;
using System.Collections.Generic;
using FlatOutLike.Controllers.Pickups;
using FlatOutLike.Core.Pool;
using FlatOutLike.Data.Configs.Pickups;
using FlatOutLike.Models;
using UnityEngine;
using VContainer;
using VContainer.Unity; // extension InjectGameObject(this IObjectResolver, GameObject)

namespace FlatOutLike.Factories.Pickup
{
    /// <summary>
    /// Управляет пулами пикапов. Прогревает их по данным <see cref="PickupConfig"/> в конструкторе.
    /// Паттерн полностью аналогичен <see cref="FlatOutLike.Factories.Vfx.VfxFactory"/>.
    /// </summary>
    public sealed class PickupFactory : IPickupFactory
    {
        private readonly IObjectResolver _resolver;
        private readonly IPoolManager    _pool;
        private readonly PickupConfig    _config;

        // Маппинг type → prefab; нужен чтобы Spawn знал, какой prefab передать IPoolManager.
        private readonly Dictionary<PickupType, GameObject> _prefabs
            = new Dictionary<PickupType, GameObject>();

        public PickupFactory(IObjectResolver resolver, IPoolManager pool, PickupConfig config)
        {
            _resolver = resolver;
            _pool     = pool;
            _config   = config;
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

        public IPickup Spawn(PickupType type, Vector3 position, Action<IPickup> onCollected)
        {
            if (!_prefabs.TryGetValue(type, out var prefab))
            {
                Debug.LogWarning($"[PickupFactory] Нет префаба для {type}. Проверьте PickupConfig.");
                return null;
            }

            var go = _pool.Get(prefab);
            go.transform.position = position;

            // Инжектируем зависимости каждый раз — после возврата в пул они обнуляются.
            _resolver.InjectGameObject(go);

            var pickup = go.GetComponent<IPickup>();
            if (pickup == null)
            {
                Debug.LogWarning($"[PickupFactory] Префаб {prefab.name} не содержит IPickup-компонента. Возвращён в пул.");
                _pool.Return(go);
                return null;
            }

            // Activate сбрасывает _collected и сохраняет колбэк для OnCollected-оповещения.
            pickup.Activate(onCollected);
            return pickup;
        }

        public void Despawn(IPickup pickup)
        {
            if (pickup is MonoBehaviour mb)
                _pool.Return(mb.gameObject);
        }
    }
}
