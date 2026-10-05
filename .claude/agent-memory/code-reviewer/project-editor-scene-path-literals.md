---
name: project-editor-scene-path-literals
description: Editor-скрипты повсеместно держат литералы путей сцен ("Assets/Development/Scenes/X.unity") при наличии SceneKeys; оценивать как Warning, не Critical
metadata:
  type: project
---

В Editor-скриптах (BuildBootstrapSceneEditor, BuildDerbyArenaEditor, PopulateDerbyArenaHazardsEditor,
CreateDefaultRewardConfigEditor, SetupCarDamageVisualEditor, с 11a — BuildBaseHubLayoutEditor) путь сцены
задан `private const string ScenePath = "Assets/Development/Scenes/<Name>.unity"`, хотя имя сцены есть в
`Models/SceneKeys`. Правильный образец — `GetScenePath(sceneKey) => ScenesFolder + "/" + sceneKey + ".unity"`
(BuildMetaScenesEditor/BuildGameHudEditor/BuildMetaUIEditor, но он везде private).

**Why:** arch-guard этот шаблон не ловит, прецедент сложился до пайплайна ревью; рантайм-загрузка сцен идёт
только через SceneKeys/GameStateSceneMap, так что расхождение ломает лишь editor-меню при переименовании.

**How to apply:** в новых editor-скриптах — Warning с фиксом «собрать путь из SceneKeys»; Critical — только
если литерал имени сцены появился в runtime-коде (LoadScene/сравнение имени). Кандидат на общий
`EditorScenePaths.For(sceneKey)` при следующем аудите.
