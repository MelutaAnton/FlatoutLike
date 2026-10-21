---
name: step12b-hub-stages-editor
description: Шаг 12b — editor-скрипты стадий стройки BaseHub (префабы стадий, VFX-варианты, аудио-строки); приёмы, не очевидные из кода
metadata:
  type: project
---

Шаг 12b (editor-часть) написан 2026-10-22, меню НЕ прогонялось автором (прогон — оркестратор через MCP).

Приёмы, которые стоит переиспользовать:
- Сборка префаба без пачкания сцены пользователя: `EditorSceneManager.NewPreviewScene()` + `PrefabUtility.InstantiatePrefab(asset, parent)` + `SaveAsPrefabAsset` + `ClosePreviewScene` в finally. Bounds деталей — через `renderer.localBounds` и матрицу в оси корня (не зависит от сцены).
- Префаб-ВАРИАНТ ThirdParty-эффекта: сначала `SaveAsPrefabAsset(инстанс источника)` (= Variant), потом правка через `LoadPrefabContents` -> API -> `SaveAsPrefabAsset` -> `Unload`, затем контрольная проверка загрузкой ассета. Правка API на сцен-инстансе до сохранения overrides может не записать.
- Пути стадий/финалов зданий живут ТОЛЬКО в `CreateBuildingSoSetEditor.HubStagePathsTable`; `BuildHubStagePrefabsEditor` их читает.
- `FitVisual` в BuildBaseHubLayout применяется к пустырю, финалу и зданиям «построено со старта»; промежуточные префаб-стадии — `ResetLocal` (они уже в метрах).

**Why:** промежуточные Synty-стадии собираются в реальных метрах под VisualFootprint, автомасштаб ломал бы пропорции.
**How to apply:** новые стадийные префабы/здания — добавлять строку в HubStagePathsTable и описание в `GetParts`.
