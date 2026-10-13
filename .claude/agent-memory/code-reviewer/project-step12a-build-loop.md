---
name: project-step12a-build-loop
description: Шаг 12a стройка — IHubSaveStorage (одобренное отклонение), flush-модель сейва (TrySpend persist:false + flush на этапе/выходе/Dispose), per-scene MetaProgressService vs Dispose-flush; что проверять в 12b/13
metadata:
  type: project
---

R1 12a (2026-10-09) FAIL: `IHubSaveStorage` не был в «Отклонениях» (ГДД §8/§16 говорят «через IProfileStorage») + `RegisterInstance<IReadOnlyList<BuildingPanelView>>`. R2 (2026-10-09) PASS: оба исправлены (строка в «Отклонениях», `RegisterComponent` каждой панели — VContainer 1.18 `TryFallbackToSingleElementCollection` есть в Registry.cs:179).

**Why:** правило «неучтённое отступление — Critical» проверяется только по разделу «Отклонения»; DoD/OQ не считаются.

**How to apply:**
- Сейв стройки: пачка = `TrySpend(n, persist:false)` в памяти; flush (meta.Save → hub Save) на завершении этапа, ExitZone, Dispose. Горячая точка: `MetaProgressService` per-scene (новый scope читает профиль из PlayerPrefs в Awake), а Dispose-flush старого хаба — в OnDestroy; порядок Awake(new)/OnDestroy(old) при LoadSceneAsync(Single) не проверен → путь «PLAY из зоны стройки» мог вернуть хвост монет. Рекомендован детерминированный flush в Tick при `Current != BaseHub` или по `SceneTransitionCoveredEvent`. На 13 проверить, сделано ли.
- 13 (IdleIncome): данные — в тот же `IHubSaveStorage`/`HubSaveData` (расширить, не второй блок); любой `_meta.Save()` в хабе между пачками (Credit от сбора дохода) пишет «грязный» баланс без блока хаба — OQ в progress.md.
- 12b: стадии из `Prefabs/Hub/<BuildingId>/` (§6); сейчас editor-примитивы; `VisualPrefab` заглушек «Coming soon» — мёртвые данные (одобрено).
- Гейт HighJump — запросом `IBuildProgressService.IsBuilt` в `HubLaunchService.EnterZone` без авто-запуска (одобренное отклонение), не подписка на `BuildingCompletedEvent`.
Связано: [[project-hub-launch-zones]], [[hotspot_playerprefs_tests]], [[hotspot_scene_transition_window]].
