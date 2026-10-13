---
name: step12a-build-progress
description: Шаг 12a — BuildProgressService (поток вложения пачками, flush-точки), IHubSaveStorage, TrySpend(n, persist), BuildSiteController/InvestZoneController, BuildingPanelView+Presenter, гейт HubLaunchService
metadata:
  type: project
---

12a (2026-10-09): Services/Progression/ — BuildProgressService (EntryPoint ITickable + IDisposable; Advance(dt) для тестов,
Tick гейтит GameState.BaseHub), HubSaveData/BuildingProgressRecord, IHubSaveStorage + PlayerPrefsHubSaveStorage
(ключи `FlatOutLike.Hub.Build.<(int)id>.Stages/.Invested`). IEconomyService.TrySpend(int) и TrySpend(int, bool persist).
Каталог: LaunchZone* → Zone* (FormerlySerializedAs), BuildingEntry.Stages[BuildStageEntry], IsBuiltFromStart = нет этапов.
HubLaunchService ctor +IBuildProgressService (гейт IsBuilt, без авто-запуска). IsUnderConstruction = в каталоге && Enabled && !IsBuilt.

Правки после ревью (2026-10-09): пачка = TrySpend(n, persist:false) без записи; flush (IMetaProgressService.Save → storage.Save,
флаг _dirty) — на завершении этапа, ExitZone, Dispose. Ценники — `foreach RegisterComponent(panel)`, список собирает VContainer
1.18 (2+ → коллекция, 1 → TryFallbackToSingleElementCollection).

**Why:** запись PlayerPrefs на каждую пачку (0.25 с) = фризы на мобиле; MonoBehaviour через RegisterInstance запрещён.
**How to apply:** расширяя IBuildProgressService — фейк в HubLaunchServiceTests; IEconomyService — фейк
в BuildProgressServiceTests (других фейков нет). Остаток накопителя после закрытия этапа ждёт СЛЕДУЮЩЕЙ пачки
(в тестах ожидания считать по пачкам). Риск: чужой _meta.Save() (Credit/покупка) посреди несброшенных пачек
сохранит баланс без прогресса стройки. См. [[step11c-scene-fade]], [[tooling-gotchas]].
