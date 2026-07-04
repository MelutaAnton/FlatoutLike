using FlatOutLike.Data.Configs.Car;

namespace FlatOutLike.Services.Car
{
    public interface ICarMovementService
    {
        float CalculateMotorTorque(float throttle, float currentForwardSpeed, CarStatsConfig stats);

        /// <summary>
        /// Мгновенный целевой угол руля (clamp + speed-falloff), без сглаживания.
        /// Внутренний строительный блок <see cref="CalculateSmoothedSteerAngle"/> и опора тестов.
        /// Все машины (игрок и AI — AIInputProvider подаёт ввод в тот же <c>CarController</c>)
        /// рулятся через сглаженный вариант; состояние угла хранит контроллер, по одному на машину.
        /// </summary>
        float CalculateSteerAngle(float steerInput, float currentForwardSpeed, CarStatsConfig stats);

        /// <summary>
        /// Сглаженный угол руля: целевой угол считается так же, как в <see cref="CalculateSteerAngle"/>,
        /// но текущий угол двигается к нему через MoveTowards — со скоростью <see cref="CarStatsConfig.SteerRate"/>
        /// при довороте и <see cref="CarStatsConfig.SteerReturnRate"/> при возврате к центру/смене стороны.
        /// Чистая функция без состояния — вызывающий (CarController) хранит currentAngle между тиками.
        /// </summary>
        float CalculateSmoothedSteerAngle(float currentAngle, float steerInput, float currentForwardSpeed, float dt, CarStatsConfig stats);

        float CalculateBrakeTorque(bool handbrake, float throttle, float currentForwardSpeed, CarStatsConfig stats);
    }
}
