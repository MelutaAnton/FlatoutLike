using System.Collections.Generic;
using System.Reflection;
using FlatOutLike.Core.Events;
using FlatOutLike.Data.Configs.Hub;
using FlatOutLike.Data.Configs.Progression;
using FlatOutLike.Models;
using FlatOutLike.Services.Flow;
using FlatOutLike.Services.Meta;
using FlatOutLike.Services.Progression;
using NUnit.Framework;
using UnityEngine;

namespace FlatOutLike.Tests
{
    /// <summary>
    /// Юнит-тесты <see cref="BuildProgressService"/> (Шаг 12a): потоковое вложение пачками,
    /// дробный накопитель, границы этапов, остановка без баланса (без «рывка»), события этапов,
    /// игнор недопустимых зон, остановка вне BaseHub, сохранение и нормализация сейва.
    /// Каталог и HubCharacterConfig — настоящие SO (CreateInstance), приватные поля — через
    /// reflection (образец HubLaunchServiceTests). Шина — настоящий EventBus.
    /// Время шагом 0.25 с и скорости 20/6/28 монет/с — двоично-точные (5 / 1.5 / 7 монет за пачку).
    /// </summary>
    [TestFixture]
    public class BuildProgressServiceTests
    {
        private const float Batch = 0.25f;
        private const float DefaultRate = 20f; // 5 монет за пачку

        private BuildingCatalogConfig _catalog;
        private HubCharacterConfig _character;
        private FakeEconomyService _economy;
        private FakeHubSaveStorage _storage;
        private FakeMetaProgressService _meta;
        private FakeGameFlowService _flow;
        private EventBus _bus;

        // Общий журнал записей на диск обоих блоков — проверяет порядок «коммита» (профиль → хаб).
        private readonly List<string> _saveLog = new List<string>();

        private readonly List<BuildingInvestProgressEvent> _progressEvents = new List<BuildingInvestProgressEvent>();
        private readonly List<BuildingStageInvestedEvent> _stageEvents = new List<BuildingStageInvestedEvent>();
        private readonly List<BuildingCompletedEvent> _completedEvents = new List<BuildingCompletedEvent>();

        [SetUp]
        public void SetUp()
        {
            _catalog = ScriptableObject.CreateInstance<BuildingCatalogConfig>();
            SetPrivateField(_catalog, "_entries", new[]
            {
                // Башня: 10/15/20, без дохода.
                Entry(BuildingId.HighJumpTower, enabled: true, Stage(10, 0), Stage(15, 0), Stage(20, 0)),
                // Касса: 12/20, доход 5 → 8.
                Entry(BuildingId.TicketBooth, enabled: true, Stage(12, 5), Stage(20, 8)),
                // Гараж: этапов нет — построен со старта.
                Entry(BuildingId.Garage, enabled: true),
                // Выключенная заглушка с этапами.
                Entry(BuildingId.LongJump, enabled: false, Stage(10, 1))
            });

            _character = ScriptableObject.CreateInstance<HubCharacterConfig>();
            SetPrivateField(_character, "_investStreamRate", DefaultRate);
            SetPrivateField(_character, "_investBatchSeconds", Batch);

            _saveLog.Clear();
            _economy = new FakeEconomyService { Coins = 1000 };
            _storage = new FakeHubSaveStorage(_saveLog);
            _meta = new FakeMetaProgressService(_saveLog);
            _flow = new FakeGameFlowService { Current = GameState.BaseHub };
            _bus = new EventBus();

            _progressEvents.Clear();
            _stageEvents.Clear();
            _completedEvents.Clear();
            _bus.Subscribe<BuildingInvestProgressEvent>(_progressEvents.Add);
            _bus.Subscribe<BuildingStageInvestedEvent>(_stageEvents.Add);
            _bus.Subscribe<BuildingCompletedEvent>(_completedEvents.Add);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_catalog);
            Object.DestroyImmediate(_character);
        }

        private BuildProgressService CreateService() =>
            new BuildProgressService(_catalog, _character, _economy, _storage, _bus, _flow, _meta);

