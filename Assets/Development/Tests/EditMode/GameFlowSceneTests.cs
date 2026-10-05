using System.Collections.Generic;
using FlatOutLike.Core.Events;
using FlatOutLike.Models;
using FlatOutLike.Services.Flow;
using NUnit.Framework;

namespace FlatOutLike.Tests
{
    /// <summary>
    /// Юнит-тесты подшага 8b: привязка GameState к сцене (GameStateSceneMap),
    /// поведение SceneEntryState/PassiveState внутри GameFlowService, guard от
    /// повторной загрузки уже активной сцены, публикация GameStateChangedEvent.
    /// Использует FakeSceneLoaderService — реальный SceneManager в EditMode-тестах не трогаем.
    /// </summary>
    [TestFixture]
    public class GameFlowSceneTests
    {
        private EventBus _bus;
        private FakeSceneLoaderService _loader;

        [SetUp]
        public void SetUp()
        {
            _bus = new EventBus();
            _loader = new FakeSceneLoaderService();
        }

        // ── GameStateSceneMap.InitialStateFor ──────────────────────────────────────

        [Test]
        public void InitialStateFor_Boot_ReturnsBaseHub()
        {
            Assert.AreEqual(GameState.BaseHub, GameStateSceneMap.InitialStateFor(SceneKeys.Boot));
        }

        [Test]
        public void InitialStateFor_BaseHub_ReturnsBaseHub()
        {
            Assert.AreEqual(GameState.BaseHub, GameStateSceneMap.InitialStateFor(SceneKeys.BaseHub));
        }

        [Test]
        public void InitialStateFor_Garage_ReturnsGarageMeta()
        {
            Assert.AreEqual(GameState.GarageMeta, GameStateSceneMap.InitialStateFor(SceneKeys.Garage));
        }

        [Test]
        public void InitialStateFor_DerbyArena01_ReturnsDerbyMatch()
        {
            Assert.AreEqual(GameState.DerbyMatch, GameStateSceneMap.InitialStateFor(SceneKeys.DerbyArena01));
        }

        [Test]
        public void InitialStateFor_StuntsHighJump_ReturnsStuntsHighJumpDriving()
        {
            Assert.AreEqual(GameState.StuntsHighJumpDriving, GameStateSceneMap.InitialStateFor(SceneKeys.StuntsHighJump));
        }

        [Test]
        public void InitialStateFor_UnknownScene_ReturnsBaseHubWithoutThrowing()
        {
            Assert.AreEqual(GameState.BaseHub, GameStateSceneMap.InitialStateFor("_TestSandbox"));
        }

        [Test]
        public void SceneFor_BaseHub_ReturnsBaseHubScene()
        {
            Assert.AreEqual(SceneKeys.BaseHub, GameStateSceneMap.SceneFor(GameState.BaseHub));
        }

        // ── ChangeState грузит сцену ────────────────────────────────────────────────

        [Test]
        public void ChangeState_DerbyMatchToBaseHub_LoadsBaseHub()
        {
            // Возврат из режима (Leave/Continue) ведёт в BaseHub — отдельная сцена хаба.
            _loader.ActiveSceneName = SceneKeys.DerbyArena01;
            var flow = new GameFlowService(_bus, _loader, GameState.DerbyMatch);

            flow.ChangeState(GameState.BaseHub);

            Assert.AreEqual(1, _loader.LoadCalls.Count);
            Assert.AreEqual(SceneKeys.BaseHub, _loader.LoadCalls[0]);
        }

        [Test]
        public void ChangeState_BaseHubToDerbyMatch_LoadsDerbyArena01()
        {
            _loader.ActiveSceneName = SceneKeys.BaseHub;
            var flow = new GameFlowService(_bus, _loader, GameState.BaseHub);

            flow.ChangeState(GameState.DerbyMatch);

            Assert.AreEqual(1, _loader.LoadCalls.Count);
            Assert.AreEqual(SceneKeys.DerbyArena01, _loader.LoadCalls[0]);
        }

        [Test]
        public void ChangeState_MatchEndToGarageMeta_LoadsGarage()
        {
            // MatchEnd — PassiveState (живёт внутри Derby-сцены), стартуем с него напрямую.
            _loader.ActiveSceneName = SceneKeys.DerbyArena01;
            var flow = new GameFlowService(_bus, _loader, GameState.MatchEnd);

            flow.ChangeState(GameState.GarageMeta);

            Assert.AreEqual(1, _loader.LoadCalls.Count);
            Assert.AreEqual(SceneKeys.Garage, _loader.LoadCalls[0]);
        }

        [Test]
        public void Constructor_InBootScene_ImmediatelyLoadsBaseHub()
        {
            // Boot-сценарий: scope стартует в Boot с initial=BaseHub — Enter начального
            // состояния срабатывает синхронно в ctor и обязан сразу увести в BaseHub.
            _loader.ActiveSceneName = SceneKeys.Boot;

            var flow = new GameFlowService(_bus, _loader, GameState.BaseHub);

            Assert.AreEqual(1, _loader.LoadCalls.Count);
            Assert.AreEqual(SceneKeys.BaseHub, _loader.LoadCalls[0]);
            Assert.AreEqual(GameState.BaseHub, flow.Current);
        }

