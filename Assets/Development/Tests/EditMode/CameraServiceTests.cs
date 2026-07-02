using System.Collections.Generic;
using System.Reflection;
using FlatOutLike.Controllers.Camera;
using FlatOutLike.Controllers.Car;
using FlatOutLike.Controllers.Driver;
using FlatOutLike.Core.Events;
using FlatOutLike.Models;
using FlatOutLike.Services.Camera;
using FlatOutLike.Services.Match;
using NUnit.Framework;
using UnityEngine;

namespace FlatOutLike.Tests
{
    /// <summary>
    /// Юнит-тесты <see cref="CameraService"/>: маппинг FollowPlayerCar → ActivateChase
    /// и DriverEjectedEvent → ActivateOrbit только для машины игрока.
    ///
    /// Использует настоящий <see cref="CarRegistry"/> с реальными <see cref="CarController"/>
    /// на временных GameObject (AddComponent). Приватное поле _driver заполняется через
    /// reflection, т.к. в контракте CarController у него нет публичного сеттера
    /// (заполняется editor-скриптом на префабе).
    /// </summary>
    [TestFixture]
    public class CameraServiceTests
    {
        private EventBus _bus;
        private FakeCameraRig _rig;
        private CarRegistry _registry;
        private CameraService _service;

        private GameObject _playerGo;
        private GameObject _enemyGo;
        private GameObject _driverGo;
        private CarController _playerCar;
        private CarController _enemyCar;
        private DriverController _driverController;

        [SetUp]
        public void SetUp()
        {
            _bus = new EventBus();
            _rig = new FakeCameraRig();
            _registry = new CarRegistry();

            _playerGo = new GameObject("PlayerCar");
            _playerCar = _playerGo.AddComponent<CarController>();
            _playerCar.Configure(0, null);

            _driverGo = new GameObject("Driver");
            _driverController = _driverGo.AddComponent<DriverController>();
            SetPrivateField(_playerCar, "_driver", _driverController);

            _enemyGo = new GameObject("EnemyCar");
            _enemyCar = _enemyGo.AddComponent<CarController>();
            _enemyCar.Configure(1, null);

            _registry.Register(_playerCar, isPlayer: true);
            _registry.Register(_enemyCar, isPlayer: false);

            _service = new CameraService(_rig, _registry, _bus);
        }

        [TearDown]
        public void TearDown()
        {
            _service.Dispose();

            // EditMode: DestroyImmediate обязателен для GameObject, созданных в тесте.
            Object.DestroyImmediate(_playerGo);
            Object.DestroyImmediate(_enemyGo);
            Object.DestroyImmediate(_driverGo);
        }

        // ── Тесты ───────────────────────────────────────────────────────────────

        [Test]
        public void FollowPlayerCar_ActivatesChase_WithGivenTarget()
        {
            var targetGo = new GameObject("Target");

            _service.FollowPlayerCar(targetGo.transform);

            Assert.AreEqual(1, _rig.ChaseCalls.Count);
            Assert.AreEqual(targetGo.transform, _rig.ChaseCalls[0]);
            Assert.AreEqual(0, _rig.OrbitCalls.Count);

            Object.DestroyImmediate(targetGo);
        }

        [Test]
        public void DriverEjectedEvent_ForPlayer_ActivatesOrbit_OnDriverCameraTarget()
        {
            _bus.Publish(new DriverEjectedEvent(_playerCar.CarId, Vector3.zero, EjectReason.CarDestroyed));

            Assert.AreEqual(1, _rig.OrbitCalls.Count,
                "Катапультирование водителя игрока должно включить orbit-камеру.");
            Assert.AreEqual(_driverController.CameraTarget, _rig.OrbitCalls[0]);
        }

        [Test]
        public void DriverEjectedEvent_ForEnemy_DoesNotActivateOrbit()
        {
            _bus.Publish(new DriverEjectedEvent(_enemyCar.CarId, Vector3.zero, EjectReason.CarDestroyed));

            Assert.AreEqual(0, _rig.OrbitCalls.Count,
                "Катапультирование водителя врага не должно переключать камеру игрока.");
        }

        // ── Утилиты ─────────────────────────────────────────────────────────────

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Поле {fieldName} не найдено — контракт CarController изменился?");
            field.SetValue(target, value);
        }

        // ── Фейки ───────────────────────────────────────────────────────────────

        private sealed class FakeCameraRig : ICameraRig
        {
            public readonly List<Transform> ChaseCalls = new List<Transform>();
            public readonly List<Transform> OrbitCalls = new List<Transform>();

            public void ActivateChase(Transform target) => ChaseCalls.Add(target);
            public void ActivateOrbit(Transform target) => OrbitCalls.Add(target);
        }
    }
}