        private static void Step(BuildProgressService service, int batches)
        {
            for (int i = 0; i < batches; i++)
                service.Advance(Batch);
        }

        // ── Поток пачками ───────────────────────────────────────────────────────

        [Test]
        public void Advance_StreamsInBatches_InvestsRateTimesSeconds()
        {
            var service = CreateService();
            service.EnterZone(BuildingId.TicketBooth);

            Step(service, 4); // 1 с при 20 монет/с

            Assert.AreEqual(20, 1000 - _economy.Coins, "За 1 с списано rate*1 монет.");
            // 5, 5, остаток этапа 12 — 2 (3 монеты остаются в накопителе), затем 3 + 5 во второй этап.
            CollectionAssert.AreEqual(new[] { 5, 5, 2, 8 }, _economy.SpendCalls);
            Assert.AreEqual(1, service.GetCompletedStages(BuildingId.TicketBooth));
            Assert.AreEqual(8, service.GetInvestedInStage(BuildingId.TicketBooth));
        }

        [Test]
        public void Advance_BeforeBatchPeriod_SpendsNothing()
        {
            var service = CreateService();
            service.EnterZone(BuildingId.TicketBooth);

            service.Advance(0.125f);

            Assert.AreEqual(0, _economy.SpendCalls.Count);
            Assert.AreEqual(1000, _economy.Coins);
        }

        [Test]
        public void Advance_FractionalAccumulator_CarriesRemainder()
        {
            SetPrivateField(_character, "_investStreamRate", 6f); // 1.5 монеты за пачку
            var service = CreateService();
            service.EnterZone(BuildingId.TicketBooth);

            Step(service, 4);

            CollectionAssert.AreEqual(new[] { 1, 2, 1, 2 }, _economy.SpendCalls);
            Assert.AreEqual(6, service.GetInvestedInStage(BuildingId.TicketBooth));
        }

        [Test]
        public void Advance_BatchDoesNotOvershootStageCost()
        {
            SetPrivateField(_character, "_investStreamRate", 28f); // 7 монет за пачку, этап башни — 10
            var service = CreateService();
            service.EnterZone(BuildingId.HighJumpTower);

            Step(service, 2);

            CollectionAssert.AreEqual(new[] { 7, 3 }, _economy.SpendCalls, "Вторая пачка — только остаток этапа.");
            Assert.AreEqual(1, service.GetCompletedStages(BuildingId.HighJumpTower));
            Assert.AreEqual(0, service.GetInvestedInStage(BuildingId.HighJumpTower));
        }

        [Test]
        public void Advance_StandingInZone_ContinuesToNextStage()
        {
            var service = CreateService();
            service.EnterZone(BuildingId.HighJumpTower);

            Step(service, 2 + 3); // 10 + 15

            Assert.AreEqual(2, service.GetCompletedStages(BuildingId.HighJumpTower));
            Assert.AreEqual(0, service.GetInvestedInStage(BuildingId.HighJumpTower));
            Assert.AreEqual(20, service.GetCurrentStageCost(BuildingId.HighJumpTower));

            Step(service, 4); // 20

            Assert.IsTrue(service.IsBuilt(BuildingId.HighJumpTower));
            Assert.AreEqual(45, 1000 - _economy.Coins);
        }

        [Test]
        public void Advance_ZeroBatchSeconds_EveryAdvanceIsBatch()
        {
            SetPrivateField(_character, "_investBatchSeconds", 0f);
            var service = CreateService();
            service.EnterZone(BuildingId.TicketBooth);

            service.Advance(0.25f);
            service.Advance(0.25f);

            CollectionAssert.AreEqual(new[] { 5, 5 }, _economy.SpendCalls);
        }

        // ── Недостаток баланса ──────────────────────────────────────────────────

