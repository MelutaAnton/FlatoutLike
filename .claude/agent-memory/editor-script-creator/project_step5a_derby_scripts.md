---
name: project-step5a-derby-scripts
description: 4 editor-скрипта под-шага 5a Derby — enemy prefab, арена, спавн-поинты, DerbyMatchConfig
metadata:
  type: project
---

## Под-шаг 5a Derby — созданные Editor-скрипты

**Когда:** 2026-06-29

### Скрипты и пункты меню

| Файл | MenuItem |
|---|---|
| `Editor/Setup/BuildEnemyCarPrefabEditor.cs` | `FlatOutLike/Setup/Build Enemy Car Prefab` |
| `Editor/Setup/BuildDerbyArenaEditor.cs` | `FlatOutLike/Scenes/Build Derby Arena` |
| `Editor/Setup/PopulateDerbyArenaSpawnPointsEditor.cs` | `FlatOutLike/Spawn/Populate Derby Arena Spawn Points` |
| `Editor/Configs/CreateDefaultDerbyMatchConfigEditor.cs` | `FlatOutLike/Configs/Create Default DerbyMatchConfig` |

### DerbyMatchConfig.asset

Путь: `Assets/Development/Data/Configs/Match/DerbyMatchConfig.asset`

Поля:
- `_playerCarPrefab` → `Car_Sport_09.prefab`
- `_enemyCarPrefab` → `Car_Enemy.prefab`
- `_playerStats` → `CarStatsConfig_Default.asset`
- `_enemies[3]` — EnemySpawnEntry (struct) с полями `profile` / `stats`; в 5a все 3 = Aggressor+Default

SerializedObject-паттерн для struct-массива:
```csharp
element.FindPropertyRelative("profile").objectReferenceValue = aggressorProfile;
element.FindPropertyRelative("stats").objectReferenceValue   = carStats;
```

### GameLifetimeScope на арене

Derby-арена: `_sceneCars.arraySize = 0` (CarFactory-путь), `_derbyMatchConfig` + `_spawnPoints` заполнены.
`_damageConfig` и `_vfxConfig` тоже заполняются в CreateDefaultDerbyMatchConfig (нужны для DamageService/VfxService).

### Арена Derby_Arena_01

Сцена: `Assets/Development/Scenes/Derby_Arena_01.unity`  
Размер арены: 60×60 единиц (ArenaHalfSize=30)  
Стены: 4 невидимых BoxCollider на слое Wall (WallNorth/South/East/West)  
Пол: Plane (scale 6×1×6) с MeshCollider  
StadiumVisual: ссылка на `battle_stadium_003.prefab` (ThirdParty, без правок)  
Камера: статичная (0, 40, -25), rotation (70,0,0) — упрощение 5a, chase НЕ привязан  

### SpawnPoints

7 точек по кругу радиуса 18 ед., y=0.5, имена `SpawnPoint_0..6`.  
Родитель: `SpawnPoints`.  
Каждая смотрит горизонтально в центр (0,0,0).

### Особенность prefab-contents контекста

`PrefabUtility.LoadPrefabContents()` создаёт preview-сцену.  
Внутри блока: `AddComponent` напрямую (без Undo — Undo в preview-сцене не работает корректно).  
`ConvertCar()` из `SetupCarDamageShaderEditor` вызывается в этом контексте — Undo.RecordObject — no-op, но назначение материалов сохраняется через `SaveAsPrefabAsset`.  
ОБЯЗАТЕЛЬНО: `PrefabUtility.UnloadPrefabContents(prefabRoot)` в `finally`.

### Порядок запуска (полный для 5a)

1. `FlatOutLike/Setup/Create Physics Layers` (слои Wall/Vehicle/Hazard/Pickup)
2. `FlatOutLike/Configs/Create Default DamageConfig`
3. `FlatOutLike/Configs/Create Default VfxConfig`
4. `FlatOutLike/Configs/Create Default EnemyPersonalityConfig`
5. `FlatOutLike/Configs/Create Default AISensorConfig`
6. `FlatOutLike/Setup/Build Enemy Car Prefab`  ← новый 5a
7. `FlatOutLike/Scenes/Build Derby Arena`       ← новый 5a
8. `FlatOutLike/Spawn/Populate Derby Arena Spawn Points` ← новый 5a
9. `FlatOutLike/Configs/Create Default DerbyMatchConfig` ← новый 5a

**Why:** Derby-матч спавнит машины через CarFactory.SpawnMatch(config, spawnPoints), поэтому нужны:
префаб врага с EnemyCarController + конфиг с точками спавна + GameLifetimeScope с обеими ссылками.

**How to apply:** При следующих запросах на 5b/5c/5d — эти скрипты уже есть; не создавать повторно.
