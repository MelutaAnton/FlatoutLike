using System;
using System.Collections.Generic;
using FlatOutLike.Controllers.Car;
using FlatOutLike.Core.Events;
using FlatOutLike.Data.Configs.Match;
using FlatOutLike.Data.Configs.Rewards;
using FlatOutLike.Factories.Car;
using FlatOutLike.Models;
using FlatOutLike.Services.Flow;
using FlatOutLike.Services.Match;
using NUnit.Framework;
using UnityEngine;

namespace FlatOutLike.Tests
{
    /// <summary>
    /// Юнит-тесты <see cref="DerbyMatchService"/>: жизненный цикл матча, подсчёт киллов игрока,
    /// начисление наград и идемпотентность завершения.
    ///
    /// Драйвится через тестовый шов <c>BeginTracking</c> (не входит в интерфейс) —
    /// реальный <c>ICarFactory.SpawnMatch</c> в тестах не вызывается.
    /// Использует настоящий <see cref="EventBus"/> (pure C#, тестируем без Unity).
    /// </summary>
    [TestFixture]
    public class DerbyMatchServiceTests
    {
        private EventBus _bus;
        private FakeGameFlowService _gameFlow;
        private FakeCarFactory _factory;
        private DerbyMatchConfig _matchConfig;
        private RewardConfig _rewardConfig;
        private DerbyMatchService _service;

        private static readonly CarId Player = new CarId(0);
        private static readonly CarId EnemyA = new CarId(1);
        private static readonly CarId EnemyB = new CarId(2);

        // Публикации событий, пойманные тестом.
        private readonly List<DerbyMatchEndedEvent> _endedEvents = new List<DerbyMatchEndedEvent>();
        private readonly List<RewardEarnedEvent> _rewardEvents = new List<RewardEarnedEvent>();

        [SetUp]
        public void SetUp()
        {
            _bus = new EventBus();
            _gameFlow = new FakeGameFlowService();
            _factory = new FakeCarFactory();
            _matchConfig = ScriptableObject.CreateInstance<DerbyMatchConfig>();
            _rewardConfig = ScriptableObject.CreateInstance<RewardConfig>();

            _service = new DerbyMatchService(_factory, _bus, _gameFlow, _matchConfig, _rewardConfig);

            _endedEvents.Clear();
            _rewardEvents.Clear();
            _bus.Subscribe<DerbyMatchEndedEvent>(_endedEvents.Add);
            _bus.Subscribe<RewardEarnedEvent>(_rewardEvents.Add);
        }

        [TearDown]
        public void TearDown()
        {
            _service.Dispose();
            _bus.Unsubscribe<DerbyMatchEndedEvent>(_endedEvents.Add);
            _bus.Unsubscribe<RewardEarnedEvent>(_rewardEvents.Add);

            // EditMode: DestroyImmediate обязателен для ScriptableObject-инстансов.
            // Полная квалификация UnityEngine.Object — иначе CS0104 (конфликт с System.Object из using System).
            UnityEngine.Object.DestroyImmediate(_matchConfig);
            UnityEngine.Object.DestroyImmediate(_rewardConfig);
        }

        // ── Тесты ───────────────────────────────────────────────────────────────

        // a) Победа — последний живой участник ловится как Winner, GameFlow переходит в MatchEnd
        [Test]
        public void LastAliveCar_EndsMatch_WithWinnerAndMatchEndState()
        {
            _service.BeginTracking(new List<CarId> { Player, EnemyA, EnemyB }, Player);

            _bus.Publish(new CarDestroyedEvent(EnemyA, Player));
            _bus.Publish(new CarDestroyedEvent(EnemyB, Player));

            Assert.AreEqual(1, _endedEvents.Count, "Матч должен завершиться ровно один раз.");
            Assert.AreEqual(Player, _endedEvents[0].Winner, "Победителем должен стать последний живой — игрок.");
            Assert.AreEqual(GameState.MatchEnd, _gameFlow.LastChanged, "GameFlow должен перейти в MatchEnd.");
        }

