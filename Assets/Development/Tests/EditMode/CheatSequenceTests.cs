using FlatOutLike.Services.Dev;
using NUnit.Framework;

namespace FlatOutLike.Tests
{
    /// <summary>
    /// Юнит-тесты счётчика комбинации dev-чита: срабатывание ровно на N-м нажатии подряд,
    /// сброс серии по таймауту между нажатиями, по Reset и после срабатывания.
    /// </summary>
    [TestFixture]
    public class CheatSequenceTests
    {
        private const int RequiredPresses = 4;
        private const float MaxInterval = 1.5f;
        private const float ShortGap = 0.2f;

        private static CheatSequence CreateDefault() => new CheatSequence(RequiredPresses, MaxInterval);

        [Test]
        public void RegisterPress_FourQuickPresses_TriggersOnFourth()
        {
            var sequence = CreateDefault();

            Assert.IsFalse(sequence.RegisterPress(0f));
            Assert.IsFalse(sequence.RegisterPress(ShortGap));
            Assert.IsFalse(sequence.RegisterPress(ShortGap * 2f));
            Assert.IsTrue(sequence.RegisterPress(ShortGap * 3f));
        }

        [Test]
        public void RegisterPress_ThreePresses_DoesNotTrigger()
        {
            var sequence = CreateDefault();

            Assert.IsFalse(sequence.RegisterPress(0f));
            Assert.IsFalse(sequence.RegisterPress(ShortGap));
            Assert.IsFalse(sequence.RegisterPress(ShortGap * 2f));
        }

        [Test]
        public void RegisterPress_GapLongerThanInterval_StartsNewSeries()
        {
            var sequence = CreateDefault();

            sequence.RegisterPress(0f);
            sequence.RegisterPress(ShortGap);
            sequence.RegisterPress(ShortGap * 2f);

            // 4-е нажатие после долгой паузы — снова «первое», чит не срабатывает.
            float afterPause = ShortGap * 2f + MaxInterval + 0.1f;
            Assert.IsFalse(sequence.RegisterPress(afterPause));

            // Новая серия добирается ещё тремя быстрыми нажатиями.
            Assert.IsFalse(sequence.RegisterPress(afterPause + ShortGap));
            Assert.IsFalse(sequence.RegisterPress(afterPause + ShortGap * 2f));
            Assert.IsTrue(sequence.RegisterPress(afterPause + ShortGap * 3f));
        }

        [Test]
        public void Reset_ClearsSeries()
        {
            var sequence = CreateDefault();

            sequence.RegisterPress(0f);
            sequence.RegisterPress(ShortGap);
            sequence.RegisterPress(ShortGap * 2f);
            sequence.Reset();

            // После сброса 4-е нажатие считается первым.
            Assert.IsFalse(sequence.RegisterPress(ShortGap * 3f));
        }

        [Test]
        public void RegisterPress_AfterTrigger_RequiresFullSeriesAgain()
        {
            var sequence = CreateDefault();
            float time = 0f;

            for (int i = 0; i < RequiredPresses - 1; i++, time += ShortGap)
                sequence.RegisterPress(time);
            Assert.IsTrue(sequence.RegisterPress(time));

            // Следующие три нажатия не срабатывают, четвёртое — срабатывает снова.
            for (int i = 0; i < RequiredPresses - 1; i++)
            {
                time += ShortGap;
                Assert.IsFalse(sequence.RegisterPress(time), $"Нажатие {i + 1} после срабатывания.");
            }

            time += ShortGap;
            Assert.IsTrue(sequence.RegisterPress(time));
        }
    }
}
