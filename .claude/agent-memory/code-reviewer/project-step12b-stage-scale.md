---
name: project-step12b-stage-scale
description: Шаг 12b — стадии стройки запекаются в префабы Prefabs/Hub/<Id>/ под вписанный след финала; пересборка по сигнатуре W/D в userData .meta (без H и состава деталей); VfxConfig_Hub отдельный от общего VfxConfig
metadata:
  type: project
---

R1 12b (2026-10-22) FAIL: стадии Tribunes собраны при F=7, финал при F=12 (разный масштаб); FX_Dust без материала.
R2 (2026-10-22) PASS: плита стадий = вписанный след финала (k = F/max(x,z), как FitVisual), сигнатура `hubStage:W=..;D=..` в AssetImporter.userData префаба стадии, пересборка при несовпадении, WarnIfStageStale в Build Base Hub Layout; пыль = CFXR Magic Poof; hub-VFX — в `VfxConfig_Hub.asset` (прошит только в BaseHub), общий VfxConfig.asset без hub-записей; параметры попа — в BuildingCatalogConfig.

**Why:** DoD «стадии одного масштаба» держится только при совпадении следа сборки и текущего финала/VisualFootprint.

**How to apply:**
- Проверка соответствия: userData в `Prefabs/Hub/*/*.prefab.meta` против VisualFootprint каталога (Tribunes 12×6.42, TicketBooth 3.83×7, HighJump 8×4 — клампнут 0.5·F, финал HighRamp = тонкая сетка, OQ пользователя).
- Сигнатура НЕ включает высоту финала H и состав GetParts — смена финала той же пропорции или правка деталей не пересобирает (потолки высот стадий устаревают).
- PresentationDefaults Tribunes в CreateBuildingSoSetEditor остался 7 при ассете 12 — при пересоздании каталога вернётся 7.
- `_vfxConfig` BaseHub прошивает только меню Build Hub Build VFX (WireScope раскладки его не трогает).
- Звук тика: кулдаун BuildInvestTick 0.1 с < InvestBatchSeconds 0.25 — при тюнинге батча ниже 0.1 тики глотаются.
Связано: [[project-step12a-build-loop]], [[hotspot_pool_create_lifecycle]].
