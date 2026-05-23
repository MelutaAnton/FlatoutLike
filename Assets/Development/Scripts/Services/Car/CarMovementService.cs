using FlatOutLike.Data.Configs.Car;
using UnityEngine;

namespace FlatOutLike.Services.Car
{
    public sealed class CarMovementService : ICarMovementService
    {
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

        public float CalculateBrakeTorque(bool handbrake, float throttle, float currentForwardSpeed, CarStatsConfig stats)
        {
            if (handbrake) return stats.BrakeForce;

            // Engine braking: when throttle is released and the car is still moving,
            // apply a soft brake to mimic engine drag. Disengaged in two cases:
            //   1) throttle pressed (motor torque takes over)
            //   2) car is at rest (no point braking standstill — would also lock wheels)
            bool throttleReleased = Mathf.Abs(throttle) < 0.05f;
            bool isMoving = Mathf.Abs(currentForwardSpeed) > 0.1f;
            if (throttleReleased && isMoving) return stats.EngineBrakeForce;

            return 0f;
        }
    }
}
