using System.Collections.Generic;
using UnityEngine;

namespace FlatOutLike.Core.Pool
{
    /// <summary>
    /// Пул одного prefab'а. Хранит неактивные инстансы в Queue.
    /// Get активирует объект, Return деактивирует и возвращает в очередь.
    /// </summary>
    public sealed class GameObjectPool
    {
        private readonly GameObject _prefab;
        private readonly Queue<GameObject> _queue = new Queue<GameObject>();

        public GameObjectPool(GameObject prefab)
        {
            _prefab = prefab;
        }

        /// <summary>Предварительно создаёт <paramref name="count"/> неактивных инстансов.</summary>
        public void Prewarm(int count)
        {
            for (int i = 0; i < count; i++)
                _queue.Enqueue(CreateInstance());
        }

        /// <summary>
        /// Возвращает активный инстанс из очереди. Если очередь пуста — инстанцирует новый.
        /// </summary>
        public GameObject Get()
        {
            var go = _queue.Count > 0 ? _queue.Dequeue() : CreateInstance();
            go.SetActive(true);
            return go;
        }

        /// <summary>Деактивирует инстанс и кладёт его обратно в очередь.</summary>
        public void Return(GameObject instance)
        {
            // Защита от повторного Return: уже неактивный инстанс считаем возвращённым.
            // Без неё двойной Return заэнкьюил бы дубликат, и Get выдал бы один объект дважды.
            if (!instance.activeSelf) return;

            instance.SetActive(false);
            _queue.Enqueue(instance);
        }

        /// <summary>Инстанцирует новый неактивный объект с маркером <see cref="PooledInstance"/>.</summary>
        private GameObject CreateInstance()
        {
            var go = Object.Instantiate(_prefab);
            go.SetActive(false);

            var marker = go.GetComponent<PooledInstance>() ?? go.AddComponent<PooledInstance>();
            marker.SourcePrefab = _prefab;

            return go;
        }
    }
}
