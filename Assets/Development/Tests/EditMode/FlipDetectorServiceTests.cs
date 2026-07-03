using System.Collections.Generic;
using FlatOutLike.Core.Events;
using FlatOutLike.Data.Configs.Driver;
using FlatOutLike.Models;
using FlatOutLike.Services.Driver;
using NUnit.Framework;
using UnityEngine;

namespace FlatOutLike.Tests
{
    /// <summary>
    /// Юнит-тесты <see cref="FlipDetectorService"/>: накопление вращения по оси выше/ниже
    /// порога <c>RotationBonusThreshold</c> и сброс состояния через Reset.
    /// </summary>
    [TestFixture]
    public class FlipDetectorServiceTests
    {
        private EventBus _bus;
        private DriverFlightConfig _config;
        private FlipDetectorService _service;
        private readonly List<FlipDetectedEvent> _flipEvents = new List<FlipDetectedEvent>();

        [SetUp]
        public void SetUp()
        {
            _bus = new EventBus();
            _config = ScriptableObject.CreateInstance<DriverFlightConfig>(); // RotationBonusThreshold по умолчанию = 180 град/с
            _service = new FlipDetectorService(_config, _bus);

            _flipEvents.Clear();
            _bus.Subscribe<FlipDetectedEvent>(_flipEvents.Add);
        }

        [TearDown]
        public void TearDown()
        {
            _bus.Unsubscribe<FlipDetectedEvent>(_flipEvents.Add);
            UnityEngine.Object.DestroyImmediate(_config);
        }

        // a) Серия вращений 360° по оси Yaw на скорости выше порога -> FlipCount == 1 + событие
        [Test]
        public void Feed_FullRotationAboveThreshold_CountsOneFlip()
        {
            const float dt = 0.1f;
            const float stepDegrees = 30f; // скорость = 300 град/с > порога 180 град/с

            // Первый Feed только задаёт базовую ориентацию (дельты ещё нет).
            _service.Feed(Quaternion.identity, dt);

            // 12 шагов по 30° = 360° суммарно -> ровно один флип на последнем шаге.
            for (int i = 1; i <= 12; i++)
                _service.Feed(Quaternion.Euler(0f, stepDegrees * i, 0f), dt);

            Assert.AreEqual(1, _service.FlipCount);
            Assert.AreEqual(1, _flipEvents.Count);
            Assert.AreEqual(FlipAxis.Yaw, _flipEvents[0].Axis);
            Assert.AreEqual(300f, _flipEvents[0].SpeedDegPerSec, 0.5f);
        }

        // b) Та же суммарная амплитуда, но ниже порога скорости -> флип не засчитывается
        [Test]
        public void Feed_RotationBelowThreshold_DoesNotCountFlip()
        {
            const float dt = 0.1f;
            const float stepDegrees = 10f; // скорость = 100 град/с < порога 180 град/с

            _service.Feed(Quaternion.identity, dt);

            // 40 шагов по 10° — суммарно 400° "было бы" достаточно, но каждый шаг ниже порога.
            for (int i = 1; i <= 40; i++)
                _service.Feed(Quaternion.Euler(0f, stepDegrees * i, 0f), dt);

            Assert.AreEqual(0, _service.FlipCount);
            Assert.AreEqual(0, _flipEvents.Count);
        }

        // c) Reset обнуляет счётчик и накопленное вращение
        [Test]
        public void Reset_ClearsFlipCountAndAccumulatedRotation()
        {
            const float dt = 0.1f;
            const float stepDegrees = 30f;

            _service.Feed(Quaternion.identity, dt);
            for (int i = 1; i <= 12; i++)
                _service.Feed(Quaternion.Euler(0f, stepDegrees * i, 0f), dt);

            Assert.AreEqual(1, _service.FlipCount);

            _service.Reset();

            Assert.AreEqual(0, _service.FlipCount);

            // После Reset первый Feed снова только задаёт базовую ориентацию — новый флип
            // не должен засчитаться раньше, чем накопится собственные полные 360°.
            _flipEvents.Clear();
            _service.Feed(Quaternion.identity, dt);
            _service.Feed(Quaternion.Euler(0f, stepDegrees, 0f), dt);

            Assert.AreEqual(0, _service.FlipCount);
            Assert.AreEqual(0, _flipEvents.Count);
        }
    }
}
