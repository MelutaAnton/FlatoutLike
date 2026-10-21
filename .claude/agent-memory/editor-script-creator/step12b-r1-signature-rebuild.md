---
name: step12b-r1-signature-rebuild
description: Паттерн «сигнатура входов в AssetImporter.userData» для пересборки editor-генерируемых префабов + правила VFX-кандидатов (шаг 12b R1)
metadata:
  type: project
---

Префабы стадий Hub (BuildHubStagePrefabsEditor) пересобираются по сигнатуре `hubStage:W=..;D=..` в `AssetImporter.userData` (+SaveAndReimport); общий публичный helper (`ResolveFinalDims`/`BuildSignature`/`WarnIfStageStale`) читает и Build Base Hub Layout. Финал измеряется в preview-сцене (identity, без TMP/частиц — как FitVisual), k = VisualFootprint / max(x,z).

**Why:** плита F×F была шире узких финалов, а смена VisualFootprint в каталоге молча оставляла старые префабы.
**How to apply:** любой editor-генерируемый ассет, зависящий от внешнего входа, помечать сигнатурой входов; не дублировать формулу — один public static helper. VFX-кандидаты из ThirdParty отбраковывать по `ParticleSystemRenderer.sharedMaterial == null` (FX_Dust без материала); hub-VFX живут в отдельном `VfxConfig_Hub.asset` (путь — const в BuildHubBuildVfxEditor). См. [[step8c-meta-ui]] для общих правил.
