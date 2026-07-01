using System.Collections.Generic;
using FlatOutLike.Controllers.Destructible;
using FlatOutLike.Core.Pool;
using FlatOutLike.Data.Configs.Destructible;
using UnityEngine;

namespace FlatOutLike.Factories.Destructible
{
    /// <summary>
    /// Фабрика обломков. ЕДИНСТВЕННОЕ место доступа к <see cref="IPoolManager"/> для обломков.
    /// Зеркалит паттерн <c>VfxFactory</c>: Get из пула → Launch с колбэком возврата.
    /// Обломки не требуют DI-инъекции — InjectGameObject здесь не нужен.
    /// </summary>
    public sealed class DestructibleFactory : IDestructibleFactory
    {
        private readonly IPoolManager _pool;

        // Префабы, для которых пул уже прогрет — чтобы не прогревать повторно на каждый таран
        private readonly HashSet<GameObject> _prewarmed = new();

        // Обычный конструктор — VContainer разрешает параметры автоматически
        public DestructibleFactory(IPoolManager pool)
        {
            _pool = pool;
        }

        public void SpawnDebris(Vector3 origin, DestructibleConfig config)
        {
            if (config.DebrisPrefabs == null || config.DebrisPrefabs.Length == 0)
            {
                Debug.LogWarning("[DestructibleFactory] DebrisPrefabs в DestructibleConfig не заполнен. Обломки не созданы.");
                return;
            }

            var prefabs     = config.DebrisPrefabs;
            var prefabCount = prefabs.Length;

            // Ленивый прогрев пула один раз на каждый префаб обломка — убирает хитч
            // инстанцирования в момент первого тарана (размер берётся из конфига).
            foreach (var p in prefabs)
            {
                if (p != null && _prewarmed.Add(p))
                    _pool.Prewarm(p, config.DebrisPoolSize);
            }

            for (int i = 0; i < config.DebrisPieceCount; i++)
            {
                // Циклический выбор префаба по индексу
                var prefab = prefabs[i % prefabCount];
                if (prefab == null) continue;

                var go = _pool.Get(prefab);

                var debris = go.GetComponent<DebrisPieceController>();
                if (debris == null)
                {
                    Debug.LogWarning($"[DestructibleFactory] Префаб {prefab.name} не содержит DebrisPieceController. Объект возвращён в пул.");
                    _pool.Return(go);
                    continue;
                }

                // Случайное направление разлёта: произвольно по горизонтали, немного вверх
                var direction = new Vector3(
                    Random.Range(-1f, 1f),
                    Random.Range(0.2f, 1f),
                    Random.Range(-1f, 1f)
                );
                var impulse = direction.normalized * config.DebrisScatterForce;

                // Небольшое смещение спавна, чтобы обломки не накладывались в одной точке
                var offset = new Vector3(
                    Random.Range(-0.4f, 0.4f),
                    Random.Range(0f,    0.4f),
                    Random.Range(-0.4f, 0.4f)
                );

                // Колбэк возвращает именно тот gameObject, который был взят из пула
                debris.Launch(
                    origin + offset,
                    Quaternion.identity,
                    impulse,
                    config.DebrisLifetime,
                    piece => _pool.Return(piece.gameObject)
                );
            }
        }
    }
}
