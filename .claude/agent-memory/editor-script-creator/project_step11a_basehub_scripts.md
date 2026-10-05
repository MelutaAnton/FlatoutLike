---
name: step11a-basehub-scripts
description: Шаг 11a BaseHub — 3 новых editor-меню (Hub Character prefab, Building SO Set, Base Hub Layout), миграция меты, enum как intValue в AudioConfig
metadata:
  type: project
---

Шаг 11a: BuildHubCharacterPrefabEditor (Setup), CreateBuildingSoSetEditor (Configs), Hub/BuildBaseHubLayoutEditor (новая папка Hub, ns FlatOutLike.Editor.Hub).

- Layout-скрипт сам доделывает отсутствующие SO/префаб персонажа (вызывает соседние меню), реюзит публичные хелперы: BuildMetaScenesEditor.CreateGameLifetimeScope/CreateGameBootstrap/RegisterAllScenesInBuildSettings, UIBuildHelpers.EnsureCanvasAndEventSystem, SetupCinemachineCameraRigEditor.EnsureBrain, SetupAudioEditor.WireOpenedScene, Bind*-методы.
- RegisterAllScenesInBuildSettings теперь чистит записи с отсутствующим файлом и «выведенные» сцены из Assets/Development/Scenes (не в реестре, не начинаются с "_") — без литералов имён удалённых сцен (grep по MainMenu/StuntsHub в Editor должен быть пуст).
- Enum с явными значениями (GameState) в SerializedProperty писать `intValue = (int)value`, не enumValueIndex; осиротевшие записи чистить через Enum.IsDefined.
- AnimatorController пересобирать на месте: RemoveParameter, RemoveState + удалить осиротевший BlendTree-сабассет (RemoveObjectFromAsset+DestroyImmediate), useAutomaticThresholds=false.
- Порядок: Build Mobile Input Canvas (в игровой сцене, добавляет HubGroup/HubStick в префаб) → Create Building SO Set → Build Hub Character Prefab → Build Base Hub Layout.