        [Test]
        public void Advance_NotEnoughCoins_StreamStops_NoBurstAfterTopUp()
        {
            _economy.Coins = 7;
            var service = CreateService();
            service.EnterZone(BuildingId.TicketBooth);

            Step(service, 2);

            CollectionAssert.AreEqual(new[] { 5, 2 }, _economy.SpendCalls, "Пачка не превышает баланс.");
            Assert.AreEqual(0, _economy.Coins);

            Step(service, 8); // 2 с без денег — поток стоит

            Assert.AreEqual(2, _economy.SpendCalls.Count);
            Assert.AreEqual(7, service.GetInvestedInStage(BuildingId.TicketBooth));

            _economy.Coins = 100;
            Step(service, 1);

            Assert.AreEqual(3, _economy.SpendCalls.Count);
            Assert.AreEqual(5, _economy.SpendCalls[2], "После пополнения — обычная пачка, без «рывка» за простой.");
        }

        // ── События ─────────────────────────────────────────────────────────────

        [Test]
        public void FullBuild_StageAndCompletedEvents_ExactlyOnce()
        {
            var service = CreateService();
            service.EnterZone(BuildingId.HighJumpTower);

            Step(service, 40); // с запасом: построится за 9 пачек, дальше поток должен стоять

            Assert.AreEqual(3, _stageEvents.Count);
            for (int i = 0; i < _stageEvents.Count; i++)
            {
                Assert.AreEqual(BuildingId.HighJumpTower, _stageEvents[i].Id);
                Assert.AreEqual(i + 1, _stageEvents[i].CompletedStages);
                Assert.AreEqual(3, _stageEvents[i].TotalStages);
            }

            Assert.AreEqual(1, _completedEvents.Count);
            Assert.AreEqual(BuildingId.HighJumpTower, _completedEvents[0].Id);
            Assert.AreEqual(45, 1000 - _economy.Coins, "После завершения поток не списывает.");
        }

        [Test]
        public void Batch_PublishesInvestProgressEvent()
        {
            var service = CreateService();
            service.EnterZone(BuildingId.TicketBooth);

            Step(service, 1);

            Assert.AreEqual(1, _progressEvents.Count);
            Assert.AreEqual(BuildingId.TicketBooth, _progressEvents[0].Id);
            Assert.AreEqual(0, _progressEvents[0].CompletedStages);
            Assert.AreEqual(5, _progressEvents[0].InvestedInStage);
            Assert.AreEqual(12, _progressEvents[0].StageCost);
        }

        [Test]
        public void Completed_ZoneDeactivated_ReenterIgnored()
        {
            var service = CreateService();
            service.EnterZone(BuildingId.HighJumpTower);
            Step(service, 9);
            Assert.IsTrue(service.IsBuilt(BuildingId.HighJumpTower));
            int spends = _economy.SpendCalls.Count;

            service.EnterZone(BuildingId.HighJumpTower);
            Step(service, 4);

            Assert.AreEqual(spends, _economy.SpendCalls.Count);
        }

        // ── Недопустимые зоны ───────────────────────────────────────────────────

        [Test]
        public void EnterZone_BuiltFromStart_IsIgnored()
        {
            var service = CreateService();
            service.EnterZone(BuildingId.Garage);
            Step(service, 4);

            Assert.AreEqual(0, _economy.SpendCalls.Count);
        }

        [Test]
        public void EnterZone_DisabledBuilding_IsIgnored()
        {
            var service = CreateService();
            service.EnterZone(BuildingId.LongJump);
            Step(service, 4);

            Assert.AreEqual(0, _economy.SpendCalls.Count);
        }

        [Test]
        public void EnterZone_MissingInCatalog_IsIgnored()
        {
            var service = CreateService();
            service.EnterZone(BuildingId.Bowling);
            Step(service, 4);

            Assert.AreEqual(0, _economy.SpendCalls.Count);
        }

        [Test]
        public void EnterZone_BuiltFromSave_IsIgnored()
        {
            _storage.Preload(BuildingId.HighJumpTower, 3, 0);
            var service = CreateService();

            service.EnterZone(BuildingId.HighJumpTower);
            Step(service, 4);

            Assert.AreEqual(0, _economy.SpendCalls.Count);
        }

        // ── Выход и перекрытие ──────────────────────────────────────────────────

        [Test]
        public void ExitZone_StopsStream()
        {
            var service = CreateService();
            service.EnterZone(BuildingId.TicketBooth);
            Step(service, 1);

            service.ExitZone(BuildingId.TicketBooth);
            Step(service, 4);

            Assert.AreEqual(1, _economy.SpendCalls.Count);
        }

