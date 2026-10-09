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

        /// <summary>
        /// Возвращает активный инстанс, поставленный в <paramref name="position"/> / <paramref name="rotation"/>.
        /// Из очереди: поза ставится, пока объект выключен, активация — последней.
        /// Свежий инстанс: создаётся сразу в заданной позе (Awake/OnEnable первичной активации
        /// префаба проходят уже там), затем выключается и активируется повторно — Rigidbody
        /// и коллайдеры никогда не видят позу корня префаба.
        /// </summary>
        public GameObject Get(Vector3 position, Quaternion rotation)
        {
            GameObject go;
            if (_queue.Count > 0)
            {
                go = _queue.Dequeue();
                go.transform.SetPositionAndRotation(position, rotation);
            }
            else
            {
                go = CreateInstance(position, rotation);
            }

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

        /// <summary>Инстанцирует новый неактивный объект в позе корня префаба.</summary>
        private GameObject CreateInstance()
        {
            var prefabTransform = _prefab.transform;
            return CreateInstance(prefabTransform.position, prefabTransform.rotation);
        }

        /// <summary>
        /// Инстанцирует новый неактивный объект с маркером <see cref="PooledInstance"/> сразу
        /// в заданной позе: Instantiate с позой — Awake/OnEnable и создание физики идут в ней.
        /// </summary>
        private GameObject CreateInstance(Vector3 position, Quaternion rotation)
        {
            var go = Object.Instantiate(_prefab, position, rotation);
            go.SetActive(false);

            // Не использовать ?? с Unity-объектами: fake-null обходит перегруженный ==.
            var marker = go.GetComponent<PooledInstance>();
            if (marker == null)
                marker = go.AddComponent<PooledInstance>();
            marker.SourcePrefab = _prefab;

            return go;
        }
    }
}
