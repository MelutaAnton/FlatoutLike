using FlatOutLike.Core.Events;
using FlatOutLike.Data.Configs.Driver;
using FlatOutLike.Models;
using UnityEngine;

namespace FlatOutLike.Services.Driver
{
    /// <summary>
    /// Чистый C#: раскладывает дельту вращения между кадрами на локальные оси Pitch/Roll/Yaw
    /// через Euler-разложение <c>Quaternion.eulerAngles</c>. При малых dt (FixedUpdate) это
    /// достаточно точное арcade-приближение, чтобы не путать оси между собой.
    /// </summary>
    public sealed class FlipDetectorService : IFlipDetectorService
    {
        private readonly DriverFlightConfig _config;
        private readonly IEventBus _eventBus;

        // Накопленные градусы по осям Pitch(0)/Roll(1)/Yaw(2) — индекс соответствует порядку enum FlipAxis.
        private readonly float[] _accumulatedDegrees = new float[3];

        private Quaternion _lastRotation;
        private bool _hasLastRotation;

        public int FlipCount { get; private set; }

        public FlipDetectorService(DriverFlightConfig config, IEventBus eventBus)
        {
            _config = config;
            _eventBus = eventBus;
        }

        public void Reset()
        {
            FlipCount = 0;
            _hasLastRotation = false;
            _accumulatedDegrees[0] = 0f;
            _accumulatedDegrees[1] = 0f;
            _accumulatedDegrees[2] = 0f;
        }

        public void Feed(Quaternion rotation, float dt)
        {
            // Первый кадр после Reset — просто запоминаем ориентацию, дельту считать не с чем.
            if (!_hasLastRotation)
            {
                _lastRotation = rotation;
                _hasLastRotation = true;
                return;
            }

            if (dt <= 0f) return;

            Quaternion delta = Quaternion.Inverse(_lastRotation) * rotation;
            _lastRotation = rotation;

            Vector3 deltaEuler = delta.eulerAngles;
            ProcessAxis(FlipAxis.Pitch, SignedDelta(deltaEuler.x), dt);
            ProcessAxis(FlipAxis.Yaw,   SignedDelta(deltaEuler.y), dt);
            ProcessAxis(FlipAxis.Roll,  SignedDelta(deltaEuler.z), dt);
        }

        /// <summary>Euler-компонента даёт угол 0..360 — переводим в знаковую дельту -180..180.</summary>
        private static float SignedDelta(float eulerComponent)
        {
            return eulerComponent > 180f ? eulerComponent - 360f : eulerComponent;
        }

        private void ProcessAxis(FlipAxis axis, float deltaDegrees, float dt)
        {
            float speedDegPerSec = Mathf.Abs(deltaDegrees) / dt;
            if (speedDegPerSec < _config.RotationBonusThreshold) return; // ниже порога — не флип, а дрожание ragdoll

            int index = (int)axis;
            _accumulatedDegrees[index] += Mathf.Abs(deltaDegrees);

            if (_accumulatedDegrees[index] < 360f) return;

            _accumulatedDegrees[index] -= 360f;
            FlipCount++;
            _eventBus.Publish(new FlipDetectedEvent(axis, speedDegPerSec));
        }
    }
}