        [Test]
        public void ExitZone_ResetsBatchTimer()
        {
            var service = CreateService();
            service.EnterZone(BuildingId.TicketBooth);
            service.Advance(0.125f);

            service.ExitZone(BuildingId.TicketBooth);
            service.EnterZone(BuildingId.TicketBooth);
            service.Advance(0.125f);

            Assert.AreEqual(0, _economy.SpendCalls.Count, "Накопленное до выхода не должно доплюсоваться после перезахода.");
        }

        [Test]
        public void Overlap_LastEnteredActive_ExitFallsBackToRemaining()
        {
            var service = CreateService();
            service.EnterZone(BuildingId.TicketBooth);
            service.EnterZone(BuildingId.HighJumpTower);

            Step(service, 1);

            Assert.AreEqual(5, service.GetInvestedInStage(BuildingId.HighJumpTower));
            Assert.AreEqual(0, service.GetInvestedInStage(BuildingId.TicketBooth));

            service.ExitZone(BuildingId.HighJumpTower);
            Step(service, 1);

            Assert.AreEqual(5, service.GetInvestedInStage(BuildingId.TicketBooth));
            Assert.AreEqual(5, service.GetInvestedInStage(BuildingId.HighJumpTower));
        }

        // ── Только в BaseHub ────────────────────────────────────────────────────

        [Test]
        public void Tick_OutsideBaseHub_DoesNotInvest()
        {
            var service = CreateService();
            service.EnterZone(BuildingId.TicketBooth);
            _flow.Current = GameState.Paused;

            for (int i = 0; i < 100; i++)
                service.Tick();

            Assert.AreEqual(0, _economy.SpendCalls.Count);
            Assert.AreEqual(0, service.GetInvestedInStage(BuildingId.TicketBooth));
        }

        // ── Сейв ────────────────────────────────────────────────────────────────

        [Test]
        public void Tick_OutsideBaseHub_FlushesUnsavedBatchesOnce()
        {
            var service = CreateService();
            service.EnterZone(BuildingId.TicketBooth);
            Step(service, 2); // 10 < 12 — этап не завершён, пачки только в памяти
            Assert.AreEqual(0, _storage.SaveCount);

            // Игрок жмёт PLAY, стоя в зоне: состояние сменилось — Tick сбрасывает хвост.
            _flow.Current = GameState.Paused;
            service.Tick();

            Assert.AreEqual(1, _meta.SaveCount);
            Assert.AreEqual(1, _storage.SaveCount);
            CollectionAssert.AreEqual(new[] { "meta", "hub" }, _saveLog);
            Assert.IsTrue(_storage.LastSaved.TryGetValue(BuildingId.TicketBooth, out var saved));
            Assert.AreEqual(10, saved.invested);

            // Повторные Tick вне BaseHub ничего не пишут (хвост уже сброшен).
            service.Tick();
            service.Tick();

            Assert.AreEqual(1, _meta.SaveCount);
            Assert.AreEqual(1, _storage.SaveCount);
        }

        [Test]
        public void Batches_WithoutFlushPoint_DoNotWriteStorageOrProfile()
        {
            var service = CreateService();
            service.EnterZone(BuildingId.TicketBooth);

            Step(service, 2); // 5 + 5 = 10 < 12 — этап не завершён

            Assert.AreEqual(10, service.GetInvestedInStage(BuildingId.TicketBooth), "Прогресс в памяти обновлён.");
            Assert.AreEqual(0, _storage.SaveCount, "Пачка не пишет блок хаба.");
            Assert.AreEqual(0, _meta.SaveCount, "Пачка не пишет профиль.");
            CollectionAssert.AreEqual(new[] { false, false }, _economy.PersistFlags,
                "Списание пачкой — TrySpend(n, persist: false).");
        }

