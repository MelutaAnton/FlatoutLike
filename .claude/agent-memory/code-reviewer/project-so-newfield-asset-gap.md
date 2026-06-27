---
name: project-so-newfield-asset-gap
description: Review hot-spot — adding [SerializeField] fields to a config that already has .asset instances needs an explicit re-serialize step
metadata:
  type: project
---

Когда diff добавляет новые `[SerializeField]` поля в существующий ScriptableObject-конфиг (напр. `EnemyPersonalityConfig`), всегда проверяй, есть ли уже сохранённый `.asset` на диске (`Assets/Development/Data/Configs/...`). Существующий YAML-asset НЕ содержит новых полей → при reimport их значения не гарантированно берутся из C#-инициализаторов (`= 0.35f`), и в худшем случае десериализуются как `0`, что инвертирует фикс (напр. MinThrottle=0 → машина встаёт вместо тарана).

**Why:** Editor-скрипты вида `CreateDefault*Editor` идемпотентны через "asset exists → skip" (см. `CreateDefaultEnemyPersonalityConfigEditor.cs`), поэтому они НЕ дописывают новые поля в уже существующий asset. Механизма авто-апдейта значений в существующем asset нет.

**How to apply:** Если в diff новые SO-поля + существует соответствующий `.asset` без этих ключей в YAML — поднимай как Major. Грепай `Assets/**/Data/Configs/**/*.asset` на наличие новых ключей. Для `EnemyPersonalityConfig` upsert-механизм УЖЕ существует: `CreateDefaultEnemyPersonalityConfigEditor.SyncNewTuningFields` (меню `FlatOutLike/Configs/Create Default EnemyPersonalityConfig`) дописывает поля из `fieldNames`-списка через SerializedObject в существующий asset, GUID сохраняется. При добавлении нового tuning-поля надо: (1) добавить имя поля в массив `fieldNames`, (2) ПОСЛЕ импорта в Unity выполнить пункт меню — иначе значения де　сериализуются как 0. Т.е. требование не «написать скрипт», а «обновить fieldNames + прогнать sync». Наблюдалось 2026-06: asset имел `_stuckTime`, но НЕ имел `_circleBreakTime`/`_graceTime` (+ держал удалённые `_minThrottle`/`_reverseAlignmentDot`) → sync не прогонялся после правки.
