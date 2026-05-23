using FlatOutLike.Data.Configs.Car;

namespace FlatOutLike.Services.Car
{
    public interface ICarMovementService
    {
        float CalculateMotorTorque(float throttle, float currentForwardSpeed, CarStatsConfig stats);
        float CalculateSteerAngle(float steerInput, float currentForwardSpeed, CarStatsConfig stats);
        float CalculateBrakeTorque(bool handbrake, float throttle, float currentForwardSpeed, CarStatsConfig stats);
    }
}
