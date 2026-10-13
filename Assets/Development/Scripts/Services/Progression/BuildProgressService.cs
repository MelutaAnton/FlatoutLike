using System;
using System.Collections.Generic;
using FlatOutLike.Core.Events;
using FlatOutLike.Data.Configs.Hub;
using FlatOutLike.Data.Configs.Progression;
using FlatOutLike.Models;
using FlatOutLike.Services.Flow;
using FlatOutLike.Services.Meta;
using VContainer.Unity;

namespace FlatOutLike.Services.Progression
{
    /// <summary>
    /// Стадийная стройка BaseHub (pure C#, EntryPoint; game-design §6, cli-workflow «Шаг 12»).
    /// Поток: пока персонаж стоит в зоне недостроенного здания, монеты копятся в накопителе со
    /// скоростью InvestStreamRate и списываются целыми пачками раз в InvestBatchSeconds через
    /// IEconomyService.TrySpend. Пачка не перескакивает цену этапа и не превышает баланс; без
    /// монет накопитель обнуляется (после пополнения нет «рывка»). Занятые зоны — как в
    /// HubLaunchService: список в порядке входа, активна последняя вошедшая. Поток идёт только
    /// в состоянии BaseHub (пауза / экран загрузки — стоит). Сервис не вызывает GameFlow и не
    /// ветвится по BuildingId — цены/этапы только из каталога, разблокировки — по событиям.
    ///
    /// Сохранение — не на каждую пачку (4+ записи PlayerPrefs/с = фризы на мобиле), а «коммитом»
    /// обоих блоков (профиль с балансом, затем save-блок хаба) в точках flush: завершение этапа
    /// (включая финальный), выход из зоны, первый Tick вне BaseHub (смена состояния / пауза) и
    /// Dispose как страховка (scope выгружается при смене сцены / выходе).
    /// Пачка списывает через TrySpend(n, persist: false) и меняет данные только в памяти. При
    /// убийстве процесса теряется лишь несброшенный хвост, причём баланс и стройка откатываются
    /// согласованно — ни один из двух блоков этот хвост не сохранил.
    /// </summary>
    public sealed class BuildProgressService : IBuildProgressService, ITickable, IDisposable
    {
        private readonly BuildingCatalogConfig _catalog;
        private readonly HubCharacterConfig _character;
        private readonly IEconomyService _economy;
        private readonly IHubSaveStorage _storage;
        private readonly IEventBus _eventBus;
        private readonly IGameFlowService _gameFlow;
        private readonly IMetaProgressService _meta;

        // Состояние стройки = save-блок хаба (одна структура в памяти и на диске).
        private readonly HubSaveData _data;

        // Есть несохранённые пачки (баланс профиля и/или блок хаба изменены только в памяти).
        private bool _dirty;

        // Занятые зоны в порядке входа; последняя — активная. Недопустимые id сюда не попадают.
        private readonly List<BuildingId> _occupiedZones = new List<BuildingId>();

        private bool _hasActiveZone;
        private BuildingId _activeZone;
        private float _accumulator;
        private float _batchTimer;

        public BuildProgressService(
            BuildingCatalogConfig catalog,
            HubCharacterConfig character,
            IEconomyService economy,
            IHubSaveStorage storage,
            IEventBus eventBus,
            IGameFlowService gameFlow,
            IMetaProgressService meta)
        {
            _catalog = catalog;
            _character = character;
            _economy = economy;
            _storage = storage;
            _eventBus = eventBus;
            _gameFlow = gameFlow;
            _meta = meta;

            _data = _storage.Load() ?? new HubSaveData();
            NormalizeByCatalog();
        }

        // ── Запросы ─────────────────────────────────────────────────────────────

        public int GetCompletedStages(BuildingId id)
        {
            if (!_catalog.TryGetEntry(id, out _)) return 0;

            return _data.TryGet(id, out var record) ? record.CompletedStages : 0;
        }

        public int GetTotalStages(BuildingId id) =>
            _catalog.TryGetEntry(id, out var entry) ? entry.StageCount : 0;

        public bool IsBuilt(BuildingId id)
        {
            if (!_catalog.TryGetEntry(id, out var entry)) return false;
            if (!entry.Enabled) return false;
            if (entry.IsBuiltFromStart) return true;

            return GetCompletedStages(id) >= entry.StageCount;
        }

        public bool IsUnderConstruction(BuildingId id) =>
            _catalog.TryGetEntry(id, out var entry) && entry.Enabled && !IsBuilt(id);

        public int GetInvestedInStage(BuildingId id)
        {
            if (!_catalog.TryGetEntry(id, out var entry)) return 0;
            if (GetCompletedStages(id) >= entry.StageCount) return 0;

            return _data.TryGet(id, out var record) ? record.InvestedInStage : 0;
        }

