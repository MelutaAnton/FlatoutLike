using System.Collections.Generic;
using FlatOutLike.Core.Events;
using FlatOutLike.Data.Configs.Stunts;
using FlatOutLike.Models;
using FlatOutLike.Services.Meta;
using FlatOutLike.Services.Stunts;
using NUnit.Framework;
using UnityEngine;

namespace FlatOutLike.Tests
{
    /// <summary>
    /// Юнит-тесты <see cref="HighJumpScoreService"/>: формула очков, обновление личного
    /// рекорда профиля и начисление монет за высоту.
    /// Fake IMetaProgressService/IPlayerProfile — простые нест-классы (без PlayerPrefs).
    /// </summary>
    [TestFixture]
    public class HighJumpScoreServiceTests
    {
        private EventBus _bus;
        private HighJumpRulesConfig _rules;
        private FakeMetaProgressService _meta;
        private HighJumpScoreService _service;

        private readonly List<StuntsScoredEvent> _scoredEvents = new List<StuntsScoredEvent>();
        private readonly List<RewardEarnedEvent> _rewardEvents = new List<RewardEarnedEvent>();

        [SetUp]
        public void SetUp()
        {
            _bus = new EventBus();
            _rules = ScriptableObject.CreateInstance<HighJumpRulesConfig>(); // HeightToCoinRatio=5
            _meta = new FakeMetaProgressService();
            _service = new HighJumpScoreService(_rules, _meta, _bus);

            _scoredEvents.Clear();
            _rewardEvents.Clear();
            _bus.Subscribe<StuntsScoredEvent>(_scoredEvents.Add);
            _bus.Subscribe<RewardEarnedEvent>(_rewardEvents.Add);
        }

        [TearDown]
        public void TearDown()
        {
            _bus.Unsubscribe<StuntsScoredEvent>(_scoredEvents.Add);
            _bus.Unsubscribe<RewardEarnedEvent>(_rewardEvents.Add);
            UnityEngine.Object.DestroyImmediate(_rules);
        }

        // a) Результат попытки = пиковая ВЫСОТА (композитный счёт удалён решением геймдизайна);
        //    флипы ни на что не влияют.
        [Test]
        public void ScoreAttempt_PublishesHeightAsResult_FlipsIgnored()
        {
            var landed = new DriverLandedEvent(peakHeight: 12f, flipCount: 2, totalAirTime: 3f);

            _service.ScoreAttempt(landed);

            Assert.AreEqual(1, _scoredEvents.Count);
            Assert.AreEqual(StuntType.HighJump, _scoredEvents[0].Type);
            Assert.AreEqual(12f, _scoredEvents[0].Height, 0.001f, "Результат — высота в метрах, флипы не участвуют.");
        }

        // b) Новый рекорд ВЫСОТЫ обновляет профиль, сохраняет и isNewRecord == true
        [Test]
        public void ScoreAttempt_WithHigherPeak_UpdatesHeightRecord_AndSaves()
        {
            var landed = new DriverLandedEvent(peakHeight: 12f, flipCount: 2, totalAirTime: 3f);

            _service.ScoreAttempt(landed);

            Assert.AreEqual(12f, _meta.Profile.BestHighJumpHeight, 0.001f);
            Assert.AreEqual(1, _meta.SaveCallCount);
            Assert.IsTrue(_scoredEvents[0].IsNewRecord);
        }

        // c) Меньшая высота не обновляет рекорд, isNewRecord == false —
        //    сколько бы флипов ни было (высота — единственная метрика)
        [Test]
        public void ScoreAttempt_WithLowerPeak_DoesNotUpdateRecord()
        {
            _service.ScoreAttempt(new DriverLandedEvent(peakHeight: 12f, flipCount: 0, totalAirTime: 3f)); // 12 м — рекорд
            _scoredEvents.Clear();

            _service.ScoreAttempt(new DriverLandedEvent(peakHeight: 10f, flipCount: 5, totalAirTime: 3f));

            Assert.AreEqual(12f, _meta.Profile.BestHighJumpHeight, 0.001f, "Рекорд высоты не должен уменьшиться.");
            Assert.AreEqual(1, _meta.SaveCallCount, "Save не должен вызываться повторно без нового рекорда.");
            Assert.IsFalse(_scoredEvents[0].IsNewRecord, "Флипы не двигают рекорд высоты.");
        }

        // d) RewardEarnedEvent: монеты = Round(PeakHeight * HeightToCoinRatio), источник HighJump
        [Test]
        public void ScoreAttempt_PublishesReward_WithCoinsByHeightRatio()
        {
            _service.ScoreAttempt(new DriverLandedEvent(peakHeight: 12f, flipCount: 2, totalAirTime: 3f));

            Assert.AreEqual(1, _rewardEvents.Count);
            Assert.AreEqual(RewardSource.HighJump, _rewardEvents[0].Source);
            Assert.AreEqual(60, _rewardEvents[0].CoinAmount, "12 * 5 = 60.");
            Assert.AreEqual(0, _rewardEvents[0].XpAmount);
        }

        // ── Фейки ───────────────────────────────────────────────────────────────

        private sealed class FakePlayerProfile : IPlayerProfile
        {
            public int Coins { get; set; }
            public float BestHighJumpHeight { get; set; }
            public string SelectedCarId { get; set; } = string.Empty;

            private readonly HashSet<string> _ownedCarIds = new HashSet<string>();
            private readonly Dictionary<(string, UpgradeCategory), int> _upgradeLevels = new Dictionary<(string, UpgradeCategory), int>();

            public IReadOnlyCollection<string> OwnedCarIds => _ownedCarIds;

            public bool OwnsCar(string carId) => _ownedCarIds.Contains(carId);
            public void AddOwnedCar(string carId) => _ownedCarIds.Add(carId);

            public int GetUpgradeLevel(string carId, UpgradeCategory category)
            {
                if (string.IsNullOrEmpty(carId))
                    return 0;
                return _upgradeLevels.TryGetValue((carId, category), out var level) ? level : 0;
            }

            public void SetUpgradeLevel(string carId, UpgradeCategory category, int level)
            {
                if (string.IsNullOrEmpty(carId))
                    return;
                _upgradeLevels[(carId, category)] = level;
            }

            public IReadOnlyCollection<(string CarId, UpgradeCategory Category, int Level)> GetAllUpgradeLevels()
            {
                var result = new List<(string CarId, UpgradeCategory Category, int Level)>(_upgradeLevels.Count);
                foreach (var kvp in _upgradeLevels)
                    result.Add((kvp.Key.Item1, kvp.Key.Item2, kvp.Value));
                return result;
            }
        }

        private sealed class FakeMetaProgressService : IMetaProgressService
        {
            public IPlayerProfile Profile { get; } = new FakePlayerProfile();
            public int SaveCallCount { get; private set; }

            public void Save() => SaveCallCount++;
        }
    }
}
