---
name: hotspot-scene-transition-window
description: С 11c/11d старая сцена живёт под экраном загрузки с уже переключённым FSM; 11d закрыл дыры IsLoading-гейтами — проверять новых подписчиков и error-path провала LoadSceneAsync
metadata:
  type: project
---

С шага 11c старая сцена симулируется весь переход (проявление экрана + async-загрузка).
GameFlowService.ChangeState публикует GameStateChangedEvent СРАЗУ (Enter→Load только начинает переход).

11d (экран загрузки, R2 2026-10-08) закрыл: `ISceneLoaderService.IsLoading` → GameFlowService отбрасывает
ChangeState/Push/Pop, PauseService.Tick не ставит паузу и OnStateChanged не сбрасывает timeScale,
AudioService не стартует трек (ни в OnGameStateChanged, ни в Start — Boot больше не играет хаб-трек);
после проявления экрана `SceneTransitionCoveredEvent` → timeScale 0 + AudioListener.pause
(сброс — ctor PauseService / Start AudioService новой сцены). Порядок Covered/StateChanged с NullLoadingScreen
обратный — поэтому гейт в PauseService.OnStateChanged.

Остаточное: error-path `BeginAsyncLoad` (operation == null) снимает _loading, но FSM уже в состоянии
назначения, музыка остановлена, а при Leave из паузы timeScale остаётся 0 (OnStateChanged был загейчен).
Dev-only (сцена вне Build Settings) — Warning.

**Why:** раньше SceneManager.LoadScene выгружал сцену в конце того же кадра — окна не было.
**How to apply:** при ревью шагов, добавляющих подписчиков GameStateChangedEvent/GameResumedEvent, награды/
матч-логику, таймеры в хабе (шаг 12–13 idle-доход!) — спрашивать «что будет, если это сработает
в окне перехода» и гейтится ли это IsLoading. См. [[project-hub-launch-zones]].
