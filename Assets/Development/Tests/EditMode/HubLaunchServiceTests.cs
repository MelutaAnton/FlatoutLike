using System.Collections.Generic;
using System.Reflection;
using FlatOutLike.Data.Configs.Progression;
using FlatOutLike.Models;
using FlatOutLike.Services.Flow;
using FlatOutLike.Services.Hub;
using FlatOutLike.Services.Progression;
using NUnit.Framework;
using UnityEngine;

namespace FlatOutLike.Tests
{
    /// <summary>
    /// Юнит-тесты <see cref="HubLaunchService"/> (Шаг 11b): таймер подтверждения зоны запуска,
    /// сброс при выходе, перекрытие зон, игнор недоступных зон и остановка времени вне BaseHub.
    /// Каталог — настоящий BuildingCatalogConfig (CreateInstance), приватные поля заполняются
    /// через reflection (образец CameraServiceTests.SetPrivateField).
    /// Значения времени — двоично-точные (0.25/0.5/0.75), чтобы порог не зависел от округления float.
    /// </summary>
    [TestFixture]
    public class HubLaunchServiceTests
    {
        private const float ConfirmSeconds = 1f;

        private BuildingCatalogConfig _catalog;
        private FakeGameFlowService _flow;
        private FakeBuildProgressService _build;
        private HubLaunchService _service;

        [SetUp]
        public void SetUp()
        {
            _catalog = ScriptableObject.CreateInstance<BuildingCatalogConfig>();
            SetPrivateField(_catalog, "_launchConfirmSeconds", ConfirmSeconds);
            SetPrivateField(_catalog, "_entries", new[]
            {
                Entry(BuildingId.DerbyArena, enabled: true, hasLaunchZone: true, GameState.DerbyMatch),
                Entry(BuildingId.HighJumpTower, enabled: true, hasLaunchZone: true, GameState.StuntsHighJumpDriving),
                Entry(BuildingId.Garage, enabled: true, hasLaunchZone: false, GameState.GarageMeta),
                Entry(BuildingId.LongJump, enabled: false, hasLaunchZone: true, GameState.DerbyMatch)
            });

            _flow = new FakeGameFlowService(GameState.BaseHub);
            _build = new FakeBuildProgressService();
            _service = new HubLaunchService(_catalog, _flow, _build);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_catalog);
        }

        // ── Таймер и запуск ───────────────────────────────────────────────────────

        [Test]
        public void Advance_BeforeConfirmSeconds_DoesNotLaunch()
        {
            _service.EnterZone(BuildingId.DerbyArena);

            _service.Advance(0.75f);

            Assert.AreEqual(0, _flow.ChangeStateCalls.Count);
        }

        [Test]
        public void Advance_ReachesConfirmSeconds_LaunchesExactlyOnceWithZoneState()
        {
            _service.EnterZone(BuildingId.HighJumpTower);

            _service.Advance(0.75f);
            _service.Advance(0.25f);
            // Лишние тики после запуска не должны стрелять повторно (Current фейка остаётся BaseHub).
            _service.Advance(1f);
            _service.Advance(1f);

            Assert.AreEqual(1, _flow.ChangeStateCalls.Count);
            Assert.AreEqual(GameState.StuntsHighJumpDriving, _flow.ChangeStateCalls[0]);
        }

        [Test]
        public void Advance_AfterLaunch_ZoneIsDeactivated()
        {
            _service.EnterZone(BuildingId.DerbyArena);

            _service.Advance(ConfirmSeconds);

            Assert.IsFalse(_service.TryGetActiveZone(out _));
            Assert.AreEqual(0f, _service.Progress01);
        }

        [Test]
        public void Advance_NonPositiveConfirmSeconds_LaunchesOnFirstAdvance()
        {
            SetPrivateField(_catalog, "_launchConfirmSeconds", 0f);
            _service.EnterZone(BuildingId.DerbyArena);

            _service.Advance(0f);

            Assert.AreEqual(1, _flow.ChangeStateCalls.Count);
            Assert.AreEqual(GameState.DerbyMatch, _flow.ChangeStateCalls[0]);
        }

        [Test]
        public void Advance_WithoutActiveZone_DoesNothing()
        {
            _service.Advance(5f);

            Assert.AreEqual(0, _flow.ChangeStateCalls.Count);
        }

        // ── Выход и повторный вход ────────────────────────────────────────────────

        [Test]
        public void ExitZone_BeforeTimer_ResetsAndDoesNotLaunch()
        {
            _service.EnterZone(BuildingId.DerbyArena);
            _service.Advance(0.75f);

            _service.ExitZone(BuildingId.DerbyArena);
            _service.Advance(1f);

            Assert.IsFalse(_service.TryGetActiveZone(out _));
            Assert.AreEqual(0f, _service.Progress01);
            Assert.AreEqual(0, _flow.ChangeStateCalls.Count);
        }

