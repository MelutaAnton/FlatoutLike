---
name: project-hub-camera-outside-rig
description: С шага 11a камера BaseHub (CinemachineHubCameraBehaviour) живёт вне ICameraRig/CameraService — статичный Priority, Follow прошит editor-скриптом; ICameraBehaviour-методы в рантайме никто не зовёт
metadata:
  type: project
---

BaseHub: одна vcam `CM_HubCamera` + `CinemachineHubCameraBehaviour` (конфиг через SerializeField, применяется
в Awake, без DI). `_cameraRig` в scope хаба = null → CameraService не регистрируется, в консоли 3 штатных
LogWarning (_cameraRig/_cameraShake/_nitroCameraEffect) на каждую загрузку хаба. DoD 11a явно говорит
«одна vcam, без CameraRig», но строки в «Отклонениях» progress.md на это нет (было Warning в ревью 11a).

Обновление 11b (2026-10-06): эти 3 сообщения в мета-сценах (initialState BaseHub/GarageMeta) теперь `Debug.Log` через локальную `LogMissingOptional` в `GameLifetimeScope.Configure`; в остальных сценах остаются LogWarning. Хвост закрыт.

**Why:** per-scene scope (одобренное отклонение шага 8) делает сквозной CameraRig из §11 бессмысленным в хабе.

**How to apply:** если в 11b+ появится второй вид камеры в хабе (например, на зону запуска) — проверить,
что переключение идёт через сервис по GameStateChangedEvent, а не ручным Priority из контроллеров.
Связано: [[project-di-lazy-singletons]].
