---
name: project-step5b-personality-configs
description: Под-шаг 5b — 2 новых профиля AI (Coward/PlayerHunter) и разводка профилей в DerbyMatchConfig
metadata:
  type: project
---

## Под-шаг 5b — Editor-скрипты для профилей EnemyPersonalityConfig

**Когда:** 2026-06-30

### Изменённые файлы

| Файл | Что изменилось |
|---|---|
| `Editor/Configs/CreateDefaultEnemyPersonalityConfigEditor.cs` | SyncNewTuningFields поддерживает enum-поля; добавлены 2 новых MenuItem и хелпер CreateOrSyncDerivedProfile |
| `Editor/Configs/CreateDefaultDerbyMatchConfigEditor.cs` | Загружает 3 профиля, entry[0]=Aggressor / [1]=Coward / [2]=PlayerHunter |

### Новые MenuItem'ы

- `FlatOutLike/Configs/Create EnemyPersonalityConfig Coward`
- `FlatOutLike/Configs/Create EnemyPersonalityConfig PlayerHunter`

### Профили AI

| Профиль | TargetSelectionMode | retreatHpThreshold | ramAggression |
|---|---|---|---|
| Aggressor | Nearest (0) | дефолт класса | дефолт класса |
| Coward | Weakest (1) | 0.35 | 0.6 |
| PlayerHunter | Player (2) | 0 | 1.0 |

### Паттерн enum-поля в SerializedObject

```csharp
var prop = so.FindProperty("_targetSelectionMode");
prop.enumValueIndex = 0; // 0=Nearest, 1=Weakest, 2=Player
```

### Паттерн CopyAsset для производных профилей

```csharp
bool copied = AssetDatabase.CopyAsset(AggressorPath, targetPath);
AssetDatabase.Refresh();
// затем SerializedObject → переопределить только отличающиеся поля
```

Копирование через CopyAsset сохраняет весь отыгранный тюнинг движения Aggressor.
GUID нового файла создаётся Unity автоматически и не меняется при повторном запуске (ассет уже существует).

### SyncNewTuningFields — разделение float/enum

Метод теперь читает defaultFloatValues (dict<string,float>) И defaultEnumValues (dict<string,int>) ДО DestroyImmediate временного инстанса, затем применяет раздельными циклами через floatValue / enumValueIndex.

### Порядок запуска (полный для 5b)

1–6. Те же что в 5a (Physics Layers, DamageConfig, VfxConfig, EnemyPersonalityConfig/Aggressor, AISensorConfig, Build Enemy Car Prefab)
7. `FlatOutLike/Configs/Create EnemyPersonalityConfig Coward`
8. `FlatOutLike/Configs/Create EnemyPersonalityConfig PlayerHunter`
9. `FlatOutLike/Scenes/Build Derby Arena`
10. `FlatOutLike/Spawn/Populate Derby Arena Spawn Points`
11. `FlatOutLike/Configs/Create Default DerbyMatchConfig`  ← теперь пишет 3 разных профиля

**Why:** Разнообразие профилей AI делает Derby-матч менее однообразным; Coward добивает слабых,
PlayerHunter давит конкретно игрока, Aggressor таранит ближайшую цель.

**How to apply:** При следующих запросах на 5c+ — все 3 профиля уже созданы; не воссоздавать.