        public int GetCurrentStageCost(BuildingId id)
        {
            if (!_catalog.TryGetEntry(id, out var entry)) return 0;
            if (IsBuilt(id)) return 0;

            int completed = GetCompletedStages(id);
            if (completed >= entry.StageCount) return 0;

            return StageCost(entry, completed);
        }

        public int GetCurrentIncomePerMinute(BuildingId id)
        {
            if (!_catalog.TryGetEntry(id, out var entry)) return 0;

            int completed = GetCompletedStages(id);
            if (completed <= 0 || completed > entry.StageCount) return 0;

            var stage = entry.Stages[completed - 1];
            return stage != null ? stage.IncomePerMinute : 0;
        }

        // ── Зоны ────────────────────────────────────────────────────────────────

        public void EnterZone(BuildingId id)
        {
            if (!_catalog.TryGetEntry(id, out var entry)) return;
            if (!entry.Enabled || IsBuilt(id)) return;

            // Повторный вход не дублирует запись: id уходит в конец списка (последняя вошедшая).
            _occupiedZones.Remove(id);
            _occupiedZones.Add(id);

            ActivateZone(id);
        }

        public void ExitZone(BuildingId id)
        {
            if (!_occupiedZones.Remove(id)) return;

            // Точка flush: персонаж ушёл из зоны — фиксируем вложенное за время стояния.
            FlushIfDirty();

            // Выход из перекрытой (неактивной) зоны поток не трогает.
            if (!_hasActiveZone || _activeZone != id) return;

            ResetActiveZone();
            ActivateLastOccupied();
        }

        // ── Поток ───────────────────────────────────────────────────────────────

        public void Tick()
        {
            // Поток только в самом хабе: Paused поверх BaseHub и экран загрузки его останавливают.
            if (_gameFlow.Current != GameState.BaseHub)
            {
                // Точка flush: состояние сменилось (например, PLAY в Derby, пока персонаж стоит в зоне).
                // Переход FSM публикуется раньше начала загрузки сцены, так что хвост пачек уходит на диск
                // до того, как MetaProgressService нового scope перечитает профиль. Иначе flush остался бы
                // только в Dispose старого scope, а его порядок относительно Awake нового scope при
                // LoadSceneAsync не гарантирован: новая сцена прочла бы баланс до списания и затем
                // перезаписала бы его («вернув» монеты игроку). Заодно сбрасывает хвост на паузе.
                FlushIfDirty();
                return;
            }

            Advance(UnityEngine.Time.deltaTime);
        }

        /// <summary>Вся логика потока; Tick лишь подставляет deltaTime (тесты зовут напрямую).</summary>
        public void Advance(float deltaTime)
        {
            if (!_hasActiveZone) return;

            _accumulator += _character.InvestStreamRate * deltaTime;
            _batchTimer += deltaTime;

            float batchSeconds = _character.InvestBatchSeconds;
            if (batchSeconds <= 0f)
            {
                // Пачка на каждом Advance.
                _batchTimer = 0f;
                ProcessBatch();
                return;
            }

            // _hasActiveZone проверяется на каждом шаге: завершение здания снимает активную зону
            // (и сбрасывает таймер), дальнейшие пачки этого Advance не нужны.
            while (_hasActiveZone && _batchTimer >= batchSeconds)
            {
                _batchTimer -= batchSeconds;
                if (!ProcessBatch()) return;
            }
        }

        /// <summary>
        /// Одна пачка списания для активной зоны. false — поток встал (нет денег / зона недоступна),
        /// оставшиеся пачки текущего Advance обрабатывать не нужно.
        /// </summary>
        private bool ProcessBatch()
        {
            var id = _activeZone;
            if (!_catalog.TryGetEntry(id, out var entry) || IsBuilt(id))
            {
                // Каталог/состояние поменялись под активной зоной — снимаем её без события.
                DeactivateCompletedZone(id);
                return false;
            }

            int completed = GetCompletedStages(id);
            int invested = GetInvestedInStage(id);
            int cost = StageCost(entry, completed);

            // Бесплатный этап (cost ≤ 0) завершается без списания.
            if (cost <= 0)
            {
                CompleteStage(id, entry, completed);
                return _hasActiveZone;
            }

            int coins = _economy.Coins;
            int amount = (int)Math.Floor(_accumulator);
            amount = Math.Min(amount, cost - invested);
            amount = Math.Min(amount, coins);

            if (amount <= 0)
            {
                // Баланса нет — поток стоит; накопитель обнуляется, чтобы после пополнения не было
                // «рывка» накопленным за время простоя. Если деньги есть, но накопитель < 1 монеты —
                // просто ждём следующей пачки.
                if (coins <= 0)
                {
                    _accumulator = 0f;
                    return false;
                }

                return true;
            }

            // persist: false — профиль сохраняется вместе с блоком хаба в точке flush (см. summary).
            if (!_economy.TrySpend(amount, persist: false))
            {
                _accumulator = 0f;
                return false;
            }

            _dirty = true;
            invested += amount;
            _accumulator -= amount;

            if (invested < cost)
            {
                _data.Set(id, completed, invested);
                _eventBus.Publish(new BuildingInvestProgressEvent(id, completed, invested, cost));
                return true;
            }

            _eventBus.Publish(new BuildingInvestProgressEvent(id, completed, invested, cost));
            CompleteStage(id, entry, completed);
            return _hasActiveZone;
        }

