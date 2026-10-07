---
name: step11c-scene-fade
description: Шаги 11c/11d — SceneLoaderService (EntryPoint IStartable+ITickable) с ILoadingScreen вместо фейда, IsLoading-гарды, Boot-ловушка, isActiveAndEnabled-ловушка
metadata:
  type: project
---

11c (2026-10-08) сделал двухфазный чёрный фейд; 11d (2026-10-08) заменил его экраном загрузки:
IScreenFader/NullScreenFader/ScreenFadeView удалены → ILoadingScreen/NullLoadingScreen/LoadingScreenView
+ LoadingScreenConfig (Data/Configs/Flow) + LoadingProgressModel (pure C#, фейк-кривая прогресса).
SceneLoaderService: ctor(ILoadingScreen, LoadingScreenConfig, IEventBus) — Cover в ctor (eager через
RegisterBuildCallback), Load → Show → onShown → LoadSceneAsync(allowSceneActivation=false) +
SceneTransitionCoveredEvent; Tick ведёт прогресс; Start() → Hide только при `!_loading`.

**Why:** scope per-scene, без DontDestroyOnLoad/static. Boot: GameFlowService вызывает Load ещё при
сборке контейнера. Порядок: SceneEntryState.Enter → Load (IsLoading=true) → ПОТОМ GameStateChangedEvent,
поэтому подписчики (AudioService) уже видят IsLoading. GameFlowService.ChangeState/Push отбрасывает
переходы при IsLoading; PauseService.Tick тоже; covered-событие → timeScale 0 и AudioListener-пауза
(сброс — ctor PauseService / Start AudioService новой сцены).

**How to apply:** расширяя ISceneLoaderService — фейки в GameFlowSceneTests И PauseServiceTests.
Новые ctor-зависимости PauseService/AudioService/SceneLoaderService — обновить тесты (FakeLoadingScreen
не вызывает onShown, чтобы не трогать SceneManager в EditMode). Во view, вызываемом из сборки контейнера
(Awake scope), не полагаться на isActiveAndEnabled (false до OnEnable) — `enabled && activeInHierarchy`.
См. [[project-architecture]], [[tooling-gotchas]].
