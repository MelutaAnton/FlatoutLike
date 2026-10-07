using FlatOutLike.Services.Flow;
using NUnit.Framework;

namespace FlatOutLike.Tests
{
    /// <summary>
    /// Юнит-тесты подшага 11d: фейковая кривая прогресса экрана загрузки. Кривая ease-out
    /// 0 → fakeTarget, затем медленный рост до creepTarget без готовности сцены, рывок до 100%
    /// только после готовности и выдержанного минимума показа; значение никогда не убывает.
    /// </summary>
    [TestFixture]
    public class LoadingProgressModelTests
    {
        private const float FakeTarget = 0.75f;
        private const float FakeSeconds = 2f;
        private const float CreepTarget = 0.9f;
        private const float CreepSeconds = 6f;
        private const float FinishSeconds = 0.2f;
        private const float MinSeconds = 0.8f;

        private const float Step = 0.01f;
        private const float Tolerance = 0.01f;

        private static LoadingProgressModel CreateDefault() =>
            new LoadingProgressModel(FakeTarget, FakeSeconds, CreepTarget, CreepSeconds, FinishSeconds, MinSeconds);

        private static void AdvanceFor(LoadingProgressModel model, float seconds, bool sceneReady)
        {
            int steps = (int)(seconds / Step + 0.5f);
            for (int i = 0; i < steps; i++)
                model.Advance(Step, sceneReady);
        }

        [Test]
        public void Displayed_NeverDecreases()
        {
            var model = CreateDefault();
            float previous = model.Displayed;

            // 10 c без готовности, затем готовность — на всём пути значение не убывает.
            for (int i = 0; i < 1200; i++)
            {
                model.Advance(Step, i >= 1000);
                Assert.GreaterOrEqual(model.Displayed, previous, $"Шаг {i}: прогресс убыл.");
                previous = model.Displayed;
            }
        }

        [Test]
        public void Displayed_ReachesFakeTargetAtFakeSeconds()
        {
            var model = CreateDefault();

            AdvanceFor(model, FakeSeconds, false);

            Assert.AreEqual(FakeTarget, model.Displayed, Tolerance);
        }

        [Test]
        public void WithoutSceneReady_NeverExceedsCreepTarget()
        {
            var model = CreateDefault();

            AdvanceFor(model, 100f, false);

            Assert.LessOrEqual(model.Displayed, CreepTarget + 1e-5f);
            Assert.AreEqual(CreepTarget, model.Displayed, Tolerance);
            Assert.IsFalse(model.IsFinishing);
            Assert.IsFalse(model.IsComplete);
        }

        [Test]
        public void SceneReadyBeforeMinSeconds_DoesNotFinishUntilMin()
        {
            var model = CreateDefault();

            AdvanceFor(model, 0.5f, true);
            Assert.IsFalse(model.IsFinishing, "Готовность раньше минимума показа не должна запускать рывок.");

            AdvanceFor(model, 0.4f, true);
            Assert.IsTrue(model.IsFinishing);
        }

        [Test]
        public void AfterReadyAndMin_CompletesWithinFinishSeconds()
        {
            var model = CreateDefault();

            model.Advance(1f, true);
            Assert.IsTrue(model.IsFinishing);
            Assert.IsFalse(model.IsComplete);

            model.Advance(FinishSeconds * 0.5f, true);
            Assert.IsFalse(model.IsComplete);

            model.Advance(FinishSeconds * 0.75f, true);
            Assert.IsTrue(model.IsComplete);
            Assert.AreEqual(1f, model.Displayed);
        }

        [Test]
        public void ZeroDurations_ReadyImmediately_CompletesOnFirstAdvance()
        {
            var model = new LoadingProgressModel(FakeTarget, 0f, CreepTarget, 0f, 0f, 0f);

            model.Advance(0f, true);

            Assert.IsTrue(model.IsFinishing);
            Assert.IsTrue(model.IsComplete);
            Assert.AreEqual(1f, model.Displayed);
        }

        [Test]
        public void ZeroDurations_NotReady_JumpsToCreepTargetWithoutNaN()
        {
            var model = new LoadingProgressModel(FakeTarget, 0f, CreepTarget, 0f, 0f, 0f);

            model.Advance(0f, false);

            Assert.IsFalse(float.IsNaN(model.Displayed));
            Assert.AreEqual(CreepTarget, model.Displayed, 1e-5f);
            Assert.IsFalse(model.IsComplete);
        }
    }
}
