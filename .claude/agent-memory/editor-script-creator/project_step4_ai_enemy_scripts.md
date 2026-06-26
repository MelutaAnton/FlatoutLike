---
name: project-step4-ai-enemy-scripts
description: Step 4 AI Enemy editor scripts — field names, patterns, and menu sequence for EnemyPersonalityConfig / EnemyCarController setup
metadata:
  type: project
---

## Step 4 editor scripts created

### Files
- NEW `Assets/Development/Scripts/Editor/Configs/CreateDefaultEnemyPersonalityConfigEditor.cs`
  - Menu: `FlatOutLike/Configs/Create Default EnemyPersonalityConfig`
  - Asset: `Assets/Development/Data/Configs/Enemies/EnemyPersonalityConfig_Aggressor.asset`
  - No extra SO overrides needed — class defaults already match Aggressor profile

- NEW `Assets/Development/Scripts/Editor/Setup/SetupEnemyCarEditor.cs`
  - Menu: `FlatOutLike/Setup/Setup Enemy Car (Step 4)`
  - Finds player car as first CarController without an EnemyCarController sibling
  - Uses `PrefabUtility.GetCorrespondingObjectFromSource(playerCar.gameObject)` to get the exact same prefab; falls back to `FindFirstCarPrefab()` from Vehicles folder
  - Enemy spawn: position `(0, 1, 25)`, rotation `Euler(0, 180, 0)` (facing player)
  - Sets `CarController._carId = 1` via `SerializedObject` before filling EnemyCarController refs
  - Calls `SetupCarDamageShaderEditor.ConvertCar(enemyCar)` for damage shader on enemy

- MODIFIED `Assets/Development/Scripts/Editor/Setup/SetupCarDamageShaderEditor.cs`
  - Extracted `public static void ConvertCar(CarController carController)` from the existing menu method
  - Existing `ConvertCarBodyToDamageShader()` menu behavior is unchanged (finds via FindAnyObjectByType, then calls ConvertCar)
  - ConvertCar does NOT manage Undo groups — caller is responsible

- MODIFIED `Assets/Development/Scripts/Editor/Setup/FillGameLifetimeScopeRefsEditor.cs`
  - Added `_sceneCars` filling: `Object.FindObjectsByType<CarController>()` → `transform.root.gameObject`
  - Added alias: `[MenuItem("FlatOutLike/Setup/Fill GameLifetimeScope Refs (Step 4)")]` → calls `FillRefs()`
  - Step 3 menu item behavior is unchanged (still fills damageConfig + vfxConfig; now also fills _sceneCars)

## Field names (EnemyCarController private SerializeFields)
- `_car` — CarController (the AI-controlled car, sibling component)
- `_target` — CarController (player car)
- `_statsConfig` — CarStatsConfig
- `_profile` — EnemyPersonalityConfig
- `_rigidbody` — Rigidbody

## Field names (CarController)
- `_carId` — int (0=player, 1=enemy)
- `_config` — CarStatsConfig
- `_rigidbody` — Rigidbody

## Field names (GameLifetimeScope — Step 4 addition)
- `_sceneCars` — GameObject[] (all car root GameObjects; filled with transform.root.gameObject of each CarController)

## Correct run sequence for Step 4
1. `FlatOutLike/Setup/Build Driving Test Scene` (if not done in Step 2)
2. `FlatOutLike/Configs/Create Default EnemyPersonalityConfig`
3. `FlatOutLike/Setup/Setup Enemy Car (Step 4)`
4. `FlatOutLike/Setup/Fill GameLifetimeScope Refs (Step 4)`

## Key design decisions
- Player identification: CarController without EnemyCarController on same GO (not by name or _carId)
- Idempotency in SetupEnemyCar: checks for EnemyCarController component, not "EnemyCar" name
- Damage shader for enemy: calls SetupCarDamageShaderEditor.ConvertCar() directly — avoids FindAnyObjectByType ambiguity with two cars in scene
- _sceneCars filling uses `transform.root.gameObject` to always store the prefab root, not a child
