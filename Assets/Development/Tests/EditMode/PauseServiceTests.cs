using System.Collections.Generic;
using FlatOutLike.Core.Events;
using FlatOutLike.Core.Input;
using FlatOutLike.Models;
using FlatOutLike.Services.Flow;
using NUnit.Framework;
using UnityEngine;

namespace FlatOutLike.Tests
{
    /// <summary>
    /// Юнит-тесты <see cref="PauseService"/> (Шаг 10a): переключение GameFlow по PausePressed
    /// и синхронизация Time.timeScale с GamePausedEvent/GameResumedEvent/GameStateChangedEvent.
    /// FakeGameFlowService здесь — стековая реализация (образец HighJumpSessionServiceTests
    /// расширен под Push/Pop, т.к. PauseService явно опирается на стек состояний).
    /// </summary>
    [TestFixture]
    public class PauseServiceTests
    {
        private EventBus _bus;
        private FakeInputProvider _input;
        private FakeGameFlowService _flow;
        private PauseService _service;

        [SetUp]
        public void SetUp()
        {
            _bus = new EventBus();
            _input = new FakeInputProvider();
            _flow = new FakeGameFlowService(GameState.DerbyMatch);
            _service = new PauseService(_input, _flow, _bus);
        }

        [TearDown]
        public void TearDown()
        {
            _service.Dispose();
            Time.timeScale = 1f; // страховка теста: не протечь в соседние тесты
        }

        // ── Tick: переключение по PausePressed ────────────────────────────────────

        [Test]
        public void Tick_PausePressedInDerbyMatch_PushesPaused()
        {
            _input.PausePressed = true;

            _service.Tick();

            Assert.AreEqual(GameState.Paused, _flow.Current);
        }

        [Test]
        public void Tick_PausePressedInHighJumpDriving_PushesPaused()
        {
            ReplaceService(GameState.StuntsHighJumpDriving);
            _input.PausePressed = true;

            _service.Tick();

            Assert.AreEqual(GameState.Paused, _flow.Current);
        }

        [Test]
        public void Tick_PausePressedInHighJumpFlight_PushesPaused()
        {
            ReplaceService(GameState.StuntsHighJumpFlight);
            _input.PausePressed = true;

            _service.Tick();

            Assert.AreEqual(GameState.Paused, _flow.Current);
        }

        [Test]
        public void Tick_PausePressedWhilePaused_Pops()
        {
            _flow.Push(GameState.Paused);
            _input.PausePressed = true;

            _service.Tick();

            Assert.AreEqual(GameState.DerbyMatch, _flow.Current);
        }

        [Test]
        public void Tick_PausePressedInMainMenu_Ignored()
        {
            ReplaceService(GameState.MainMenu);
            _input.PausePressed = true;

            _service.Tick();

            Assert.AreEqual(GameState.MainMenu, _flow.Current);
        }

        [Test]
        public void Tick_PauseNotPressed_DoesNothing()
        {
            _input.PausePressed = false;

            _service.Tick();

            Assert.AreEqual(GameState.DerbyMatch, _flow.Current);
        }

        // ── Time.timeScale ──────────────────────────────────────────────────────

        [Test]
        public void GamePausedEvent_SetsTimeScaleToZero()
        {
            _bus.Publish(new GamePausedEvent());

            Assert.AreEqual(0f, Time.timeScale);
        }

        [Test]
        public void GameResumedEvent_SetsTimeScaleToOne()
        {
            Time.timeScale = 0f;

            _bus.Publish(new GameResumedEvent());

            Assert.AreEqual(1f, Time.timeScale);
        }

        [Test]
        public void GameStateChangedEvent_ToNonPaused_ResetsTimeScaleToOne()
        {
            // Страховка: timeScale — глобальный static, переживает загрузку сцены. Переход
            // в любое состояние, кроме Paused, обязан восстановить его даже без GameResumedEvent.
            Time.timeScale = 0f;

            _bus.Publish(new GameStateChangedEvent(GameState.Paused, GameState.MainMenu));

            Assert.AreEqual(1f, Time.timeScale);
        }

        [Test]
        public void Constructor_AlwaysResetsTimeScaleToOne()
        {
            Time.timeScale = 0f;

            var service = new PauseService(_input, _flow, _bus);

            Assert.AreEqual(1f, Time.timeScale);
            service.Dispose();
        }

        // ── Хелпер ──────────────────────────────────────────────────────────────

        private void ReplaceService(GameState initial)
        {
            _service.Dispose();
            _flow = new FakeGameFlowService(initial);
            _service = new PauseService(_input, _flow, _bus);
        }

        // ── Фейки ───────────────────────────────────────────────────────────────

        private sealed class FakeInputProvider : IInputProvider
        {
            public InputContext CurrentContext { get; set; }
            public Vector2 Move => Vector2.zero;
            public Vector2 Look => Vector2.zero;
            public bool Handbrake => false;
            public bool Nitro => false;
            public bool EjectPressed => false;
            public bool MidairBoostPressed => false;
            public bool PausePressed { get; set; }

            public void Tick() { }
        }

        /// <summary>Стековая реализация IGameFlowService — PauseService опирается на Push/Pop/Current.</summary>
        private sealed class FakeGameFlowService : IGameFlowService
        {
            private readonly Stack<GameState> _stack = new Stack<GameState>();

            public GameState Current => _stack.Peek();

            public FakeGameFlowService(GameState initial) => _stack.Push(initial);

            public void ChangeState(GameState next)
            {
                _stack.Pop();
                _stack.Push(next);
            }

            public void Push(GameState next) => _stack.Push(next);

            public void Pop()
            {
                if (_stack.Count > 1) _stack.Pop();
            }

            public void Tick() { }
        }
    }
}
