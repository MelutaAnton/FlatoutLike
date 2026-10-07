---
name: hotspot-scene-transition-window
description: С 11c старая сцена живёт весь фейд+LoadSceneAsync с уже переключённым FSM — проверять побочки GameStateChangedEvent и геймплея в этом окне
metadata:
  type: project
---

С шага 11c (SceneLoaderService → IScreenFader.FadeOut → LoadSceneAsync) старая сцена продолжает
симулироваться ~0.25 с фейда + время async-загрузки. GameFlowService.ChangeState публикует
GameStateChangedEvent СРАЗУ (Enter→Load только начинает фейд), поэтому в уходящей сцене уже:
AudioService играет музыку целевого состояния (потом новая сцена стартует её заново),
InputRouter переключил контекст, PauseService разрешает Push(Paused) по ESC (под шторкой).
Повторный Load гасится флагом `_loading`, но FSM-переход второго запроса всё равно проходит.

**Why:** раньше SceneManager.LoadScene выгружал сцену в конце того же кадра — окна не было.
**How to apply:** при ревью шагов, добавляющих подписчиков GameStateChangedEvent, награды/
матч-логику, таймеры в хабе (шаг 12–13 idle-доход!) — спрашивать «что будет, если это сработает
в окне перехода». Boot без камеры → чёрный в билде, вспышки нет. См. [[project-hub-launch-zones]].
