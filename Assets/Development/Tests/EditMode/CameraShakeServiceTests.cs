using System.Collections.Generic;
using FlatOutLike.Controllers.Camera;
using FlatOutLike.Controllers.Car;
using FlatOutLike.Core.Events;
using FlatOutLike.Data.Configs.Camera;
using FlatOutLike.Models;
using FlatOutLike.Services.Camera;
using FlatOutLike.Services.Match;
using NUnit.Framework;
using UnityEngine;

namespace FlatOutLike.Tests
{
    /// <summary>
    /// Юнит-тесты <see cref="CameraShakeService"/>: релевантность события игроку,
    /// порог MinImpactSpeed, расчёт силы и клампинг по MaxForce, отписка в Dispose.
    /// </summary>
    [TestFixture]
    public class CameraShakeServiceTests
    {
        private EventBus _bus;
        private FakeCameraShaker _shaker;
        private CarRegistry _registry;
        private CameraShakeConfig _config;
        private CameraShakeService _service;

        private readonly List<GameObject> _createdGOs = new List<GameObject>();
        private readonly List<ScriptableObject> _createdSOs = new List<ScriptableObject>();

        private CarController _playerCar;
        private CarController _enemyCar;
        private CarController _enemyCar2;

        [SetUp]
        public void SetUp()
        {
            _bus = new EventBus();
            _shaker = new FakeCameraShaker();
            _registry = new CarRegistry();

            // minImpactSpeed=4, speedToForce=0.1, maxForce=1.2 — удобные числа для расчёта в тестах.
            _config = MakeConfig(minSpeed: 4f, speedToForce: 0.1f, maxForce: 1.2f);

            _playerCar = MakeCar(0);
            _enemyCar = MakeCar(1);
            _enemyCar2 = MakeCar(2);

            _registry.Register(_playerCar, isPlayer: true);
            _registry.Register(_enemyCar, isPlayer: false);
            _registry.Register(_enemyCar2, isPlayer: false);

            _service = new CameraShakeService(_bus, _registry, _shaker, _config);
        }

        [TearDown]
        public void TearDown()
        {
            _service.Dispose();

            foreach (var go in _createdGOs)
                if (go != null) Object.DestroyImmediate(go);
            _createdGOs.Clear();

            foreach (var so in _createdSOs)
                if (so != null) Object.DestroyImmediate(so);
            _createdSOs.Clear();
        }

        // ── Тесты ───────────────────────────────────────────────────────────────

        [Test]
        public void CollisionAboveThreshold_InvolvingPlayer_ShakesWithExpectedForce()
        {
            var point = new Vector3(1f, 0f, 2f);

            // Враг таранит игрока: ImpactSpeed=10, excess=10-4=6, force=6*0.1=0.6.
            _bus.Publish(new CarCollisionEvent(_enemyCar.CarId, _playerCar.CarId, 10f, point));

            Assert.AreEqual(1, _shaker.Calls.Count, "Столкновение с игроком выше порога должно вызвать тряску.");
            Assert.AreEqual(point, _shaker.Calls[0].Position);
            Assert.AreEqual(0.6f, _shaker.Calls[0].Force, 0.001f);
        }

        [Test]
        public void CollisionBelowThreshold_DoesNotShake()
        {
            // ImpactSpeed=3 < MinImpactSpeed=4 — ниже порога.
            _bus.Publish(new CarCollisionEvent(_playerCar.CarId, _enemyCar.CarId, 3f, Vector3.zero));

            Assert.AreEqual(0, _shaker.Calls.Count, "Удар ниже MinImpactSpeed не должен трясти камеру.");
        }

        [Test]
        public void CollisionExactlyAtThreshold_DoesNotShake()
        {
            // ImpactSpeed == MinImpactSpeed → сила ровно 0 — вызова шейкера быть не должно.
            _bus.Publish(new CarCollisionEvent(_playerCar.CarId, _enemyCar.CarId, 4f, Vector3.zero));

            Assert.AreEqual(0, _shaker.Calls.Count, "Удар ровно на пороге даёт нулевую силу — тряски нет.");
        }

        [Test]
        public void CollisionWithoutPlayer_DoesNotShake()
        {
            // Столкновение двух врагов — игрок не участвует.
            _bus.Publish(new CarCollisionEvent(_enemyCar.CarId, _enemyCar2.CarId, 20f, Vector3.zero));

            Assert.AreEqual(0, _shaker.Calls.Count, "Столкновение без участия игрока не должно трясти камеру.");
        }

        [Test]
        public void Force_IsClampedToMaxForce()
        {
            // ImpactSpeed=50, excess=46, raw force=46*0.1=4.6 >> MaxForce=1.2.
            _bus.Publish(new CarCollisionEvent(_playerCar.CarId, _enemyCar.CarId, 50f, Vector3.zero));

            Assert.AreEqual(1, _shaker.Calls.Count);
            Assert.AreEqual(1.2f, _shaker.Calls[0].Force, 0.001f, "Сила импульса должна клампиться по MaxForce.");
        }

        [Test]
        public void Dispose_UnsubscribesFromEventBus()
        {
            _service.Dispose();

            _bus.Publish(new CarCollisionEvent(_enemyCar.CarId, _playerCar.CarId, 10f, Vector3.zero));

            Assert.AreEqual(0, _shaker.Calls.Count, "После Dispose сервис не должен реагировать на события.");
        }

        // ── Хелперы ─────────────────────────────────────────────────────────────

        private CameraShakeConfig MakeConfig(float minSpeed, float speedToForce, float maxForce)
        {
            var cfg = ScriptableObject.CreateInstance<CameraShakeConfig>();
            _createdSOs.Add(cfg);

            var so = new UnityEditor.SerializedObject(cfg);
            so.FindProperty("_minImpactSpeed").floatValue = minSpeed;
            so.FindProperty("_speedToForce").floatValue = speedToForce;
            so.FindProperty("_maxForce").floatValue = maxForce;
            so.ApplyModifiedPropertiesWithoutUndo();

            return cfg;
        }

        private CarController MakeCar(int carId)
        {
            var go = new GameObject($"Car_{carId}");
            _createdGOs.Add(go);

            var car = go.AddComponent<CarController>();
            car.Configure(carId, null);
            return car;
        }

        // ── Фейки ───────────────────────────────────────────────────────────────

        private sealed class FakeCameraShaker : ICameraShaker
        {
            public readonly List<(Vector3 Position, float Force)> Calls = new List<(Vector3, float)>();

            public void Shake(Vector3 position, float force) => Calls.Add((position, force));
        }
    }
}
