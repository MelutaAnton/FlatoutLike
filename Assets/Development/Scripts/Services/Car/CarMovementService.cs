using FlatOutLike.Data.Configs.Car;
using UnityEngine;

namespace FlatOutLike.Services.Car
{
    public sealed class CarMovementService : ICarMovementService
    {
        // Газ ниже порога считается отпущенным (engine braking включается).
        private const float ThrottleDeadzone = 0.05f;
        // Скорость выше порога (м/с) считается движением.
        private const float MovingSpeedThreshold = 0.1f;

        public float CalculateMotorTorque(float throttle, float currentForwardSpeed, CarStatsConfig stats)
        {
            float clampedThrottle = Mathf.Clamp(throttle, -1f, 1f);

            bool sameDirection = Mathf.Sign(clampedThrottle) == Mathf.Sign(currentForwardSpeed);
            bool atTopSpeed = Mathf.Abs(currentForwardSpeed) >= stats.TopSpeed;

            if (sameDirection && atTopSpeed)
                return 0f;

            return clampedThrottle * stats.Acceleration;
        }

        public float CalculateSteerAngle(float steerInput, float currentForwardSpeed, CarStatsConfig stats)
        {
            float clampedSteer = Mathf.Clamp(steerInput, -1f, 1f);
            float speedFactor = Mathf.Clamp01(Mathf.Abs(currentForwardSpeed) / Mathf.Max(0.001f, stats.TopSpeed));
            float effectiveAngle = stats.SteerAngle * (1f - stats.SteerSpeedFalloff * speedFactor);
            return clampedSteer * effectiveAngle;
        }

        public float CalculateSmoothedSteerAngle(float currentAngle, float steerInput, float currentForwardSpeed, float dt, CarStatsConfig stats)
        {
            float targetAngle = CalculateSteerAngle(steerInput, currentForwardSpeed, stats);

            // Возврат к центру (или смена стороны) должен ощущаться отзывчивее доворота —
            // на клавиатуре руль либо полностью выкручен, либо отпущен, и медленный возврат
            // читается как "вязкая" машина. Поэтому SteerReturnRate > SteerRate.
            // Смена стороны (current и target по разные стороны нуля): участок до нуля — тоже
            // "возврат", идёт быстрым темпом; после пересечения нуля знаки совпадут и доворот
            // до цели продолжится обычным SteerRate.
            bool oppositeSides = currentAngle * targetAngle < 0f;
            bool returningToCenter = oppositeSides || Mathf.Abs(targetAngle) < Mathf.Abs(currentAngle);
            float rate = returningToCenter ? stats.SteerReturnRate : stats.SteerRate;

            return Mathf.MoveTowards(currentAngle, targetAngle, rate * dt);
        }

        public float CalculateBrakeTorque(bool handbrake, float throttle, float currentForwardSpeed, CarStatsConfig stats)
        {
            // Ручник во FlatOut — инструмент заноса, а не полная остановка: частичный тормозной
            // момент даёт машине сорваться в дрифт, а не мгновенно встать колом.
            if (handbrake) return stats.HandbrakeBrakeForce;

            // Engine braking: when throttle is released and the car is still moving,
            // apply a soft brake to mimic engine drag. Disengaged in two cases:
            //   1) throttle pressed (motor torque takes over)
            //   2) car is at rest (no point braking standstill — would also lock wheels)
            bool throttleReleased = Mathf.Abs(throttle) < ThrottleDeadzone;
            bool isMoving = Mathf.Abs(currentForwardSpeed) > MovingSpeedThreshold;
            if (throttleReleased && isMoving) return stats.EngineBrakeForce;

            return 0f;
        }
    }
}
