using FlatOutLike.Core.Events;
using FlatOutLike.Data.Configs.Driver;
using FlatOutLike.Services.Driver;
using NUnit.Framework;
using UnityEngine;

namespace FlatOutLike.Tests
{
    /// <summary>
    /// Юнит-тесты <see cref="DriverFlightService"/>: лимит бустов, штраф скорости
    /// steer-коррекции, отслеживание пика высоты и одноразовость NotifyLanded.
    /// Использует настоящий <see cref="EventBus"/> и настоящий <see cref="FlipDetectorService"/> —
    /// оба чистый C#, тестируемы без Unity Runtime.
    /// </summary>
    [TestFixture]
    public class DriverFlightServiceTests
    {
        private EventBus _bus;
        private DriverFlightConfig _config;
        private FlipDetectorService _flipDetector;
        private DriverFlightService _service;

        [SetUp]
        public void SetUp()
        {
            _bus = new EventBus();
            _config = ScriptableObject.CreateInstance<DriverFlightConfig>();
            _flipDetector = new FlipDetectorService(_config, _bus);
            _service = new DriverFlightService(_config, _flipDetector, _bus);
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_config);
        }

        // a) Буст доступен ровно BoostCount раз (по умолчанию — 1) за полёт
        [Test]
        public void TryConsumeBoost_SucceedsExactlyBoostCountTimes()
        {
            _service.BeginFlight(Vector3.zero);

            Assert.IsTrue(_service.TryConsumeBoost(), "Первый буст в пределах лимита должен пройти.");
            Assert.IsFalse(_service.TryConsumeBoost(), "Второй буст сверх BoostCount=1 должен быть отклонён.");
        }

        // b) Новый полёт (BeginFlight) сбрасывает счётчик бустов
        [Test]
        public void BeginFlight_ResetsBoostCounter()
        {
            _service.BeginFlight(Vector3.zero);
            _service.TryConsumeBoost();

            _service.BeginFlight(Vector3.zero);

            Assert.IsTrue(_service.TryConsumeBoost(), "После BeginFlight лимит бустов должен обнулиться.");
        }

        // c) Look == 0 -> нулевая коррекция, штрафа нет
        [Test]
        public void CalculateSteerAcceleration_WithZeroLook_ReturnsZero()
        {
            Vector3 result = _service.CalculateSteerAcceleration(Vector2.zero, new Vector3(0f, 0f, 10f));

            Assert.AreEqual(Vector3.zero, result);
        }

        // d) Look != 0 -> присутствует штраф торможения вдоль текущей скорости
        [Test]
        public void CalculateSteerAcceleration_WithNonZeroLook_IncludesSpeedPenaltyAlongVelocity()
        {
            Vector3 velocity = new Vector3(0f, 0f, 10f);
            Vector2 look = new Vector2(1f, 0f);

            Vector3 result = _service.CalculateSteerAcceleration(look, velocity);

            // Компонента результата вдоль -velocity (штраф торможения) должна быть положительной,
            // т.е. вектор результата действительно тормозит машину вдоль хода скорости.
            float brakingComponent = Vector3.Dot(result, -velocity.normalized);
            Assert.Greater(brakingComponent, 0f,
                "При активной steer-коррекции (look != 0) должен присутствовать штраф торможения.");
        }

        // e) PeakHeight считается относительно startPosition.y
        [Test]
        public void Track_UpdatesPeakHeight_RelativeToStartPosition()
        {
            _service.BeginFlight(new Vector3(0f, 5f, 0f));

            _service.Track(new Vector3(0f, 8f, 0f), 0.1f);  // высота +3
            _service.Track(new Vector3(0f, 12f, 0f), 0.1f); // высота +7 — новый пик
            _service.Track(new Vector3(0f, 9f, 0f), 0.1f);  // высота +4 — пик не обновляется

            Assert.AreEqual(7f, _service.PeakHeight, 0.001f);
        }

        // f) NotifyLanded одноразовый и публикует DriverLandedEvent с корректными данными
        [Test]
        public void NotifyLanded_PublishesEventOnce_WithCollectedData()
        {
            var landedEvents = new System.Collections.Generic.List<DriverLandedEvent>();
            _bus.Subscribe<DriverLandedEvent>(landedEvents.Add);

            _service.BeginFlight(new Vector3(0f, 0f, 0f));
            _service.Track(new Vector3(0f, 6f, 0f), 0.1f);
            _service.NotifyLanded();
            _service.NotifyLanded(); // повторный вызов — должен быть проигнорирован

            Assert.AreEqual(1, landedEvents.Count, "DriverLandedEvent должен публиковаться ровно один раз.");
            Assert.AreEqual(6f, landedEvents[0].PeakHeight, 0.001f);
            Assert.IsFalse(_service.IsInFlight, "После NotifyLanded полёт должен считаться завершённым.");

            _bus.Unsubscribe<DriverLandedEvent>(landedEvents.Add);
        }
    }
}