        /// <summary>Этап оплачен: следующий этап (или завершение здания), flush и события.</summary>
        private void CompleteStage(BuildingId id, BuildingCatalogConfig.BuildingEntry entry, int completed)
        {
            int newCompleted = completed + 1;
            _data.Set(id, newCompleted, 0);

            // Точка flush: завершённый этап (и финальный тоже) фиксируется на диске сразу.
            _dirty = true;
            FlushIfDirty();

            _eventBus.Publish(new BuildingStageInvestedEvent(id, newCompleted, entry.StageCount));

            if (newCompleted < entry.StageCount) return;

            // Здание построено: зона перестаёт принимать вложения (без события выхода).
            // Запуск режима сюда не привязан — персонаж перезаходит в зону (HubLaunchService).
            DeactivateCompletedZone(id);
            _eventBus.Publish(new BuildingCompletedEvent(id));
        }

        // ── Активная зона ───────────────────────────────────────────────────────

        private void ActivateZone(BuildingId id)
        {
            // Повторный вход в уже активную зону не сбрасывает накопленное.
            if (_hasActiveZone && _activeZone == id) return;

            _hasActiveZone = true;
            _activeZone = id;
            _accumulator = 0f;
            _batchTimer = 0f;
        }

        private void ResetActiveZone()
        {
            _hasActiveZone = false;
            _activeZone = default;
            _accumulator = 0f;
            _batchTimer = 0f;
        }

        /// <summary>ExitZone-логика без события: убрать зону и переключиться на оставшуюся занятую.</summary>
        private void DeactivateCompletedZone(BuildingId id)
        {
            _occupiedZones.Remove(id);
            if (_hasActiveZone && _activeZone == id)
            {
                ResetActiveZone();
                ActivateLastOccupied();
            }
        }

        /// <summary>
        /// Персонаж ещё стоит в другой зоне (OnTriggerEnter для неё уже не придёт) — она становится
        /// активной. Зоны, ставшие недопустимыми (достроены), выбрасываются из списка.
        /// </summary>
        private void ActivateLastOccupied()
        {
            while (_occupiedZones.Count > 0)
            {
                var fallback = _occupiedZones[_occupiedZones.Count - 1];
                if (_catalog.TryGetEntry(fallback, out var entry) && entry.Enabled && !IsBuilt(fallback))
                {
                    ActivateZone(fallback);
                    return;
                }

                _occupiedZones.RemoveAt(_occupiedZones.Count - 1);
            }
        }

        // ── Сейв ────────────────────────────────────────────────────────────────

        /// <summary>
        /// Scope выгружается (смена сцены / выход из игры) — последняя точка flush: сохраняем
        /// хвост, если персонаж так и не вышел из зоны.
        /// </summary>
        public void Dispose() => FlushIfDirty();

        /// <summary>
        /// «Коммит» обоих блоков: сначала профиль (баланс после списаний), затем блок хаба.
        /// Без несохранённых изменений ничего не пишет.
        /// </summary>
        private void FlushIfDirty()
        {
            if (!_dirty) return;

            _meta.Save();
            _storage.Save(_data);
            _dirty = false;
        }

        /// <summary>
        /// Приводит загруженный сейв к текущему каталогу: CompletedStages в [0..StageCount],
        /// InvestedInStage в [0..cost-1] текущего этапа (0, если построено). Битый/устаревший
        /// сейв не роняет стройку и не даёт «лишних» этапов.
        /// </summary>
        private void NormalizeByCatalog()
        {
            var entries = _catalog.Entries;
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null || !_data.TryGet(entry.Id, out var record)) continue;

                int completed = Clamp(record.CompletedStages, 0, entry.StageCount);
                int invested = 0;
                if (completed < entry.StageCount)
                {
                    int cost = StageCost(entry, completed);
                    invested = cost > 0 ? Clamp(record.InvestedInStage, 0, cost - 1) : 0;
                }

                _data.Set(entry.Id, completed, invested);
            }
        }

        // Без UnityEngine.Mathf (сервис pure C#); Math.Clamp нет в профиле .NET Framework.
        private static int Clamp(int value, int min, int max) => Math.Max(min, Math.Min(value, max));

        private static int StageCost(BuildingCatalogConfig.BuildingEntry entry, int stageIndex)
        {
            if (stageIndex < 0 || stageIndex >= entry.StageCount) return 0;

            var stage = entry.Stages[stageIndex];
            return stage != null ? stage.Cost : 0;
        }
    }
}
