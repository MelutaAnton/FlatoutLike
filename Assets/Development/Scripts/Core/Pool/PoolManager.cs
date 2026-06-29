using System.Collections.Generic;
using UnityEngine;

namespace FlatOutLike.Core.Pool
{
    /// <summary>
    /// Реализация <see cref="IPoolManager"/> на основе словаря prefab → пул.
    /// Чистый C# (pure C#), без MonoBehaviour.
    /// Инъекции зависимостей и вызов Launch — ответственность вызывающей фабрики.
    /// </summary>
    public sealed class PoolManager : IPoolManager
    {
        private readonly Dictionary<GameObject, GameObjectPool> _pools
            = new Dictionary<GameObject, GameObjectPool>();

        /// <inheritdoc/>
        public void Prewarm(GameObject prefab, int count)
        {
            if (prefab == null)
            {
                Debug.LogWarning("[PoolManager] Prewarm: prefab равен null. Пропуск.");
                return;
            }

            GetOrCreatePool(prefab).Prewarm(count);
        }

        /// <inheritdoc/>
        public GameObject Get(GameObject prefab)
        {
            if (prefab == null)
            {
                Debug.LogWarning("[PoolManager] Get: prefab равен null.");
                return null;
            }

            return GetOrCreatePool(prefab).Get();
        }

        /// <inheritdoc/>
        public void Return(GameObject instance)
        {
            if (instance == null) return;

            // Во всех ветках «не могу вернуть в пул» объект НЕ уничтожаем:
            // чужой объект принадлежит вызывающему (не нам его трогать), а Object.Destroy
            // к тому же запрещён в edit mode (ломал бы EditMode-тесты). Просто предупреждаем
            // и выходим — владение остаётся у вызывающего.
            var marker = instance.GetComponent<PooledInstance>();
            if (marker == null)
            {
                Debug.LogWarning($"[PoolManager] Объект '{instance.name}' возвращён без маркера PooledInstance — не принадлежит пулу, игнорируется.");
                return;
            }

            if (marker.SourcePrefab == null)
            {
                Debug.LogWarning($"[PoolManager] Маркер у '{instance.name}' повреждён (SourcePrefab == null), игнорируется.");
                return;
            }

            if (!_pools.TryGetValue(marker.SourcePrefab, out var pool))
            {
                Debug.LogWarning($"[PoolManager] Пул для '{marker.SourcePrefab.name}' не зарегистрирован, объект игнорируется.");
                return;
            }

            pool.Return(instance);
        }

        private GameObjectPool GetOrCreatePool(GameObject prefab)
        {
            if (!_pools.TryGetValue(prefab, out var pool))
            {
                pool = new GameObjectPool(prefab);
                _pools[prefab] = pool;
            }

            return pool;
        }
    }
}