        [Test]
        public void StageCompleted_FlushesProfileThenHubBlock()
        {
            var service = CreateService();
            service.EnterZone(BuildingId.TicketBooth);

            Step(service, 3); // 5, 5, 2 (остаток этапа 12) → этап 1

            Assert.AreEqual(1, _meta.SaveCount);
            Assert.AreEqual(1, _storage.SaveCount);
            CollectionAssert.AreEqual(new[] { "meta", "hub" }, _saveLog, "Сначала профиль, затем блок хаба.");
            Assert.IsTrue(_storage.LastSaved.TryGetValue(BuildingId.TicketBooth, out var saved));
            Assert.AreEqual(1, saved.completed);
            Assert.AreEqual(0, saved.invested);
        }

        [Test]
        public void FinalStage_Flushes_OncePerStage()
        {
            var service = CreateService();
            service.EnterZone(BuildingId.HighJumpTower);

            Step(service, 9); // 10 / 15 / 20 — три этапа, последний достраивает здание

            Assert.IsTrue(service.IsBuilt(BuildingId.HighJumpTower));
            Assert.AreEqual(3, _storage.SaveCount, "Flush — ровно один на каждый завершённый этап.");
            Assert.AreEqual(3, _meta.SaveCount);
            Assert.IsTrue(_storage.LastSaved.TryGetValue(BuildingId.HighJumpTower, out var saved));
            Assert.AreEqual(3, saved.completed);
        }

        [Test]
        public void ExitZone_WithUnsavedBatches_Flushes()
        {
            var service = CreateService();
            service.EnterZone(BuildingId.TicketBooth);
            Step(service, 1);

            service.ExitZone(BuildingId.TicketBooth);

            Assert.AreEqual(1, _meta.SaveCount);
            Assert.AreEqual(1, _storage.SaveCount);
            Assert.IsTrue(_storage.LastSaved.TryGetValue(BuildingId.TicketBooth, out var saved));
            Assert.AreEqual(0, saved.completed);
            Assert.AreEqual(5, saved.invested);
        }

        [Test]
        public void ExitZone_WithoutChanges_DoesNotWrite()
        {
            var service = CreateService();
            service.EnterZone(BuildingId.TicketBooth);

            service.ExitZone(BuildingId.TicketBooth);

            Assert.AreEqual(0, _meta.SaveCount);
            Assert.AreEqual(0, _storage.SaveCount);
        }

        [Test]
        public void Dispose_WithUnsavedBatches_Flushes()
        {
            var service = CreateService();
            service.EnterZone(BuildingId.TicketBooth);
            Step(service, 2);

            service.Dispose();

            Assert.AreEqual(1, _meta.SaveCount);
            Assert.AreEqual(1, _storage.SaveCount);
            Assert.IsTrue(_storage.LastSaved.TryGetValue(BuildingId.TicketBooth, out var saved));
            Assert.AreEqual(10, saved.invested);
        }

        [Test]
        public void Dispose_WithoutChanges_DoesNotWrite()
        {
            var service = CreateService();
            service.Dispose();

            Assert.AreEqual(0, _meta.SaveCount);
            Assert.AreEqual(0, _storage.SaveCount);
        }

        [Test]
        public void Dispose_AfterFlush_DoesNotWriteAgain()
        {
            var service = CreateService();
            service.EnterZone(BuildingId.TicketBooth);
            Step(service, 1);
            service.ExitZone(BuildingId.TicketBooth);

            service.Dispose();

            Assert.AreEqual(1, _meta.SaveCount, "Изменения уже сброшены на выходе из зоны.");
            Assert.AreEqual(1, _storage.SaveCount);
        }

        [Test]
        public void Load_RestoresProgress()
        {
            _storage.Preload(BuildingId.HighJumpTower, 1, 7);
            var service = CreateService();

            Assert.AreEqual(1, service.GetCompletedStages(BuildingId.HighJumpTower));
            Assert.AreEqual(7, service.GetInvestedInStage(BuildingId.HighJumpTower));
            Assert.AreEqual(15, service.GetCurrentStageCost(BuildingId.HighJumpTower));
            Assert.IsFalse(service.IsBuilt(BuildingId.HighJumpTower));
        }

