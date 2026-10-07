---
name: project-editor-scene-path-literals
description: С 11b путь сцены в editor-коде — ScenePaths.For(SceneKeys.X) (Editor/Tools/ScenePaths.cs); литерал "Scenes/X.unity" в новом editor-коде — Warning, в runtime — Critical
metadata:
  type: project
---

11b закрыл хвост: `Assets/Development/Scripts/Editor/Tools/ScenePaths.cs` — единственное место сборки пути
(`Folder + key + ".unity"`); BuildBootstrapScene/BuildDerbyArena/BuildHighJumpScene/PopulateDerbyArenaHazards/
CreateDefaultRewardConfig/SetupCarDamageVisual/BuildBaseHubLayout переведены. Временная `_TestDriving` — const
ключ только в SetupCarDamageVisualEditor (в runtime SceneKeys её нет).
Остались дубли логики (не литералы): private `GetScenePath` в BuildMetaScenesEditor/BuildGameHudEditor/
BuildMetaUIEditor и константы `ScenesFolder = "Assets/Development/Scenes"` в нескольких файлах — Suggestion.

**Why:** arch-guard шаблон пути не ловит; рантайм-загрузка сцен идёт только через SceneKeys/GameStateSceneMap,
так что расхождение ломает лишь editor-меню при переименовании.

**How to apply:** новый editor-код с литералом пути сцены — Warning с фиксом «ScenePaths.For(SceneKeys.X)»;
Critical — только если литерал имени сцены появился в runtime-коде (LoadScene/сравнение имени).
