---
name: step11c-scene-fade
description: Шаг 11c — async-загрузка сцен через IScreenFader (ScreenFadeView/NullScreenFader), SceneLoaderService как EntryPoint, Boot-ловушка FadeIn
metadata:
  type: project
---

Шаг 11c (2026-10-08): SceneLoaderService стал EntryPoint (IStartable) с ctor(IScreenFader); переходы двухфазные — FadeOut → LoadSceneAsync, новая сцена стартует чёрной (префаб alpha=1) и проявляется в Start().

**Why:** scope per-scene, без DontDestroyOnLoad/static. Ловушка: в Boot GameFlowService вызывает Load ещё при сборке контейнера (StateMachine ctor вызывает Enter начального состояния), поэтому Start() делает FadeIn только при `!_loading`.

**How to apply:** любой новый ctor-зависимостью SceneLoaderService — обновить SceneLoaderServiceTests (FakeScreenFader не вызывает onComplete, чтобы не трогать SceneManager в EditMode). Новые сцены без ScreenFadeView получают NullScreenFader (мгновенный переход). См. [[project-architecture]].