        [Test]
        public void EnterZone_Again_StartsFromZero()
        {
            _service.EnterZone(BuildingId.DerbyArena);
            _service.Advance(0.75f);
            _service.ExitZone(BuildingId.DerbyArena);

            _service.EnterZone(BuildingId.DerbyArena);
            _service.Advance(0.5f);

            Assert.AreEqual(0, _flow.ChangeStateCalls.Count, "Отсчёт после повторного входа должен начаться с нуля.");
            Assert.AreEqual(0.5f, _service.Progress01, 1e-4f);
        }

        // ── Недоступные зоны ──────────────────────────────────────────────────────

        [Test]
        public void EnterZone_WithoutLaunchZone_IsIgnored()
        {
            _service.EnterZone(BuildingId.Garage);
            _service.Advance(2f);

            Assert.IsFalse(_service.TryGetActiveZone(out _));
            Assert.AreEqual(0, _flow.ChangeStateCalls.Count);
        }

        [Test]
        public void EnterZone_DisabledBuilding_IsIgnored()
        {
            _service.EnterZone(BuildingId.LongJump);
            _service.Advance(2f);

            Assert.IsFalse(_service.TryGetActiveZone(out _));
            Assert.AreEqual(0, _flow.ChangeStateCalls.Count);
        }

        [Test]
        public void EnterZone_BuildingMissingInCatalog_IsIgnored()
        {
            _service.EnterZone(BuildingId.Bowling);
            _service.Advance(2f);

            Assert.IsFalse(_service.TryGetActiveZone(out _));
            Assert.AreEqual(0, _flow.ChangeStateCalls.Count);
        }

        // ── Гейт стройкой (Шаг 12a) ─────────────────────────────────────────────

        [Test]
        public void EnterZone_UnbuiltBuilding_DoesNotLaunch()
        {
            _build.Unbuilt.Add(BuildingId.HighJumpTower);

            _service.EnterZone(BuildingId.HighJumpTower);
            _service.Advance(2f);

            Assert.IsFalse(_service.TryGetActiveZone(out _));
            Assert.AreEqual(0, _flow.ChangeStateCalls.Count);
        }

        [Test]
        public void EnterZone_BuiltWhileInside_NoAutoLaunch_ReenterLaunches()
        {
            _build.Unbuilt.Add(BuildingId.HighJumpTower);
            _service.EnterZone(BuildingId.HighJumpTower);

            // Стройка завершилась, персонаж стоит в зоне — повторного OnTriggerEnter нет, запуска нет.
            _build.Unbuilt.Remove(BuildingId.HighJumpTower);
            _service.Advance(2f);

            Assert.AreEqual(0, _flow.ChangeStateCalls.Count, "Завершение стройки не должно авто-запускать режим.");

            // Перезаход в зону — обычный запуск по таймеру.
            _service.ExitZone(BuildingId.HighJumpTower);
            _service.EnterZone(BuildingId.HighJumpTower);
            _service.Advance(ConfirmSeconds);

            Assert.AreEqual(1, _flow.ChangeStateCalls.Count);
            Assert.AreEqual(GameState.StuntsHighJumpDriving, _flow.ChangeStateCalls[0]);
        }

        // ── Время только в BaseHub ────────────────────────────────────────────────

        [Test]
        public void Advance_OutsideBaseHub_TimeDoesNotPass()
        {
            _service.EnterZone(BuildingId.DerbyArena);
            _flow.Current = GameState.Paused;

            _service.Advance(5f);

            Assert.AreEqual(0, _flow.ChangeStateCalls.Count);
            Assert.AreEqual(0f, _service.Progress01);

            // После возврата в BaseHub отсчёт продолжается с того же места (с нуля, т.к. в паузе не шёл).
            _flow.Current = GameState.BaseHub;
            _service.Advance(0.5f);

            Assert.AreEqual(0.5f, _service.Progress01, 1e-4f);
            Assert.AreEqual(0, _flow.ChangeStateCalls.Count);
        }

        // ── Перекрытие зон ────────────────────────────────────────────────────────

        [Test]
        public void EnterZone_SecondWhileFirstActive_SecondBecomesActive()
        {
            _service.EnterZone(BuildingId.DerbyArena);
            _service.Advance(0.75f);

            _service.EnterZone(BuildingId.HighJumpTower);

            Assert.IsTrue(_service.TryGetActiveZone(out var active));
            Assert.AreEqual(BuildingId.HighJumpTower, active);
            Assert.AreEqual(0f, _service.Progress01, "Вход в новую зону начинает отсчёт с нуля.");
        }

        [Test]
        public void ExitZone_OfInactiveOverlappedZone_DoesNotResetActive()
        {
            _service.EnterZone(BuildingId.DerbyArena);
            _service.EnterZone(BuildingId.HighJumpTower);
            _service.Advance(0.5f);

            _service.ExitZone(BuildingId.DerbyArena);

            Assert.IsTrue(_service.TryGetActiveZone(out var active));
            Assert.AreEqual(BuildingId.HighJumpTower, active);
            Assert.AreEqual(0.5f, _service.Progress01, 1e-4f);

            _service.Advance(0.5f);

            Assert.AreEqual(1, _flow.ChangeStateCalls.Count);
            Assert.AreEqual(GameState.StuntsHighJumpDriving, _flow.ChangeStateCalls[0]);
        }

