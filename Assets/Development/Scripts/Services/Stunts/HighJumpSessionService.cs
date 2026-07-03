using System;
using FlatOutLike.Core.Events;
using FlatOutLike.Data.Configs.Stunts;
using FlatOutLike.Models;
using FlatOutLike.Services.Driver;
using FlatOutLike.Services.Flow;
using VContainer.Unity;

namespace FlatOutLike.Services.Stunts
{
    /// <summary>
    /// Event-driven жизненный цикл попытки «High Jump» (аналог DerbyMatchService для другого режима).
    /// Тик используется только как предохранитель против «улетевшего мимо LandingZone» ragdoll —
    /// основная логика реагирует на DriverEjectedEvent/DriverLandedEvent.
    /// </summary>
    public sealed class HighJumpSessionService : IHighJumpSessionService, IDisposable, ITickable
    {
        private readonly IGameFlowService _gameFlow;
        private readonly IHighJumpScoreService _score;
        private readonly IDriverFlightService _flight;
        private readonly HighJumpRulesConfig _rules;
        private readonly IEventBus _eventBus;

        public HighJumpSessionService(IGameFlowService gameFlow, IHighJumpScoreService score,
            IDriverFlightService flight, HighJumpRulesConfig rules, IEventBus eventBus)
        {
            _gameFlow = gameFlow;
            _score = score;
            _flight = flight;
            _rules = rules;
            _eventBus = eventBus;

            _eventBus.Subscribe<DriverEjectedEvent>(OnDriverEjected);
            _eventBus.Subscribe<DriverLandedEvent>(OnDriverLanded);
        }

        public void StartSession()
        {
            _gameFlow.ChangeState(GameState.StuntsHighJumpDriving);
        }

        private void OnDriverEjected(DriverEjectedEvent e)
        {
            if (e.Reason != EjectReason.StuntRamp) return;

            // BeginFlight вызывает DriverFlightController (у него есть позиция таза) —
            // сессия только переключает GameFlow, чтобы InputRouter отдал Look/Boost водителю.
            _gameFlow.ChangeState(GameState.StuntsHighJumpFlight);
        }

        private void OnDriverLanded(DriverLandedEvent e)
        {
            _score.ScoreAttempt(e);
            _gameFlow.ChangeState(GameState.StuntsHighJumpScoring);
        }

        /// <summary>
        /// Предохранитель: если полёт затянулся дольше FlightTimeoutSeconds (ragdoll улетел
        /// мимо LandingZone), принудительно завершаем попытку — иначе сессия зависает в Flight.
        /// </summary>
        public void Tick()
        {
            if (_gameFlow.Current != GameState.StuntsHighJumpFlight) return;
            if (!_flight.IsInFlight) return;
            if (_flight.AirTime <= _rules.FlightTimeoutSeconds) return;

            _flight.NotifyLanded();
        }

        public void Dispose()
        {
            _eventBus.Unsubscribe<DriverEjectedEvent>(OnDriverEjected);
            _eventBus.Unsubscribe<DriverLandedEvent>(OnDriverLanded);
        }
    }
}
