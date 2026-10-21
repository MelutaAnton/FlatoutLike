using System.Collections.Generic;
using System.Reflection;
using FlatOutLike.Controllers.Car;
using FlatOutLike.Core.Events;
using FlatOutLike.Data.Configs.Audio;
using FlatOutLike.Models;
using FlatOutLike.Services.Audio;
using FlatOutLike.Services.Flow;
using FlatOutLike.Services.Match;
using NUnit.Framework;
using UnityEngine;

namespace FlatOutLike.Tests
{
    /// <summary>
    /// Юнит-тесты SFX стройки в <see cref="AudioService"/> (шаг 12b): тик на каждое списание пачки,
    /// звук промежуточного этапа, отсутствие звука этапа на финальном и фанфара завершения.
    /// Кулдаун в записях = 0, чтобы Time.unscaledTime (в EditMode может не идти) не влиял на ассерты.
    /// </summary>
    [TestFixture]
    public class AudioServiceBuildSfxTests
    {
        private EventBus _bus;
        private AudioConfig _config;
        private FakeAudioPlayer _player;
        private AudioService _service;
        private AudioClip _tickClip;
        private AudioClip _stageClip;
        private AudioClip _completedClip;

        [SetUp]
        public void SetUp()
        {
            _bus = new EventBus();
            _player = new FakeAudioPlayer();

            _tickClip = AudioClip.Create("tick", 16, 1, 8000, false);
            _stageClip = AudioClip.Create("stage", 16, 1, 8000, false);
            _completedClip = AudioClip.Create("completed", 16, 1, 8000, false);

            _config = ScriptableObject.CreateInstance<AudioConfig>();
            var entries = new[]
            {
                MakeEntry(SfxId.BuildInvestTick, _tickClip),
                MakeEntry(SfxId.BuildStageComplete, _stageClip),
                MakeEntry(SfxId.BuildingCompleted, _completedClip)
            };
            typeof(AudioConfig)
                .GetField("_sfxEntries", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(_config, entries);

            _service = new AudioService(_bus, _config, _player, new FakeCarRegistry(),
                new FakeGameFlowService(), new FakeSceneLoaderService());
        }

        [TearDown]
        public void TearDown()
        {
            _service.Dispose();
            Object.DestroyImmediate(_config);
            Object.DestroyImmediate(_tickClip);
            Object.DestroyImmediate(_stageClip);
            Object.DestroyImmediate(_completedClip);
        }

        [Test]
        public void InvestProgress_PlaysTickForEachEvent()
        {
            _bus.Publish(new BuildingInvestProgressEvent(BuildingId.Garage, 0, 10, 100));
            _bus.Publish(new BuildingInvestProgressEvent(BuildingId.Garage, 0, 20, 100));
            _bus.Publish(new BuildingInvestProgressEvent(BuildingId.Garage, 0, 30, 100));

            Assert.AreEqual(3, _player.CountOf(_tickClip));
            Assert.AreEqual(0, _player.CountOf(_stageClip));
            Assert.AreEqual(0, _player.CountOf(_completedClip));
        }

        [Test]
        public void StageInvested_Intermediate_PlaysStageSound()
        {
            _bus.Publish(new BuildingStageInvestedEvent(BuildingId.Garage, 1, 3));

            Assert.AreEqual(1, _player.CountOf(_stageClip));
        }

        [Test]
        public void StageInvested_Final_DoesNotPlayStageSound()
        {
            _bus.Publish(new BuildingStageInvestedEvent(BuildingId.Garage, 3, 3));

            Assert.AreEqual(0, _player.CountOf(_stageClip));
        }

        [Test]
        public void BuildingCompleted_PlaysFanfare()
        {
            _bus.Publish(new BuildingCompletedEvent(BuildingId.Garage));

            Assert.AreEqual(1, _player.CountOf(_completedClip));
        }

        [Test]
        public void AfterDispose_NoSoundsOnBuildEvents()
        {
            _service.Dispose();

            _bus.Publish(new BuildingInvestProgressEvent(BuildingId.Garage, 0, 10, 100));
            _bus.Publish(new BuildingStageInvestedEvent(BuildingId.Garage, 1, 3));
            _bus.Publish(new BuildingCompletedEvent(BuildingId.Garage));

            Assert.AreEqual(0, _player.Total);
        }

        private static AudioConfig.SfxEntry MakeEntry(SfxId id, AudioClip clip) => new AudioConfig.SfxEntry
        {
            Id = id,
            Clips = new[] { clip },
            Volume = 1f,
            PitchJitter = 0f,
            CooldownSeconds = 0f
        };

        // ── Фейки ───────────────────────────────────────────────────────────────

        /// <summary>Считает one-shot'ы по клипам.</summary>
        private sealed class FakeAudioPlayer : IAudioPlayer
        {
            private readonly Dictionary<AudioClip, int> _counts = new Dictionary<AudioClip, int>();

            public int Total { get; private set; }

            public int CountOf(AudioClip clip) => _counts.TryGetValue(clip, out var n) ? n : 0;

            public void Initialize(int oneShotSourcesCount) { }

            public void PlayOneShot(AudioClip clip, float volume, float pitch)
            {
                _counts[clip] = CountOf(clip) + 1;
                Total++;
            }

            public void StartLoop(AudioClip clip, float volume) { }
            public void StopLoop() { }
            public void PlayMusic(AudioClip clip, float volume, float fadeSeconds) { }
            public void StopMusic(float fadeSeconds) { }
            public void SetPaused(bool paused) { }
        }

        /// <summary>Пустой реестр: тестируемые события его не читают.</summary>
        private sealed class FakeCarRegistry : ICarRegistry
        {
            public IReadOnlyList<CarController> Cars => new List<CarController>();
            public CarController Player => null;

            public bool TryGetPlayerCarId(out CarId carId)
            {
                carId = default;
                return false;
            }

            public void Register(CarController car, bool isPlayer) { }
            public void Unregister(CarController car) { }
            public void RegisterName(CarId id, string displayName) { }

            public bool TryGetName(CarId id, out string displayName)
            {
                displayName = null;
                return false;
            }

            public void Clear() { }
        }

        private sealed class FakeGameFlowService : IGameFlowService
        {
            public GameState Current => GameState.BaseHub;

            public void ChangeState(GameState next) { }
            public void Push(GameState next) { }
            public void Pop() { }
            public void Tick() { }
        }

        private sealed class FakeSceneLoaderService : ISceneLoaderService
        {
            public string ActiveSceneName => string.Empty;
            public bool IsLoading => false;

            public void Load(string sceneKey) { }
        }
    }
}