        [Test]
        public void ExitZone_OfActiveWhileInsideAnother_FallsBackToRemaining()
        {
            // Персонаж стоит в A, задевает перекрытие с B и уходит вглубь A.
            _service.EnterZone(BuildingId.DerbyArena);
            _service.EnterZone(BuildingId.HighJumpTower);
            _service.Advance(0.5f);

            _service.ExitZone(BuildingId.HighJumpTower);

            Assert.IsTrue(_service.TryGetActiveZone(out var active));
            Assert.AreEqual(BuildingId.DerbyArena, active);
            Assert.AreEqual(0f, _service.Progress01, "Возврат к оставшейся зоне начинает отсчёт с нуля.");
            Assert.AreEqual(0, _flow.ChangeStateCalls.Count);

            _service.Advance(ConfirmSeconds);

            Assert.AreEqual(1, _flow.ChangeStateCalls.Count);
            Assert.AreEqual(GameState.DerbyMatch, _flow.ChangeStateCalls[0]);
        }

        [Test]
        public void ExitZone_AllZones_NoActive()
        {
            _service.EnterZone(BuildingId.DerbyArena);
            _service.EnterZone(BuildingId.HighJumpTower);

            _service.ExitZone(BuildingId.HighJumpTower);
            _service.ExitZone(BuildingId.DerbyArena);
            _service.Advance(2f);

            Assert.IsFalse(_service.TryGetActiveZone(out _));
            Assert.AreEqual(0f, _service.Progress01);
            Assert.AreEqual(0, _flow.ChangeStateCalls.Count);
        }

        [Test]
        public void EnterZone_SameZoneTwice_DoesNotDuplicate()
        {
            _service.EnterZone(BuildingId.DerbyArena);
            _service.EnterZone(BuildingId.DerbyArena);

            _service.ExitZone(BuildingId.DerbyArena);

            Assert.IsFalse(_service.TryGetActiveZone(out _), "Дубль в списке занятых зон оставил бы зону активной.");
            Assert.AreEqual(0f, _service.Progress01);
        }

        // ── Progress01 ────────────────────────────────────────────────────────────

        [Test]
        public void Progress01_HalfwayThroughTimer_IsHalf()
        {
            _service.EnterZone(BuildingId.DerbyArena);

            _service.Advance(0.5f);

            Assert.IsTrue(_service.TryGetActiveZone(out var active));
            Assert.AreEqual(BuildingId.DerbyArena, active);
            Assert.AreEqual(0.5f, _service.Progress01, 1e-4f);
        }

        [Test]
        public void Progress01_NoActiveZone_IsZero()
        {
            Assert.AreEqual(0f, _service.Progress01);
        }

        // ── Утилиты ─────────────────────────────────────────────────────────────

        private static BuildingCatalogConfig.BuildingEntry Entry(BuildingId id, bool enabled, bool hasLaunchZone,
            GameState launchState)
        {
            return new BuildingCatalogConfig.BuildingEntry
            {
                Id = id,
                Enabled = enabled,
                HasLaunchZone = hasLaunchZone,
                LaunchState = launchState
            };
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Поле {fieldName} не найдено — контракт BuildingCatalogConfig изменился?");
            field.SetValue(target, value);
        }

        // ── Фейки ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Фейк GameFlow: Current задаётся тестом и НЕ меняется от ChangeState — так проверяется,
        /// что сервис сам деактивирует зону после запуска, а не полагается на уход из BaseHub.
        /// </summary>
        private sealed class FakeGameFlowService : IGameFlowService
        {
            public readonly List<GameState> ChangeStateCalls = new List<GameState>();

            public FakeGameFlowService(GameState initial) => Current = initial;

            public GameState Current { get; set; }

            public void ChangeState(GameState next) => ChangeStateCalls.Add(next);
            public void Push(GameState next) { }
            public void Pop() { }
            public void Tick() { }
        }

        /// <summary>
        /// Фейк стройки: по умолчанию ВСЁ построено (старые тесты проверяют только таймер),
        /// недостроенные здания тест кладёт в Unbuilt.
        /// </summary>
        private sealed class FakeBuildProgressService : IBuildProgressService
        {
            public readonly HashSet<BuildingId> Unbuilt = new HashSet<BuildingId>();

            public bool IsBuilt(BuildingId id) => !Unbuilt.Contains(id);
            public bool IsUnderConstruction(BuildingId id) => Unbuilt.Contains(id);

            public int GetCompletedStages(BuildingId id) => 0;
            public int GetTotalStages(BuildingId id) => 0;
            public int GetInvestedInStage(BuildingId id) => 0;
            public int GetCurrentStageCost(BuildingId id) => 0;
            public int GetCurrentIncomePerMinute(BuildingId id) => 0;
            public void EnterZone(BuildingId id) { }
            public void ExitZone(BuildingId id) { }
        }
    }
}
