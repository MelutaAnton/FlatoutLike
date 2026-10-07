---
name: step11b-launch-zones
description: Шаг 11b BaseHub — editor-часть зон запуска, HubHud, LaunchConfirmCanvas; ScenePaths-хелпер; паттерны (миграция каталога по intValue, EnsureLabel без затирания текста)
metadata:
  type: project
---

- `CreateBuildingSoSetEditor.MigrateLaunchZones()` (public) ставит зону только у entry с `HasLaunchZone==false && LaunchState==0`; enum пишется `intValue=(int)GameState`. Вызывается и из `BuildBaseHubLayoutEditor.EnsurePrerequisiteAssets` (до OpenScene).
- Здания хаба смотрят локальной +Z к центру (DerbyArena 180, Garage 90, HighJump -90) → зона = локальный (0,0,7), размер (6,3,6).
- `BuildBaseHubLayoutEditor`: SyncLaunchZone/SyncMarker (Quad + свой URP Unlit transparent материал `Art/Materials/HubLaunchZoneMarker.mat`, текстура = sprite.texture локальной копии CircleEmpty), EnsureHubHud(canvas), EnsureLaunchConfirmCanvas() (world-space, rotation = LookRotation(-FollowOffset) из HubCameraConfig), WireScope прошивает `_buildingCatalogConfig/_hubHudView/_launchConfirmView`.
- `UIBuildHelpers.ConfigureTapButton(Button)` — чек-лист п.7 (ColorTint, targetGraphic, selected=normal, Navigation None). `CreateButton`/`CreateLabel` затирают текст → при повторном прогоне передавать label=null / использовать EnsureLabel (reuse существующего TMP).
- `Tools/ScenePaths.For(SceneKeys.X)` — единственный сборщик путей сцен; `_TestDriving` не в SceneKeys → const-ключ в SetupCarDamageVisualEditor. `python3` на машине — заглушка Windows Store, правки только через Edit/sed.
- `BuildMobileInputCanvasEditor.EnsureHubGroupInactiveInPrefab()` — префаб-миграция без сцены.
