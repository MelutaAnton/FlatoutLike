using FlatOutLike.Data.Configs.Car;
using FlatOutLike.Services.Car;
using NUnit.Framework;
using UnityEngine;

namespace FlatOutLike.Tests
{
    /// <summary>
    /// Юнит-тесты pure-C# сервиса <see cref="CarMovementService"/>.
    /// Полишинг вождения: сглаживание руля (CalculateSmoothedSteerAngle) и
    /// частичный тормоз ручника (CalculateBrakeTorque).
    /// </summary>
    [TestFixture]
    public class CarMovementServiceTests
    {
        private ICarMovementService _service;
        private CarStatsConfig _stats;

        // Дефолты CarStatsConfig: SteerAngle=30, SteerSpeedFalloff=0.35, TopSpeed=30,
        // SteerRate=300, SteerReturnRate=520, EngineBrakeForce=400, HandbrakeBrakeForce=800.
        private const float Dt = 0.02f;

        [SetUp]
        public void SetUp()
        {
            _service = new CarMovementService();
            _stats = ScriptableObject.CreateInstance<CarStatsConfig>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_stats);
        }

        // ── CalculateSmoothedSteerAngle: доворот ────────────────────────────────

        [Test]
        public void SmoothedSteer_Turning_DoesNotExceedSteerRatePerStep()
        {
            // Руль полностью выкручен, машина стоит на месте (speedFactor=0) — target = SteerAngle = 30.
            float result = _service.CalculateSmoothedSteerAngle(0f, 1f, 0f, Dt, _stats);

            float maxDelta = _stats.SteerRate * Dt;
            Assert.LessOrEqual(Mathf.Abs(result), maxDelta + 0.0001f,
                "За один шаг руль не должен доворачиваться быстрее SteerRate*dt.");
            Assert.AreEqual(maxDelta, result, 0.001f,
                "Доворот от 0 к цели должен двигаться ровно на SteerRate*dt (цель дальше этого шага).");
        }

        [Test]
        public void SmoothedSteer_Turning_NeverOvershootsTarget()
        {
            // Малый dt — доворот должен остановиться точно на цели, а не перелететь её.
            float result = _service.CalculateSmoothedSteerAngle(29.9f, 1f, 0f, 1f, _stats);

            Assert.AreEqual(30f, result, 0.001f,
                "MoveTowards не должен проскакивать целевой угол.");
        }

        // ── CalculateSmoothedSteerAngle: возврат к центру ───────────────────────

        [Test]
        public void SmoothedSteer_ReturningToCenter_UsesFasterReturnRate()
        {
            // Руль отпущен (steerInput=0) — target = 0, что ближе к нулю, чем текущий угол 20.
            float result = _service.CalculateSmoothedSteerAngle(20f, 0f, 0f, Dt, _stats);

            float expectedWithReturnRate = 20f - _stats.SteerReturnRate * Dt;
            float wouldBeWithSteerRate = 20f - _stats.SteerRate * Dt;

            Assert.AreEqual(expectedWithReturnRate, result, 0.001f,
                "Возврат к центру должен использовать SteerReturnRate.");
            Assert.Less(result, wouldBeWithSteerRate,
                "Возврат должен быть быстрее (больше по модулю), чем доворот на SteerRate.");
        }

        [Test]
        public void SmoothedSteer_ReturningToCenter_NeverOvershootsZero()
        {
            float result = _service.CalculateSmoothedSteerAngle(5f, 0f, 0f, 1f, _stats);

            Assert.AreEqual(0f, result, 0.001f,
                "Возврат к центру не должен проскакивать через ноль.");
        }

        [Test]
        public void SmoothedSteer_SwitchingSides_UsesReturnRateUntilZeroCrossing()
        {
            // Перекладка руля: текущий угол +20, цель −30 (steerInput=-1, стоя на месте).
            // Участок от +20 до нуля — это "возврат", должен идти быстрым SteerReturnRate.
            float result = _service.CalculateSmoothedSteerAngle(20f, -1f, 0f, Dt, _stats);

            float expectedWithReturnRate = 20f - _stats.SteerReturnRate * Dt;

            Assert.AreEqual(expectedWithReturnRate, result, 0.001f,
                "Смена стороны до пересечения нуля должна идти со SteerReturnRate.");
        }

        // ── CalculateBrakeTorque: ручник и engine brake ─────────────────────────

        [Test]
        public void BrakeTorque_Handbrake_ReturnsHandbrakeBrakeForce()
        {
            float brake = _service.CalculateBrakeTorque(handbrake: true, throttle: 0f, currentForwardSpeed: 10f, _stats);

            Assert.AreEqual(_stats.HandbrakeBrakeForce, brake, 0.001f,
                "При ручнике должен применяться частичный HandbrakeBrakeForce, а не полный BrakeForce.");
        }

        [Test]
        public void BrakeTorque_ThrottleReleasedNoHandbrake_ReturnsEngineBrakeForce()
        {
            float brake = _service.CalculateBrakeTorque(handbrake: false, throttle: 0f, currentForwardSpeed: 5f, _stats);

            Assert.AreEqual(_stats.EngineBrakeForce, brake, 0.001f,
                "При отпущенном газе без ручника должен применяться engine brake.");
        }

        [Test]
        public void BrakeTorque_ThrottlePressed_ReturnsZero()
        {
            float brake = _service.CalculateBrakeTorque(handbrake: false, throttle: 1f, currentForwardSpeed: 5f, _stats);

            Assert.AreEqual(0f, brake, 0.001f,
                "Газ нажат — торможения быть не должно.");
        }
    }
}
