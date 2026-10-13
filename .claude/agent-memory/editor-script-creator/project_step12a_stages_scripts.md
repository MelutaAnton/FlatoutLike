---
name: step12a-stages-scripts
description: Шаг 12a editor-часть — стадии стройки BaseHub (Stages/Stage_N, Zone, Panel, BuildSiteController), миграция каталога по флагу _stagesDefaultsApplied
metadata:
  type: project
---

Правились CreateBuildingSoSetEditor (MigrateBuildStages, поля ZoneOffset/ZoneSize) и BuildBaseHubLayoutEditor (SyncStages/SyncZone/SyncPanel/SyncBuildSite). Написано против контракта ДО готовности рантайма (BuildStageEntry, Stages, StageCount, GetZoneWorldCenter, BuildSiteController, InvestZoneController, BuildingPanelView).

- Структура площадки: `Buildings/<Id>/Stages/Stage_k` (контейнеры с единичным трансформом, контент — ребёнок: префаб-инстанс или Cube "Placeholder"), `Zone`, `Panel`, `NameLabel`. Включён только Stage_0.
- Легаси-визуал (прямой префаб-ребёнок корня) переезжает в контейнер через Undo.SetTransformParent; оставшиеся прямые префаб-дети удаляются (RemoveLegacyVisuals).
- Зона: старый `LaunchZone` переименовывается в `Zone`; контроллеры добавляются/удаляются по HasLaunchZone / StageCount>0.
- Позиция подписи считается в SyncStages, пока все стадии активны (bounds), потом уходит в SyncNameLabel и SyncPanel.
- Панель: world-space Canvas без GraphicRaycaster, Fill использует встроенный `UI/Skin/UISprite.psd` (Filled без спрайта не заполняется).
- `_vacantStagePrefab` читается через SerializedObject (имя публичного свойства в контракте не фиксировано).
- Материалы заглушек: Art/Materials/HubStageVacant.mat, HubStagePlaceholder_{0..3}.mat (URP Lit, цвет только при создании).
