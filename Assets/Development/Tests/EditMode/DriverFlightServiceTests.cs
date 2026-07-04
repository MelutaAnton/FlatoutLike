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

            // Скорость здесь не важна для теста высоты/пика — суммарный airTime (0.3с) меньше
            // MinAirTime по умолчанию (0.5с), settle-логика внутри Track ещё не активна.
            _service.Track(new Vector3(0f, 8f, 0f), Vector3.zero, 0.1f);  // высота +3
            _service.Track(new Vector3(0f, 12f, 0f), Vector3.zero, 0.1f); // высота +7 — новый пик
            _service.Track(new Vector3(0f, 9f, 0f), Vector3.zero, 0.1f);  // высота +4 — пик не обновляется

            Assert.AreEqual(7f, _service.PeakHeight, 0.001f);
        }

        // f) NotifyLanded одноразовый и публикует DriverLandedEvent с корректными данными
        [Test]
        public void NotifyLanded_PublishesEventOnce_WithCollectedData()
        {
            var landedEvents = new System.Collections.Generic.List<DriverLandedEvent>();
            _bus.Subscribe<DriverLandedEvent>(landedEvents.Add);

            _service.BeginFlight(new Vector3(0f, 0f, 0f));
            _service.Track(new Vector3(0f, 6f, 0f), Vector3.zero, 0.1f);
            _service.NotifyLanded();
            _service.NotifyLanded(); // повторный вызов — должен быть проигнорирован

            Assert.AreEqual(1, landedEvents.Count, "DriverLandedEvent должен публиковаться ровно один раз.");
            Assert.AreEqual(6f, landedEvents[0].PeakHeight, 0.001f);
            Assert.IsFalse(_service.IsInFlight, "После NotifyLanded полёт должен считаться завершённым.");

            _bus.Unsubscribe<DriverLandedEvent>(landedEvents.Add);
        }

        // g) Settle-приземление: после MinAirTime непрерывное затишье скорости в течение
        // LandingSettleDuration секунд подряд завершает полёт и публикует событие ровно один раз.
        [Test]
        public void Track_SettlesLanding_AfterMinAirTimeAndContinuousLowSpeed()
        {
            var landedEvents = new System.Collections.Generic.List<DriverLandedEvent>();
            _bus.Subscribe<DriverLandedEvent>(landedEvents.Add);

            _service.BeginFlight(Vector3.zero);

            // airTime 0.6 > MinAirTime(0.5) — settle начинает копиться, но ещё не достиг 1с.
            _service.Track(new Vector3(0f, 1f, 0f), Vector3.zero, 0.6f);
            Assert.IsTrue(_service.IsInFlight, "Settle ещё не достиг LandingSettleDuration.");

            // settle 0.6 + 0.5 = 1.1 >= LandingSettleDuration(1) — приземление.
            _service.Track(new Vector3(0f, 1f, 0f), Vector3.zero, 0.5f);

            Assert.IsFalse(_service.IsInFlight, "После накопления settle-таймера полёт должен завершиться.");
            Assert.AreEqual(1, landedEvents.Count, "DriverLandedEvent должен быть опубликован ровно один раз.");

            // Продолжаем звать Track после приземления — событие не должно публиковаться повторно.
            _service.Track(new Vector3(0f, 1f, 0f), Vector3.zero, 0.5f);
            Assert.AreEqual(1, landedEvents.Count, "Повторные вызовы Track после приземления не должны публиковать событие снова.");

            _bus.Unsubscribe<DriverLandedEvent>(landedEvents.Add);
        }

        // h) Скорость выше LandingSpeedThreshold между низкоскоростными вызовами сбрасывает
        // settle-таймер — суммарный прогресс не накапливается через "разрыв".
        [Test]
        public void Track_ResetsSettleTimer_WhenSpeedExceedsThresholdBetweenLowSpeedCalls()
        {
            var landedEvents = new System.Collections.Generic.List<DriverLandedEvent>();
            _bus.Subscribe<DriverLandedEvent>(landedEvents.Add);

            _service.BeginFlight(Vector3.zero);

            _service.Track(new Vector3(0f, 1f, 0f), Vector3.zero, 0.6f);               // airTime 0.6, settle=0.6
            _service.Track(new Vector3(0f, 1f, 0f), new Vector3(0f, 0f, 5f), 0.1f);    // скорость выше порога — settle сброшен
            _service.Track(new Vector3(0f, 1f, 0f), Vector3.zero, 0.9f);               // settle=0.9 < 1 — разрыв не суммируется

            Assert.IsTrue(_service.IsInFlight, "Сброс settle-таймера при скачке скорости не должен накапливаться через разрыв.");
            Assert.AreEqual(0, landedEvents.Count);

            _service.Track(new Vector3(0f, 1f, 0f), Vector3.zero, 0.2f); // settle=1.1 >= 1 — приземление уже после полного нового отсчёта

            Assert.IsFalse(_service.IsInFlight);
            Assert.AreEqual(1, landedEvents.Count);

            _bus.Unsubscribe<DriverLandedEvent>(landedEvents.Add);
        }

        // i) До истечения MinAirTime даже нулевая скорость не приземляет — защита от
        // мгновенного триггера сразу после eject.
        [Test]
        public void Track_DoesNotLand_BeforeMinAirTimeElapsed()
        {
            var landedEvents = new System.Collections.Generic.List<DriverLandedEvent>();
            _bus.Subscribe<DriverLandedEvent>(landedEvents.Add);

            _service.BeginFlight(Vector3.zero);

            _service.Track(new Vector3(0f, 1f, 0f), Vector3.zero, 0.2f); // airTime 0.2 < MinAirTime(0.5)
            _service.Track(new Vector3(0f, 1f, 0f), Vector3.zero, 0.2f); // airTime 0.4 < MinAirTime(0.5)

            Assert.IsTrue(_service.IsInFlight, "До MinAirTime приземление не должно засчитываться даже при нулевой скорости.");
            Assert.AreEqual(0, landedEvents.Count);

            _bus.Unsubscribe<DriverLandedEvent>(landedEvents.Add);
        }

        // j) Пик высоты продолжает обновляться независимо от состояния settle-таймера.
        [Test]
        public void Track_PeakHeightContinuesUpdating_RegardlessOfSettleState()
        {
            _service.BeginFlight(new Vector3(0f, 0f, 0f));

            // airTime > MinAirTime, settle начал копиться (0.6с из 1с), но высота всё равно растёт.
            _service.Track(new Vector3(0f, 2f, 0f), Vector3.zero, 0.6f);
            Assert.AreEqual(2f, _service.PeakHeight, 0.001f);

            // settle 0.6+0.3=0.9 < 1 — полёт продолжается, а пик уже обновился до нового максимума.
            _service.Track(new Vector3(0f, 5f, 0f), Vector3.zero, 0.3f);
            Assert.AreEqual(5f, _service.PeakHeight, 0.001f);
            Assert.IsTrue(_service.IsInFlight);
        }
    }
}
