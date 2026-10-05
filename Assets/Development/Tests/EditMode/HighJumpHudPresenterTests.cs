using FlatOutLike.Models;
using FlatOutLike.Views.UI.Hud;
using NUnit.Framework;

namespace FlatOutLike.Tests
{
    /// <summary>
    /// Юнит-тесты <see cref="HighJumpHudPresenter"/> (Шаг 8d).
    ///
    /// HighJumpHudPresenter конструируется от живого HighJumpHudView — MonoBehaviour, которое
    /// нельзя корректно собрать в EditMode-тесте без сцены/префаба (тот же лимит, что и у
    /// GarageView/DerbyHudView — см. GaragePresenterTests, DerbyHudPresenterTests).
    ///
    /// Здесь тестируются ВЫЧИСЛИТЕЛЬНЫЕ ХЕЛПЕРЫ — публичные static, используемые ApplyPhase/
    /// HandleTick в рантайме без изменений:
    ///   • IsDrivingPhase/IsFlightPhase/IsScoringPhase — маппинг GameState -> фаза HUD;
    ///   • MsToKmh — конвертация скорости машины (м/с, Rigidbody) в отображаемые км/ч.
    /// Порядок событий StuntsScoredEvent -> GameStateChangedEvent(Scoring) и переключение фаз
    /// требуют живого View/EventBus вместе и здесь не проверяются — см. класс-комментарий.
    /// </summary>
    [TestFixture]
    public class HighJumpHudPresenterTests
    {
        // ── Маппинг фаз ────────────────────────────────────────────────────────

        [Test]
        public void IsDrivingPhase_OnlyTrueForDrivingState()
        {
            Assert.IsTrue(HighJumpHudPresenter.IsDrivingPhase(GameState.StuntsHighJumpDriving));
            Assert.IsFalse(HighJumpHudPresenter.IsDrivingPhase(GameState.StuntsHighJumpFlight));
            Assert.IsFalse(HighJumpHudPresenter.IsDrivingPhase(GameState.StuntsHighJumpScoring));
            Assert.IsFalse(HighJumpHudPresenter.IsDrivingPhase(GameState.BaseHub));
        }

        [Test]
        public void IsFlightPhase_OnlyTrueForFlightState()
        {
            Assert.IsTrue(HighJumpHudPresenter.IsFlightPhase(GameState.StuntsHighJumpFlight));
            Assert.IsFalse(HighJumpHudPresenter.IsFlightPhase(GameState.StuntsHighJumpDriving));
            Assert.IsFalse(HighJumpHudPresenter.IsFlightPhase(GameState.StuntsHighJumpScoring));
            Assert.IsFalse(HighJumpHudPresenter.IsFlightPhase(GameState.DerbyMatch));
        }

        [Test]
        public void IsScoringPhase_OnlyTrueForScoringState()
        {
            Assert.IsTrue(HighJumpHudPresenter.IsScoringPhase(GameState.StuntsHighJumpScoring));
            Assert.IsFalse(HighJumpHudPresenter.IsScoringPhase(GameState.StuntsHighJumpDriving));
            Assert.IsFalse(HighJumpHudPresenter.IsScoringPhase(GameState.StuntsHighJumpFlight));
            Assert.IsFalse(HighJumpHudPresenter.IsScoringPhase(GameState.MatchEnd));
        }

        // ── MsToKmh ────────────────────────────────────────────────────────────

        [Test]
        public void MsToKmh_ConvertsUsingStandardFactor()
        {
            Assert.AreEqual(36f, HighJumpHudPresenter.MsToKmh(10f), 0.001f);
        }

        [Test]
        public void MsToKmh_Zero_ReturnsZero()
        {
            Assert.AreEqual(0f, HighJumpHudPresenter.MsToKmh(0f), 0.001f);
        }
    }
}
