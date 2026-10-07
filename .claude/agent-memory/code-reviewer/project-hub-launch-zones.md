---
name: project-hub-launch-zones
description: С 11b зона запуска HighJump активна БЕЗ гейтинга стройкой — это не отклонение (§19 шаг 11 «все режимы через зоны»), гейт — шаг 12; где живёт логика зон
metadata:
  type: project
---
11b: `HubLaunchService` (EntryPoint, условно по `_buildingCatalogConfig` на scope) + `LaunchZoneController` (инъекция `FindObjectsByType` в build callback — прецедент EjectTriggerZone/Destructible) + `LaunchConfirmPresenter` (опрос сервиса из `LaunchConfirmView.Update`). Данные зон — поля `BuildingEntry` (HasLaunchZone/LaunchState/Offset/Size) в `BuildingCatalogConfig`; миграция — `CreateBuildingSoSetEditor.MigrateLaunchZones`.

Зона HighJumpTower в 11b запускает режим без условия стройки: §19 шаг 11 ставит цель «все режимы запускаются через зоны», а гейтинг (§16 Stunts п.1) — DoD шага 12. Не считать неучтённым отклонением.

**Why:** MainMenu/StuntsHub удалены в 11a — без зоны High Jump был бы недостижим до шага 12.

Watch (11b раунд 2, 2026-10-07): `LaunchZoneOffset` — от ЦЕНТРА корня здания, а визуал автомасштабируется под `VisualFootprint` (Derby 10 м → полуширина 5 > offset 4) → зона/круг частично внутри здания с коллайдером. `MigrateLaunchZones` не трогает инициализированные entry — правка offset только данными. `MigrateBuildingPresentation` (fill-if-empty/<=0) гоняется каждым Build Base Hub Layout и ломает документированный opt-out «пусто/0 = выкл».

Раунд 3 (2026-10-07): offset = footprint/2 + size.z/2 + 0.5 (ComputeLaunchZoneForward) + флаг `_presentationDefaultsApplied` закрыли оба watch-пункта, НО вынесли зону Derby (центр (0,5.5)) в перекрытие 0.5 м с зонами Garage (-5.5,4) / HighJump (5.5,4) при 6×6. `HubLaunchService` = «последняя вошедшая», без fallback на оставшуюся занятую зону → выход из перекрытия оставляет персонажа в зоне Derby без таймера. При любой правке раскладки/offset/size — пересчитывать XZ-боксы зон на пересечение (с учётом радиуса капсулы 0.35).

Раунд 4 (2026-10-07): закрыто. `HubLaunchService` держит `List<BuildingId>` занятых зон (последняя = активная, выход из активной → fallback на последнюю оставшуюся с нуля); раскладка ±15 даёт зазор между зонами 1.5 м (> диаметр капсулы 0.7); `BuildBaseHubLayoutEditor.CheckLaunchZoneOverlaps` — AABB-проверка XZ (без учёта капсулы). Корректность fallback опирается на ОДИН коллайдер у HubCharacter (CharacterController) — если на персонаже появятся доп. коллайдеры, Enter/Exit пойдут per-collider и список рассинхронится.

**How to apply:** на ревью шага 12 проверить, что гейтинг сделан декларативно (unlock payload в SO по `BuildingCompletedEvent`), а не `if (id == HighJumpTower)` в `HubLaunchService`. Связано: [[project-hub-camera-outside-rig]].
