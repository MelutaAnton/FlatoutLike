using FlatOutLike.Core.Events;
using FlatOutLike.Data.Configs.Driver;
using UnityEngine;

namespace FlatOutLike.Services.Driver
{
    /// <summary>
    /// Чистый C#: состояние одного полёта водителя. Никакого MonoBehaviour — величины
    /// (позиция, dt, ввод) поставляет <c>DriverFlightController</c> каждый FixedUpdate.
    /// ВНИМАНИЕ: синглтон в расчёте на ОДНОГО летящего водителя (HighJump-сцена).
    /// В Derby несколько выброшенных водителей затирают состояние друг друга —
    /// безвредно (там оно никем не читается); для мультитрекинга нужен per-driver рефакторинг.
    /// </summary>
    public sealed class DriverFlightService : IDriverFlightService
    {
        private readonly DriverFlightConfig _config;
        private readonly IFlipDetectorService _flipDetector;
        private readonly IEventBus _eventBus;

        private Vector3 _startPosition;
        private float _peakHeight;
        private float _airTime;
        private int _boostsUsed;

        public bool IsInFlight { get; private set; }
        public float PeakHeight => _peakHeight;
        public float AirTime => _airTime;
        public float BoostImpulse => _config.BoostImpulse;

        public DriverFlightService(DriverFlightConfig config, IFlipDetectorService flipDetector, IEventBus eventBus)
        {
            _config = config;
            _flipDetector = flipDetector;
            _eventBus = eventBus;
        }

        public void BeginFlight(Vector3 startPosition)
        {
            _startPosition = startPosition;
            _peakHeight = 0f;
            _airTime = 0f;
            _boostsUsed = 0;
            IsInFlight = true;

            _flipDetector.Reset();
        }

        public Vector3 CalculateSteerAcceleration(Vector2 look, Vector3 currentVelocity)
        {
            if (look.sqrMagnitude <= 0f) return Vector3.zero;

            // Горизонтальная проекция скорости задаёт "вдоль" (Look.Y) и "поперёк" (Look.X) направления.
            // При почти нулевой горизонтальной скорости — фолбэк на мировой forward, чтобы коррекция
            // не пропадала в свободном падении почти строго вниз.
            Vector3 horizontalVelocity = new Vector3(currentVelocity.x, 0f, currentVelocity.z);
            Vector3 forward = horizontalVelocity.sqrMagnitude > 0.0001f
                ? horizontalVelocity.normalized
                : Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, forward);

            Vector3 steer = (right * look.x + forward * look.y) * _config.SteerInfluence;

            // Штраф скорости (§4 контракта): тормозим вдоль текущей скорости, пока коррекция активна.
            if (currentVelocity.sqrMagnitude > 0.0001f)
                steer += -currentVelocity.normalized * _config.SteerSpeedPenalty * _config.SteerInfluence;

            return steer;
        }

        public bool TryConsumeBoost()
        {
            if (_boostsUsed >= _config.BoostCount) return false;

            _boostsUsed++;
            return true;
        }

        public void Track(Vector3 position, float dt)
        {
            if (!IsInFlight) return;

            _airTime += dt;

            float height = position.y - _startPosition.y;
            if (height > _peakHeight)
                _peakHeight = height;
        }

        public void NotifyLanded()
        {
            if (!IsInFlight) return; // одноразово: повторный вызов после посадки игнорируется

            IsInFlight = false;
            _eventBus.Publish(new DriverLandedEvent(_peakHeight, _flipDetector.FlipCount, _airTime));
        }
    }
}