        [Test]
        public void StuntsChain_BaseHubToDrivingToFlightToScoring_LoadsOnlyHighJumpScene()
        {
            // Полная Stunts-цепочка §9: BaseHub → Driving (загрузка сцены) → Flight →
            // Scoring (оба — PassiveState внутри той же сцены, загрузок больше нет).
            _loader.ActiveSceneName = SceneKeys.BaseHub;
            var flow = new GameFlowService(_bus, _loader, GameState.BaseHub);

            flow.ChangeState(GameState.StuntsHighJumpDriving);
            Assert.AreEqual(1, _loader.LoadCalls.Count);
            Assert.AreEqual(SceneKeys.StuntsHighJump, _loader.LoadCalls[0]);

            // Дальше игрок уже в сцене HighJump — имитируем смену активной сцены.
            _loader.ActiveSceneName = SceneKeys.StuntsHighJump;
            flow.ChangeState(GameState.StuntsHighJumpFlight);
            flow.ChangeState(GameState.StuntsHighJumpScoring);

            Assert.AreEqual(1, _loader.LoadCalls.Count, "Flight/Scoring живут внутри сцены HighJump — новых загрузок нет.");
            Assert.AreEqual(GameState.StuntsHighJumpScoring, flow.Current);
        }

        [Test]
        public void Enter_WhenSceneAlreadyActive_DoesNotCallLoad()
        {
            // Scope стартует прямо в Derby_Arena_01 в состоянии DerbyMatch — Enter не должен
            // перезагружать сцену, в которой уже находимся (иначе бесконечный ре-старт scope).
            _loader.ActiveSceneName = SceneKeys.DerbyArena01;
            var flow = new GameFlowService(_bus, _loader, GameState.DerbyMatch);

            Assert.AreEqual(0, _loader.LoadCalls.Count);
            Assert.AreEqual(GameState.DerbyMatch, flow.Current);
        }

        [Test]
        public void ChangeState_ToPassiveState_InSameScene_DoesNotCallLoad()
        {
            // DerbyMatch → MatchEnd: MatchEnd — PassiveState без своей сцены, живёт внутри
            // текущей арены. Переход не должен трогать загрузчик сцен.
            _loader.ActiveSceneName = SceneKeys.DerbyArena01;
            var flow = new GameFlowService(_bus, _loader, GameState.DerbyMatch);

            flow.ChangeState(GameState.MatchEnd);

            Assert.AreEqual(0, _loader.LoadCalls.Count);
        }

        // ── Push/Pop не трогают загрузчик ───────────────────────────────────────────

        [Test]
        public void Push_Paused_DoesNotCallLoad()
        {
            _loader.ActiveSceneName = SceneKeys.DerbyArena01;
            var flow = new GameFlowService(_bus, _loader, GameState.DerbyMatch);

            flow.Push(GameState.Paused);

            Assert.AreEqual(0, _loader.LoadCalls.Count);
            Assert.AreEqual(GameState.Paused, flow.Current);
        }

        [Test]
        public void Pop_AfterPush_DoesNotCallLoad()
        {
            _loader.ActiveSceneName = SceneKeys.DerbyArena01;
            var flow = new GameFlowService(_bus, _loader, GameState.DerbyMatch);
            flow.Push(GameState.Paused);

            flow.Pop();

            Assert.AreEqual(0, _loader.LoadCalls.Count);
            Assert.AreEqual(GameState.DerbyMatch, flow.Current);
        }

        // ── GameStateChangedEvent ────────────────────────────────────────────────────

        [Test]
        public void ChangeState_PublishesGameStateChangedEvent_WithCorrectFromTo()
        {
            _loader.ActiveSceneName = SceneKeys.BaseHub;
            var flow = new GameFlowService(_bus, _loader, GameState.BaseHub);

            var received = new List<GameStateChangedEvent>();
            _bus.Subscribe<GameStateChangedEvent>(received.Add);

            flow.ChangeState(GameState.DerbyMatch);

            Assert.AreEqual(1, received.Count);
            Assert.AreEqual(GameState.BaseHub, received[0].From);
            Assert.AreEqual(GameState.DerbyMatch, received[0].To);
        }

        [Test]
        public void Push_PublishesGameStateChangedEvent_WithCorrectFromTo()
        {
            _loader.ActiveSceneName = SceneKeys.DerbyArena01;
            var flow = new GameFlowService(_bus, _loader, GameState.DerbyMatch);

            var received = new List<GameStateChangedEvent>();
            _bus.Subscribe<GameStateChangedEvent>(received.Add);

            flow.Push(GameState.Paused);

            Assert.AreEqual(1, received.Count);
            Assert.AreEqual(GameState.DerbyMatch, received[0].From);
            Assert.AreEqual(GameState.Paused, received[0].To);
        }

        // ── Фейк ──────────────────────────────────────────────────────────────────

        /// <summary>
        /// Фейковый ISceneLoaderService: записывает все вызовы Load, ActiveSceneName настраивается
        /// тестом вручную (реальный SceneManager в EditMode не трогаем — сцену не загружаем физически).
        /// </summary>
        private sealed class FakeSceneLoaderService : ISceneLoaderService
        {
            public string ActiveSceneName { get; set; }
            public readonly List<string> LoadCalls = new List<string>();

            public void Load(string sceneKey)
            {
                if (ActiveSceneName == sceneKey)
                    return;

                LoadCalls.Add(sceneKey);
            }
        }
    }
}