        // b) Киллы игрока считаются, чужие киллы — нет
        [Test]
        public void PlayerKills_AreCounted_EnemyKillsAreNot()
        {
            _service.BeginTracking(new List<CarId> { Player, EnemyA, EnemyB }, Player);

            // Игрок убивает EnemyA — считается.
            _bus.Publish(new CarDestroyedEvent(EnemyA, Player));
            // EnemyB убивает... себя же не может, но проверим что чужой килл (не игрока) не считается,
            // добавив ещё одного участника, погибающего не от руки игрока.
            _bus.Publish(new CarDestroyedEvent(EnemyB, EnemyA));

            Assert.AreEqual(1, _endedEvents.Count);
            Assert.AreEqual(1, _endedEvents[0].PlayerKills,
                "Должен засчитаться только килл, где Killer == playerId, а не чужой килл EnemyA->EnemyB.");
        }

        // c) Reward = kills*10 + 50 при победе игрока
        [Test]
        public void Reward_EqualsKillsTimesCoinsPerKill_PlusWinBonus_WhenPlayerWins()
        {
            _service.BeginTracking(new List<CarId> { Player, EnemyA, EnemyB }, Player);

            _bus.Publish(new CarDestroyedEvent(EnemyA, Player));
            _bus.Publish(new CarDestroyedEvent(EnemyB, Player));

            Assert.AreEqual(1, _rewardEvents.Count);
            int expectedCoins = 2 * _rewardConfig.CoinsPerKill + _rewardConfig.WinBonus;
            Assert.AreEqual(expectedCoins, _rewardEvents[0].CoinAmount,
                "2 килла игрока * CoinsPerKill + WinBonus (игрок — победитель).");
            Assert.AreEqual(RewardSource.Derby, _rewardEvents[0].Source);
        }

        // d) Идемпотентность — лишние события после End не рефайрят второй DerbyMatchEndedEvent
        [Test]
        public void EventsAfterMatchEnd_DoNotRefireEndedEvent()
        {
            _service.BeginTracking(new List<CarId> { Player, EnemyA }, Player);

            _bus.Publish(new CarDestroyedEvent(EnemyA, Player)); // матч завершается (остался 1 живой)
            Assert.AreEqual(1, _endedEvents.Count);

            // Лишнее событие после завершения матча — не должно ничего изменить.
            _bus.Publish(new CarDestroyedEvent(Player, EnemyA));

            Assert.AreEqual(1, _endedEvents.Count,
                "После завершения матча сервис должен игнорировать дальнейшие CarDestroyedEvent.");
            Assert.AreEqual(1, _rewardEvents.Count);
        }

        // e) Смерть игрока при >1 живых не завершает матч
        [Test]
        public void PlayerDeath_WithMultipleAliveRemaining_DoesNotEndMatch()
        {
            _service.BeginTracking(new List<CarId> { Player, EnemyA, EnemyB }, Player);

            _bus.Publish(new CarDestroyedEvent(Player, EnemyA));

            Assert.AreEqual(0, _endedEvents.Count,
                "Пока живы двое (EnemyA, EnemyB) — матч не должен завершаться.");
            Assert.AreNotEqual(GameState.MatchEnd, _gameFlow.LastChanged);
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

        /// <summary>
        /// Фейковая фабрика — в этих тестах StartMatch не вызывается (используем BeginTracking),
        /// поэтому SpawnMatch не реализован по-настоящему.
        /// </summary>
        private sealed class FakeCarFactory : ICarFactory
        {
            public IReadOnlyList<CarController> SpawnMatch(DerbyMatchConfig config, IReadOnlyList<Transform> spawnPoints)
                => throw new NotImplementedException("В этих тестах используется BeginTracking, а не StartMatch.");
        }
    }
}
