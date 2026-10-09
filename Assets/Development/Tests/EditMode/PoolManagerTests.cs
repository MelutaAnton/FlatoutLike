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

        // ── Тест 5: Get(prefab, pos, rot) выдаёт активный инстанс в заданной позе ─

        [Test]
        public void GetWithPose_ReturnsActiveInstance_AtGivenPositionAndRotation()
        {
            var prefab   = CreatePrefab();
            var position = new Vector3(10f, 2f, -5f);
            var rotation = Quaternion.Euler(0f, 90f, 0f);

            var result = _pool.Get(prefab, position, rotation);

            Assert.IsNotNull(result, "Get с позой должен вернуть инстанс, не null.");
            Assert.AreNotSame(prefab, result, "Get с позой должен вернуть копию prefab, не сам prefab.");
            Assert.IsTrue(result.activeSelf, "Выданный инстанс должен быть активным.");
            AssertPose(result, position, rotation);
        }

        // ── Тест 6: после Return повторный Get с позой переставляет тот же инстанс ─

        [Test]
        public void GetWithPose_AfterReturn_ReusesSameInstance_AtNewPose()
        {
            var prefab = CreatePrefab();

            var first = _pool.Get(prefab, new Vector3(1f, 0f, 1f), Quaternion.identity);
            _pool.Return(first);

            var position2 = new Vector3(-20f, 3f, 7f);
            var rotation2 = Quaternion.Euler(0f, -45f, 0f);
            var second    = _pool.Get(prefab, position2, rotation2);

            Assert.AreSame(first, second,
                "После Return повторный Get с позой должен вернуть тот же инстанс.");
            Assert.IsTrue(second.activeSelf, "Переиспользованный инстанс должен быть активным.");
            AssertPose(second, position2, rotation2);
        }

        // ── Тест 7: Get с позой и null-prefab возвращает null без исключения ─────

        [Test]
        public void GetWithPose_NullPrefab_ReturnsNull_WithoutThrowing()
        {
            GameObject result = null;

            Assert.DoesNotThrow(() => result = _pool.Get(null, Vector3.one, Quaternion.identity),
                "Get с null-prefab не должен выбрасывать исключение.");
            Assert.IsNull(result, "Get с null-prefab должен вернуть null.");
        }

        // ── Тест 8: у свежего инстанса ПЕРВЫЙ OnEnable (при Instantiate) уже в целевой позе ─

        [Test]
        public void GetWithPose_FreshInstance_FirstOnEnableSeesTargetPosition()
        {
            // Префаб стоит в нуле, цель — не в нуле: до фикса первый OnEnable видел бы ноль.
            var prefab = CreatePrefab();
            prefab.AddComponent<PoolPoseProbe>();
            var position = new Vector3(10f, 2f, -5f);

            var result = _pool.Get(prefab, position, Quaternion.identity);
            var probe  = result.GetComponent<PoolPoseProbe>();

            Assert.IsNotNull(probe, "Зонд должен скопироваться в инстанс вместе с префабом.");
            Assert.Greater(probe.EnableCount, 0, "OnEnable зонда должен был сработать.");
            Assert.That(Vector3.Distance(probe.FirstEnablePosition, position), Is.LessThan(1e-4f),
                $"Первый OnEnable свежего инстанса увидел {probe.FirstEnablePosition}, ожидалось {position}.");
            Assert.That(Vector3.Distance(probe.LastEnablePosition, position), Is.LessThan(1e-4f),
                $"Последний OnEnable свежего инстанса увидел {probe.LastEnablePosition}, ожидалось {position}.");
        }

        // ── Тест 9: инстанс из очереди видит целевую позицию в последнем OnEnable ─

        [Test]
        public void GetWithPose_InstanceFromQueue_LastOnEnableSeesTargetPosition()
        {
            var prefab = CreatePrefab();
            prefab.AddComponent<PoolPoseProbe>();

            var first = _pool.Get(prefab, new Vector3(1f, 0f, 1f), Quaternion.identity);
            _pool.Return(first);
            int enableCountBefore = first.GetComponent<PoolPoseProbe>().EnableCount;

            var position2 = new Vector3(-20f, 3f, 7f);
            var second    = _pool.Get(prefab, position2, Quaternion.identity);
            var probe     = second.GetComponent<PoolPoseProbe>();

            Assert.AreSame(first, second, "Инстанс должен прийти из очереди.");
            Assert.Greater(probe.EnableCount, enableCountBefore,
                "Повторная активация из очереди должна вызвать OnEnable.");
            Assert.That(Vector3.Distance(probe.LastEnablePosition, position2), Is.LessThan(1e-4f),
                $"Последний OnEnable инстанса из очереди увидел {probe.LastEnablePosition}, ожидалось {position2}.");
        }

        /// <summary>Сверяет позицию и поворот инстанса с допуском на погрешность float.</summary>
        private static void AssertPose(GameObject go, Vector3 position, Quaternion rotation)
        {
            Assert.That(Vector3.Distance(go.transform.position, position), Is.LessThan(1e-4f),
                $"Позиция инстанса {go.transform.position} не совпадает с ожидаемой {position}.");
            Assert.That(Quaternion.Angle(go.transform.rotation, rotation), Is.LessThan(1e-2f),
                $"Поворот инстанса {go.transform.rotation.eulerAngles} не совпадает с ожидаемым {rotation.eulerAngles}.");
        }
    }

    /// <summary>
    /// Тест-зонд: запоминает позицию transform в первом и в последнем OnEnable.
    /// [ExecuteAlways] — иначе в EditMode OnEnable у MonoBehaviour не вызывается.
    /// </summary>
    [ExecuteAlways]
    public class PoolPoseProbe : MonoBehaviour
    {
        public Vector3 FirstEnablePosition { get; private set; }
        public Vector3 LastEnablePosition { get; private set; }
        public int EnableCount { get; private set; }

        private void OnEnable()
        {
            if (EnableCount == 0)
                FirstEnablePosition = transform.position;

            LastEnablePosition = transform.position;
            EnableCount++;
        }
    }
}
