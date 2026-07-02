using System;
using System.Collections.Generic;
using FlatOutLike.Controllers.Car;
using FlatOutLike.Core.Events;
using FlatOutLike.Data.Configs.Match;
using FlatOutLike.Data.Configs.Rewards;
using FlatOutLike.Factories.Car;
using FlatOutLike.Models;
using FlatOutLike.Services.Flow;
using UnityEngine;

namespace FlatOutLike.Services.Match
{
    /// <summary>
    /// Реализация жизненного цикла Derby-матча. Event-driven: реагирует на
    /// <see cref="CarDestroyedEvent"/>, отдельного тика не требует.
    /// </summary>
    public sealed class DerbyMatchService : IDerbyMatchService, IDisposable
    {
        private readonly ICarFactory      _factory;
        private readonly IEventBus        _eventBus;
        private readonly IGameFlowService _gameFlow;
        private readonly DerbyMatchConfig _matchConfig;
        private readonly RewardConfig     _rewardConfig;

        private HashSet<CarId> _aliveCars = new HashSet<CarId>();
        private CarId _playerCarId = CarId.None;
        private int   _playerKills;
        private bool  _active;
        private float _startTime;

        public DerbyMatchService(ICarFactory factory, IEventBus eventBus, IGameFlowService gameFlow,
            DerbyMatchConfig matchConfig, RewardConfig rewardConfig)
        {
            _factory      = factory;
            _eventBus     = eventBus;
            _gameFlow     = gameFlow;
            _matchConfig  = matchConfig;
            _rewardConfig = rewardConfig;

            _eventBus.Subscribe<CarDestroyedEvent>(OnCarDestroyed);
        }

        /// <inheritdoc/>
        public IReadOnlyList<CarController> StartMatch(IReadOnlyList<Transform> spawnPoints)
        {
            var cars = _factory.SpawnMatch(_matchConfig, spawnPoints);

            if (cars.Count <= 1)
                Debug.LogWarning($"[DerbyMatchService] Матч стартовал с {cars.Count} машин(ой) — " +
                                 "победа не наступит (нужно ≥2). Проверь DerbyMatchConfig и точки спавна.");

            var participants = new List<CarId>(cars.Count);
            foreach (var car in cars)
                participants.Add(car.CarId);

            BeginTracking(participants, cars.Count > 0 ? cars[0].CarId : CarId.None);

            _gameFlow.ChangeState(GameState.DerbyMatch);

            return cars;
        }

        /// <summary>
        /// Тестовый шов: запускает отслеживание участников без реального спавна.
        /// Не входит в интерфейс — вызывается напрямую только юнит-тестами.
        /// </summary>
        public void BeginTracking(IReadOnlyList<CarId> participants, CarId playerId)
        {
            _aliveCars    = new HashSet<CarId>(participants);
            _playerCarId  = playerId;
            _playerKills  = 0;
            _active       = true;
            _startTime    = Time.time;
        }

        private void OnCarDestroyed(CarDestroyedEvent e)
        {
            if (!_active) return;

            if (e.Killer == _playerCarId && e.Killer != e.Target)
                _playerKills++;

            _aliveCars.Remove(e.Target);

            if (_aliveCars.Count <= 1)
                EndMatch();
        }

        private void EndMatch()
        {
            _active = false;

            // Определяем победителя без LINQ: если жив ровно один участник — это он.
            CarId winner = CarId.None;
            if (_aliveCars.Count == 1)
            {
                using (var e = _aliveCars.GetEnumerator())
                {
                    if (e.MoveNext())
                        winner = e.Current;
                }
            }

            float duration = Time.time - _startTime;
            int coins = _playerKills * _rewardConfig.CoinsPerKill
                + (winner == _playerCarId ? _rewardConfig.WinBonus : 0);

            _eventBus.Publish(new DerbyMatchEndedEvent(winner, _playerKills, duration));
            _eventBus.Publish(new RewardEarnedEvent(RewardSource.Derby, coins, 0));

            _gameFlow.ChangeState(GameState.MatchEnd);
        }

        public void Dispose()
        {
            _eventBus.Unsubscribe<CarDestroyedEvent>(OnCarDestroyed);
        }
    }
}
