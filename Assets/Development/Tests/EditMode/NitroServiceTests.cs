using FlatOutLike.Models;
using FlatOutLike.Services.Combat;
using NUnit.Framework;

namespace FlatOutLike.Tests
{
    /// <summary>
    /// Юнит-тесты pure-C# сервиса <see cref="NitroService"/>.
    /// Проверяем инварианты: списание, отказ при нехватке, долив с cap, GetNitro01,
    /// незарегистрированные машины — no-op.
    /// </summary>
    [TestFixture]
    public class NitroServiceTests
    {
        private INitroService _service;

        // Тестовые машины (не пересекаются с остальными тестами)
        private static readonly CarId Car1 = new CarId(10);
        private static readonly CarId Car2 = new CarId(11);

        [SetUp]
        public void SetUp()
        {
            _service = new NitroService();
        }

        // ── RegisterCar ──────────────────────────────────────────────────────────

        [Test]
        public void RegisterCar_StartsWithEmptyTank()
        {
            _service.RegisterCar(Car1, 100f);

            Assert.AreEqual(0f, _service.GetNitro01(Car1), 0.001f,
                "Только что зарегистрированный бак должен быть пустым.");
        }

        // ── TryConsume ───────────────────────────────────────────────────────────

        [Test]
        public void TryConsume_WhenEnough_DeductsAmountAndReturnsTrue()
        {
            _service.RegisterCar(Car1, 100f);
            _service.Refill(Car1, 1f); // заполнить до 100%

            bool result = _service.TryConsume(Car1, 30f);

            Assert.IsTrue(result, "Должен вернуть true при достаточном запасе.");
            Assert.AreEqual(0.7f, _service.GetNitro01(Car1), 0.001f,
                "После расхода 30 из 100 должно остаться 70%.");
        }

        [Test]
        public void TryConsume_WhenNotEnough_ReturnsFalseAndLevelUnchanged()
        {
            _service.RegisterCar(Car1, 100f);
            _service.Refill(Car1, 0.2f); // 20 единиц

            bool result = _service.TryConsume(Car1, 50f);

            Assert.IsFalse(result, "Должен вернуть false при нехватке нитро.");
            Assert.AreEqual(0.2f, _service.GetNitro01(Car1), 0.001f,
                "Уровень не должен измениться при отказе.");
        }

        [Test]
        public void TryConsume_WhenNotEnough_NeverGoesNegative()
        {
            _service.RegisterCar(Car1, 100f);
            // Бак пуст — пытаемся списать
            _service.TryConsume(Car1, 1f);

            Assert.GreaterOrEqual(_service.GetNitro01(Car1), 0f,
                "Уровень нитро никогда не уходит в минус.");
        }

        [Test]
        public void TryConsume_WithZeroAmount_ReturnsFalse()
        {
            // Регрессионный тест: amount=0 должен возвращать false.
            // Без guard'а: 0 < 0 == false → TryConsume вернул бы true и AddForce работал с пустым баком
            // (сценарий: _nitroDrainRate=0 в не-пересериализованном SO-ассете).
            _service.RegisterCar(Car1, 100f);
            _service.Refill(Car1, 1f); // бак полный — важно, чтобы проверка была не о нехватке

            bool result = _service.TryConsume(Car1, 0f);

            Assert.IsFalse(result, "Нулевой расход не является валидным списанием.");
        }

        [Test]
        public void TryConsume_UnregisteredCar_ReturnsFalse()
        {
            bool result = _service.TryConsume(Car2, 10f);
            Assert.IsFalse(result, "Незарегистрированная машина — TryConsume должен вернуть false.");
        }

        // ── Refill ───────────────────────────────────────────────────────────────

        [Test]
        public void Refill_CapsByCapacity()
        {
            _service.RegisterCar(Car1, 100f);
            _service.Refill(Car1, 0.6f);  // 60 единиц
            _service.Refill(Car1, 0.8f);  // ещё 80 — итого 140, должно стать 100

            Assert.AreEqual(1.0f, _service.GetNitro01(Car1), 0.001f,
                "Долив не должен превышать ёмкость бака.");
        }

        [Test]
        public void Refill_UnregisteredCar_IsNoOp()
        {
            Assert.DoesNotThrow(() => _service.Refill(Car2, 1f),
                "Refill незарегистрированной машины не должен бросать исключение.");
        }

        // ── GetNitro01 ───────────────────────────────────────────────────────────

        [Test]
        public void GetNitro01_ReturnsCorrectFraction()
        {
            _service.RegisterCar(Car1, 200f);
            _service.Refill(Car1, 0.5f); // 100 из 200

            Assert.AreEqual(0.5f, _service.GetNitro01(Car1), 0.001f,
                "GetNitro01 должен возвращать долю текущего уровня к ёмкости.");
        }

        [Test]
        public void GetNitro01_UnregisteredCar_ReturnsZero()
        {
            float nitro = _service.GetNitro01(Car2);
            Assert.AreEqual(0f, nitro, 0.001f,
                "Незарегистрированная машина должна возвращать 0.");
        }
    }
}
