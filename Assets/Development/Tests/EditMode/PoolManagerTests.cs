using System.Collections.Generic;
using FlatOutLike.Core.Pool;
using NUnit.Framework;
using UnityEngine;

namespace FlatOutLike.Tests
{
    /// <summary>
    /// EditMode-тесты для <see cref="PoolManager"/>.
    /// Проверяют: инстанцирование при пустом пуле, переиспользование после Return,
    /// ограничение числа объектов при наличии преварма, устойчивость к чужим объектам.
    /// </summary>
    [TestFixture]
    public class PoolManagerTests
    {
        private PoolManager _pool;

        // Список «prefab»-GameObject'ов, созданных в тестах — удаляем в TearDown.
        private readonly List<GameObject> _testPrefabs = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            _pool = new PoolManager();
        }

        [TearDown]
        public void TearDown()
        {
            // Уничтожаем все инстансы пула по маркеру PooledInstance.
            // FindObjectsInactive.Include — иначе возвращённые/преварменные (неактивные)
            // инстансы не вычистятся и протекут в следующий тест, ломая счётчик в тесте 3.
            foreach (var pi in Object.FindObjectsByType<PooledInstance>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (pi != null)
                    Object.DestroyImmediate(pi.gameObject);
            }

            // Уничтожаем «prefab» GameObject'ы, созданные в тестах.
            foreach (var go in _testPrefabs)
                if (go != null) Object.DestroyImmediate(go);

            _testPrefabs.Clear();
        }

        /// <summary>Создаёт обычный GameObject как «prefab» и регистрирует его для TearDown.</summary>
        private GameObject CreatePrefab(string name = "TestPrefab")
        {
            var go = new GameObject(name);
            _testPrefabs.Add(go);
            return go;
        }

        // ── Тест 1: Get из пустого пула инстанцирует новый объект ───────────────

        [Test]
        public void Get_FromEmptyPool_InstantiatesNewObject()
        {
            var prefab = CreatePrefab();

            var result = _pool.Get(prefab);

            Assert.IsNotNull(result, "Get из пустого пула должен вернуть инстанс, не null.");
            Assert.AreNotSame(prefab, result, "Get должен вернуть копию prefab, не сам prefab.");
        }

        // ── Тест 2: Return + Get переиспользует тот же инстанс ──────────────────

        [Test]
        public void Return_Then_Get_ReusesSameInstance()
        {
            var prefab = CreatePrefab();

            var first = _pool.Get(prefab);
            _pool.Return(first);
            var second = _pool.Get(prefab);

            Assert.AreSame(first, second,
                "После Return повторный Get должен вернуть тот же инстанс, а не создать новый.");
        }

        // ── Тест 3: Get после Prewarm(n) не создаёт лишних инстансов ────────────

        [Test]
        public void Prewarm_N_Gets_DoNotInstantiateBeyondN_Before_Exhaustion()
        {
            var prefab = CreatePrefab();
            const int n = 2;

            _pool.Prewarm(prefab, n);

            // n Gets — все должны взять объект из пула, без новых Instantiate
            for (int i = 0; i < n; i++)
                _pool.Get(prefab);

            // Считаем все маркеры PooledInstance в сцене (активные + неактивные)
            int totalInstances = Object.FindObjectsByType<PooledInstance>(
                FindObjectsInactive.Include, FindObjectsSortMode.None).Length;

            Assert.AreEqual(n, totalInstances,
                $"После Prewarm({n}) и {n} Gets должно быть ровно {n} инстансов в сцене.");
        }

        // ── Тест 4: Return объекта без маркера PooledInstance не падает ──────────

        [Test]
        public void Return_ObjectWithoutPooledMarker_DoesNotThrow()
        {
            // Создаём «чужой» объект без маркера PooledInstance
            var alien = new GameObject("AlienObject");
            _testPrefabs.Add(alien); // чужой объект пул не трогает — убирается в TearDown

            // PoolManager.Return должен залогировать Warning и НЕ трогать чужой объект
            // (владение остаётся у вызывающего), не бросая исключений.
            Assert.DoesNotThrow(() => _pool.Return(alien),
                "Return объекта без маркера PooledInstance не должен выбрасывать исключение.");
        }
    }
}
