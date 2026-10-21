using FlatOutLike.Controllers.Hub;
using NUnit.Framework;

namespace FlatOutLike.Tests
{
    /// <summary>Юнит-тесты <see cref="StagePopCurve"/> (шаг 12b): кривая «попа» появления стадии стройки.</summary>
    [TestFixture]
    public class StagePopCurveTests
    {
        private const float Start = 0.6f;
        private const float Overshoot = 1.08f;

        [Test]
        public void Evaluate_AtZero_ReturnsStartScale()
        {
            Assert.AreEqual(Start, StagePopCurve.Evaluate(0f, Start, Overshoot));
        }

        [Test]
        public void Evaluate_AtOne_ReturnsExactlyOne()
        {
            // Точное равенство: по окончании попа масштаб обязан быть ровно базовым.
            Assert.AreEqual(1f, StagePopCurve.Evaluate(1f, Start, Overshoot));
        }

        [Test]
        public void Evaluate_BelowZero_ClampsToStartScale()
        {
            Assert.AreEqual(Start, StagePopCurve.Evaluate(-0.5f, Start, Overshoot));
        }

        [Test]
        public void Evaluate_AboveOne_ClampsToOne()
        {
            Assert.AreEqual(1f, StagePopCurve.Evaluate(1.5f, Start, Overshoot));
        }

        [Test]
        public void Evaluate_Maximum_ApproximatelyOvershoot()
        {
            float max = float.MinValue;
            for (int i = 0; i <= 1000; i++)
            {
                float v = StagePopCurve.Evaluate(i / 1000f, Start, Overshoot);
                if (v > max) max = v;
            }

            Assert.AreEqual(Overshoot, max, 0.005f);
        }

        [Test]
        public void Evaluate_FirstPhase_IsMonotonicallyIncreasing()
        {
            float prev = StagePopCurve.Evaluate(0f, Start, Overshoot);
            for (int i = 1; i <= 69; i++)
            {
                float v = StagePopCurve.Evaluate(i / 100f, Start, Overshoot);
                Assert.GreaterOrEqual(v, prev, $"падение значения на t={i / 100f}");
                prev = v;
            }
        }

        [Test]
        public void Evaluate_SecondPhase_ReturnsTowardOne()
        {
            float prev = StagePopCurve.Evaluate(0.7f, Start, Overshoot);
            for (int i = 71; i <= 100; i++)
            {
                float v = StagePopCurve.Evaluate(i / 100f, Start, Overshoot);
                Assert.LessOrEqual(v, prev, $"рост значения на t={i / 100f}");
                prev = v;
            }
        }
    }
}