        [Test]
        public void Load_NormalizesBrokenSave()
        {
            _storage.Preload(BuildingId.TicketBooth, 9, 50);    // этапов больше, чем в каталоге
            _storage.Preload(BuildingId.HighJumpTower, 1, 99);  // вложено больше цены этапа (15)
            _storage.Preload(BuildingId.Garage, -3, -4);        // мусор у здания без этапов
            var service = CreateService();

            Assert.AreEqual(2, service.GetCompletedStages(BuildingId.TicketBooth));
            Assert.AreEqual(0, service.GetInvestedInStage(BuildingId.TicketBooth));
            Assert.IsTrue(service.IsBuilt(BuildingId.TicketBooth));

            Assert.AreEqual(1, service.GetCompletedStages(BuildingId.HighJumpTower));
            Assert.AreEqual(14, service.GetInvestedInStage(BuildingId.HighJumpTower), "Вложение клампится в [0..cost-1].");

            Assert.AreEqual(0, service.GetCompletedStages(BuildingId.Garage));
            Assert.AreEqual(0, service.GetInvestedInStage(BuildingId.Garage));
            Assert.IsTrue(service.IsBuilt(BuildingId.Garage));
        }

        [Test]
        public void Load_NegativeValues_ClampedToZero()
        {
            _storage.Preload(BuildingId.HighJumpTower, -2, -5);
            var service = CreateService();

            Assert.AreEqual(0, service.GetCompletedStages(BuildingId.HighJumpTower));
            Assert.AreEqual(0, service.GetInvestedInStage(BuildingId.HighJumpTower));
            Assert.AreEqual(10, service.GetCurrentStageCost(BuildingId.HighJumpTower));
        }

        // ── IsBuilt / доход ─────────────────────────────────────────────────────

        [Test]
        public void IsBuilt_ByCatalogAndProgress()
        {
            var service = CreateService();

            Assert.IsTrue(service.IsBuilt(BuildingId.Garage), "Без этапов — построено со старта.");
            Assert.IsFalse(service.IsBuilt(BuildingId.LongJump), "Выключенное здание не построено.");
            Assert.IsFalse(service.IsBuilt(BuildingId.Bowling), "Нет в каталоге — не построено.");
            Assert.IsFalse(service.IsBuilt(BuildingId.HighJumpTower));
            Assert.AreEqual(0, service.GetCurrentStageCost(BuildingId.Garage));
            Assert.AreEqual(0, service.GetCurrentStageCost(BuildingId.Bowling));
            Assert.AreEqual(3, service.GetTotalStages(BuildingId.HighJumpTower));
            Assert.AreEqual(0, service.GetTotalStages(BuildingId.Garage));
        }

        [Test]
        public void IsUnderConstruction_OnlyEnabledUnbuiltWithStages()
        {
            _storage.Preload(BuildingId.TicketBooth, 2, 0); // построено по сейву
            var service = CreateService();

            Assert.IsTrue(service.IsUnderConstruction(BuildingId.HighJumpTower));
            Assert.IsFalse(service.IsUnderConstruction(BuildingId.TicketBooth), "Построено — не строится.");
            Assert.IsFalse(service.IsUnderConstruction(BuildingId.Garage), "Без этапов — построено со старта.");
            Assert.IsFalse(service.IsUnderConstruction(BuildingId.LongJump), "Выключенное здание не строится.");
            Assert.IsFalse(service.IsUnderConstruction(BuildingId.Bowling), "Нет в каталоге.");
        }

        [Test]
        public void GetCurrentIncomePerMinute_GrowsWithStages()
        {
            var service = CreateService();
            Assert.AreEqual(0, service.GetCurrentIncomePerMinute(BuildingId.TicketBooth), "Ни один этап не завершён.");

            service.EnterZone(BuildingId.TicketBooth);
            Step(service, 3); // 15 ≥ 12 → этап 1

            Assert.AreEqual(5, service.GetCurrentIncomePerMinute(BuildingId.TicketBooth));

            Step(service, 4); // добивает этап 2 (20)

            Assert.IsTrue(service.IsBuilt(BuildingId.TicketBooth));
            Assert.AreEqual(8, service.GetCurrentIncomePerMinute(BuildingId.TicketBooth));
            Assert.AreEqual(0, service.GetCurrentIncomePerMinute(BuildingId.Garage));
            Assert.AreEqual(0, service.GetCurrentIncomePerMinute(BuildingId.Bowling));
        }

