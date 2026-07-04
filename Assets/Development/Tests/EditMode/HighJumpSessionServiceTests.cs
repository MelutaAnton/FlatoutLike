using FlatOutLike.Core.Events;
using FlatOutLike.Data.Configs.Stunts;
using FlatOutLike.Models;
using FlatOutLike.Services.Driver;
using FlatOutLike.Services.Flow;
using FlatOutLike.Services.Stunts;
using NUnit.Framework;
using UnityEngine;

namespace FlatOutLike.Tests
{
    /// <summary>
    /// Юнит-тесты <see cref="HighJumpSessionService"/>: переключения GameFlow по
    /// DriverEjectedEvent/DriverLandedEvent и вызов скоринга при приземлении.
    /// FakeGameFlowService — образец из DerbyMatchServiceTests.
    /// </summary>
    [TestFixture]
    public class HighJumpSessionServiceTests
    {
        private EventBus _bus;
        private FakeGameFlowService _gameFlow;
        private FakeHighJumpScoreService _score;
        private FakeDriverFlightService _flight;
        private HighJumpRulesConfig _rules;
        private HighJumpSessionService _service;

        private static readonly CarId Player = new CarId(0);

        [SetUp]
        public void SetUp()
        {
            _bus = new EventBus();
            _gameFlow = new FakeGameFlowService();
            _score = new FakeHighJumpScoreService();
            _flight = new FakeDriverFlightService();
            _rules = ScriptableObject.CreateInstance<HighJumpRulesConfig>();

            _service = new HighJumpSessionService(_gameFlow, _score, _flight, _rules, _bus);
        }

        [TearDown]
        public void TearDown()
        {
            _service.Dispose();
            UnityEngine.Object.DestroyImmediate(_rules);
        }

        // a) StartSession переводит GameFlow в StuntsHighJumpDriving
        [Test]
        public void StartSession_ChangesStateToDriving()
        {
            _service.StartSession();

            Assert.AreEqual(GameState.StuntsHighJumpDriving, _gameFlow.LastChanged);
        }

        // b) DriverEjectedEvent(StuntRamp) переводит GameFlow в Flight
        [Test]
        public void DriverEjected_WithStuntRampReason_ChangesStateToFlight()
        {
            _bus.Publish(new DriverEjectedEvent(Player, Vector3.up, EjectReason.StuntRamp));

            Assert.AreEqual(GameState.StuntsHighJumpFlight, _gameFlow.LastChanged);
        }

        // c) DriverEjectedEvent(CarDestroyed) — не наш случай, переход не происходит
        [Test]
        public void DriverEjected_WithCarDestroyedReason_DoesNotChangeState()
        {
            _bus.Publish(new DriverEjectedEvent(Player, Vector3.up, EjectReason.CarDestroyed));

            Assert.AreEqual(GameState.MainMenu, _gameFlow.LastChanged, "CarDestroyed не относится к HighJump — состояние не должно меняться.");
        }

        // d) DriverLandedEvent вызывает скоринг и переводит GameFlow в Scoring
        [Test]
        public void DriverLanded_ScoresAttempt_AndChangesStateToScoring()
        {
            _bus.Publish(new DriverLandedEvent(peakHeight: 10f, flipCount: 1, totalAirTime: 2f));

            Assert.AreEqual(1, _score.ScoreAttemptCalls);
            Assert.AreEqual(GameState.StuntsHighJumpScoring, _gameFlow.LastChanged);
        }

        // ── Фейки ───────────────────────────────────────────────────────────────

        /// <summary>Минимальная реализация IGameFlowService, запоминающая последний ChangeState.</summary>
        private sealed class FakeGameFlowService : IGameFlowService
        {
            public GameState LastChanged { get; private set; } = GameState.MainMenu;

            public GameState Current => LastChanged;

            public void ChangeState(GameState next) => LastChanged = next;
            public void Push(GameState next) => LastChanged = next;
            public void Pop() { }
            public void Tick() { }
        }

        private sealed class FakeHighJumpScoreService : IHighJumpScoreService
        {
            public int ScoreAttemptCalls { get; private set; }

            public int ScoreAttempt(DriverLandedEvent landed)
            {
                ScoreAttemptCalls++;
                return 0;
            }
        }

        /// <summary>Не в полёте по умолчанию — Tick() сессии в этих тестах не проверяется.</summary>
        private sealed class FakeDriverFlightService : IDriverFlightService
        {
            public bool IsInFlight => false;
            public float PeakHeight => 0f;
            public float AirTime => 0f;
            public float BoostImpulse => 0f;
            public float BoostUpFallbackSpeed => 0f;

            public void BeginFlight(Vector3 startPosition) { }
            public Vector3 CalculateSteerAcceleration(Vector2 look, Vector3 currentVelocity) => Vector3.zero;
            public bool TryConsumeBoost() => false;
            public void Track(Vector3 position, Vector3 velocity, float dt) { }
            public void NotifyLanded() { }
        }
    }
}
