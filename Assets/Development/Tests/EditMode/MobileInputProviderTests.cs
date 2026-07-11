using FlatOutLike.Core.Input;
using NUnit.Framework;
using UnityEngine;

namespace FlatOutLike.Tests
{
    /// <summary>
    /// Юнит-тесты pure-C# провайдера <see cref="MobileInputProvider"/> (Шаг 9).
    /// Проверяем: латч одноразовых триггеров на один Tick(), контекст-гейтинг
    /// (Move/Look/EjectPressed/MidairBoostPressed завязаны на CurrentContext,
    /// PausePressed — нет), и что continuous-ввод (Handbrake/Nitro) не латчится.
    /// </summary>
    [TestFixture]
    public class MobileInputProviderTests
    {
        private MobileInputProvider _provider;

        [SetUp]
        public void SetUp()
        {
            _provider = new MobileInputProvider();
        }

        // ── Латч одноразовых триггеров ──────────────────────────────────────────

        [Test]
        public void QueueEject_InDriving_IsPressedOnNextTickOnly()
        {
            _provider.CurrentContext = InputContext.Driving;
            _provider.QueueEject();

            _provider.Tick();
            Assert.IsTrue(_provider.EjectPressed,
                "После Tick(), следующего за Queue, EjectPressed должен стать true.");

            _provider.Tick();
            Assert.IsFalse(_provider.EjectPressed,
                "Следующий Tick() без нового Queue должен сбросить EjectPressed в false (латч на один кадр).");
        }

        [Test]
        public void QueueEject_BeforeTick_IsNotVisibleYet()
        {
            _provider.CurrentContext = InputContext.Driving;
            _provider.QueueEject();

            Assert.IsFalse(_provider.EjectPressed,
                "До первого Tick() после Queue EjectPressed должен оставаться false.");
        }

        // Ключевое свойство безопасности: pending-триггер, заквеенный в Driving,
        // ОТБРАСЫВАЕТСЯ Tick()'ом в чужом контексте — и не «прожигается» позже
        // при возврате в Driving (иначе отложенный eject сработал бы из меню паузы).
        [Test]
        public void QueueEject_ThenContextLeavesDriving_PendingIsDiscardedForever()
        {
            _provider.CurrentContext = InputContext.Driving;
            _provider.QueueEject();

            _provider.CurrentContext = InputContext.UI;
            _provider.Tick();
            Assert.IsFalse(_provider.EjectPressed,
                "Eject вне Driving-контекста срабатывать не должен.");

            _provider.CurrentContext = InputContext.Driving;
            _provider.Tick();
            Assert.IsFalse(_provider.EjectPressed,
                "Отброшенный pending не должен «прожечься» при возврате в Driving.");
        }

        [Test]
        public void QueueMidairBoost_InFlight_IsPressedOnNextTickOnly()
        {
            _provider.CurrentContext = InputContext.Flight;
            _provider.QueueMidairBoost();

            _provider.Tick();
            Assert.IsTrue(_provider.MidairBoostPressed,
                "В Flight-контексте буст должен сработать на ближайшем Tick().");

            _provider.Tick();
            Assert.IsFalse(_provider.MidairBoostPressed,
                "Латч на один кадр — второй Tick() без Queue сбрасывает.");
        }

        // ── Контекст-гейтинг ─────────────────────────────────────────────────────

        [Test]
        public void SetMove_InDriving_PassesValueThrough()
        {
            _provider.CurrentContext = InputContext.Driving;
            var move = new Vector2(0.5f, -1f);
            _provider.SetMove(move);

            _provider.Tick();

            Assert.AreEqual(move, _provider.Move,
                "В Driving-контексте Move должен проходить без изменений.");
        }

        [Test]
        public void SetLook_InFlight_PassesValueThrough()
        {
            _provider.CurrentContext = InputContext.Flight;
            var look = new Vector2(-0.25f, 1f);
            _provider.SetLook(look);

            _provider.Tick();

            Assert.AreEqual(look, _provider.Look,
                "В Flight-контексте Look должен проходить без изменений.");
        }

        [Test]
        public void SetHandbrakeAndNitro_OutsideDriving_ResultInFalse()
        {
            _provider.CurrentContext = InputContext.Flight;
            _provider.SetHandbrake(true);
            _provider.SetNitro(true);

            _provider.Tick();

            Assert.IsFalse(_provider.Handbrake, "Handbrake действует только в Driving-контексте.");
            Assert.IsFalse(_provider.Nitro, "Nitro действует только в Driving-контексте.");
        }

        [Test]
        public void SetMove_InUIContext_ResultsInZeroMove()
        {
            _provider.CurrentContext = InputContext.UI;
            _provider.SetMove(new Vector2(1f, 1f));

            _provider.Tick();

            Assert.AreEqual(Vector2.zero, _provider.Move,
                "Move действует только в Driving-контексте.");
        }

        [Test]
        public void SetLook_InDrivingContext_ResultsInZeroLook()
        {
            _provider.CurrentContext = InputContext.Driving;
            _provider.SetLook(new Vector2(1f, 1f));

            _provider.Tick();

            Assert.AreEqual(Vector2.zero, _provider.Look,
                "Look действует только в Flight-контексте.");
        }

        [Test]
        public void QueueMidairBoost_InDrivingContext_IsFalseAfterTick()
        {
            _provider.CurrentContext = InputContext.Driving;
            _provider.QueueMidairBoost();

            _provider.Tick();

            Assert.IsFalse(_provider.MidairBoostPressed,
                "MidairBoostPressed действует только в Flight-контексте.");
        }

        [Test]
        public void QueuePause_InAnyContext_IsTrueAfterTick()
        {
            _provider.CurrentContext = InputContext.UI;
            _provider.QueuePause();

            _provider.Tick();

            Assert.IsTrue(_provider.PausePressed,
                "PausePressed контекст-независим — должен срабатывать в любом состоянии.");
        }

        // ── Continuous-ввод (не латчится) ───────────────────────────────────────

        [Test]
        public void SetNitro_True_InDriving_StaysTrueAcrossMultipleTicks()
        {
            _provider.CurrentContext = InputContext.Driving;
            _provider.SetNitro(true);

            _provider.Tick();
            Assert.IsTrue(_provider.Nitro, "Nitro должен стать true после Tick().");

            _provider.Tick();
            Assert.IsTrue(_provider.Nitro,
                "В отличие от одноразовых триггеров, Nitro не латчится — остаётся true, пока держат кнопку.");
        }

        [Test]
        public void SetNitro_False_ResultsInFalseAfterTick()
        {
            _provider.CurrentContext = InputContext.Driving;
            _provider.SetNitro(true);
            _provider.Tick();

            _provider.SetNitro(false);
            _provider.Tick();

            Assert.IsFalse(_provider.Nitro, "Отпускание кнопки должно сбросить Nitro в false.");
        }
    }
}