        // ── Утилиты ─────────────────────────────────────────────────────────────

        private static BuildingCatalogConfig.BuildingEntry Entry(BuildingId id, bool enabled,
            params BuildingCatalogConfig.BuildStageEntry[] stages)
        {
            return new BuildingCatalogConfig.BuildingEntry
            {
                Id = id,
                Enabled = enabled,
                Stages = stages
            };
        }

        private static BuildingCatalogConfig.BuildStageEntry Stage(int cost, int incomePerMinute) =>
            new BuildingCatalogConfig.BuildStageEntry { Cost = cost, IncomePerMinute = incomePerMinute };

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Поле {fieldName} не найдено — контракт {target.GetType().Name} изменился?");
            field.SetValue(target, value);
        }

        // ── Фейки ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Фейк экономики: баланс задаётся тестом, TrySpend списывает и пишет суммы в SpendCalls,
        /// флаг persist — в PersistFlags (сохранение профиля фейк не делает).
        /// </summary>
        private sealed class FakeEconomyService : IEconomyService
        {
            public readonly List<int> SpendCalls = new List<int>();
            public readonly List<bool> PersistFlags = new List<bool>();

            public int Coins { get; set; }
            public string SelectedCarId => string.Empty;

            public bool TrySpend(int amount) => TrySpend(amount, true);

            public bool TrySpend(int amount, bool persist)
            {
                if (amount <= 0 || amount > Coins) return false;

                Coins -= amount;
                SpendCalls.Add(amount);
                PersistFlags.Add(persist);
                return true;
            }

            public bool IsCarOwned(string carId) => false;
            public bool CanBuyCar(string carId) => false;
            public bool TryBuyCar(string carId) => false;
            public bool TrySelectCar(string carId) => false;
            public int GetUpgradeLevel(UpgradeCategory category) => 0;
            public bool CanBuyUpgrade(UpgradeCategory category) => false;
            public bool TryBuyUpgrade(UpgradeCategory category) => false;
            public void Credit(int coinAmount) => Coins += coinAmount;
        }

        /// <summary>
        /// Хранилище в памяти: Preload задаёт сейв до создания сервиса, Save снимает снимок
        /// (сервис мутирует тот же HubSaveData — без снимка тест видел бы текущее, а не сохранённое).
        /// </summary>
        private sealed class FakeHubSaveStorage : IHubSaveStorage
        {
            private readonly HubSaveData _preloaded = new HubSaveData();
            private readonly List<string> _saveLog;

            public FakeHubSaveStorage(List<string> saveLog) => _saveLog = saveLog;

            public int SaveCount { get; private set; }

            public readonly Dictionary<BuildingId, (int completed, int invested)> LastSaved =
                new Dictionary<BuildingId, (int completed, int invested)>();

            public void Preload(BuildingId id, int completed, int invested) =>
                _preloaded.Set(id, completed, invested);

            public HubSaveData Load() => _preloaded;

            public void Save(HubSaveData data)
            {
                SaveCount++;
                _saveLog.Add("hub");
                LastSaved.Clear();
                foreach (var record in data.Records)
                    LastSaved[record.Id] = (record.CompletedStages, record.InvestedInStage);
            }
        }

        /// <summary>Фейк профиля: только счётчик Save и запись в общий журнал (Profile сервису не нужен).</summary>
        private sealed class FakeMetaProgressService : IMetaProgressService
        {
            private readonly List<string> _saveLog;

            public FakeMetaProgressService(List<string> saveLog) => _saveLog = saveLog;

            public IPlayerProfile Profile => null;
            public int SaveCount { get; private set; }

            public void Save()
            {
                SaveCount++;
                _saveLog.Add("meta");
            }
        }

        private sealed class FakeGameFlowService : IGameFlowService
        {
            public GameState Current { get; set; }

            public void ChangeState(GameState next) { }
            public void Push(GameState next) { }
            public void Pop() { }
            public void Tick() { }
        }
    }
}
