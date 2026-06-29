---
name: step5c-pickup-scripts
description: 4 скрипта для pickup'ов (Build Pickup Prefabs / Create Default PickupConfig / Populate Pickup Zones / Add Kinematic Rigidbody To Triggers) + расширение DerbyMatchConfig
metadata:
  type: project
---

## Шаг 5c — Editor-скрипты для pickup'ов

### Созданные файлы
- `Assets/Development/Scripts/Editor/Setup/BuildPickupPrefabsEditor.cs`
  - Меню: `FlatOutLike/Setup/Build Pickup Prefabs`
  - Создаёт Pickup_Repair.prefab и Pickup_Nitro.prefab
  - Материалы: `Assets/Development/Art/Materials/Pickups/Pickup_Repair.mat` (зелёный) / `Pickup_Nitro.mat` (синий)
  - Паттерн: если prefab существует — LoadPrefabContents → update → SaveAsPrefabAsset → Unload; иначе — CreatePrimitive(Sphere) → configure → SaveAsPrefabAsset → DestroyImmediate
  - Компоненты: SphereCollider(isTrigger=true, radius=1.5) + Rigidbody(isKinematic=true) + RepairPickup/NitroPickup
  - Слой: `Layers.Pickup` (LayerMask.NameToLayer)

- `Assets/Development/Scripts/Editor/Configs/CreateDefaultPickupConfigEditor.cs`
  - Меню: `FlatOutLike/Configs/Create Default PickupConfig`
  - Ассет: `Assets/Development/Data/Configs/Pickups/PickupConfig.asset`
  - Поля: `_repairFraction=0.4f`, `_nitroFillFraction=1.0f`, `_respawnInterval=8f` (floatValue; если int — сменить на intValue)
  - `_entries[2]`: type(enumValueIndex), prefab(objectReferenceValue), poolSize(intValue)

- `Assets/Development/Scripts/Editor/Setup/PopulatePickupZonesEditor.cs`
  - Меню: `FlatOutLike/Spawn/Populate Pickup Zones`
  - Создаёт иерархию `PickupZones/PickupZone_0` (Repair, pos=10,0.5,10) и `PickupZone_1` (Nitro, pos=-10,0.5,-10)
  - Заполняет `GameLifetimeScope._pickupZones`: FindPropertyRelative("point"/"type")
  - MarkSceneDirty + SaveScene

- `Assets/Development/Scripts/Editor/Setup/AddKinematicRigidbodyToTriggersEditor.cs`
  - Меню: `FlatOutLike/Setup/Add Kinematic Rigidbody To Selected Triggers`
  - Утилита: для выделенных в Hierarchy объектов с isTrigger=true добавляет Rigidbody(isKinematic=true, useGravity=false)
  - Использует `Undo.AddComponent<Rigidbody>(go)` + MenuItem validator (только при наличии Selection)

### Изменённые файлы
- `CreateDefaultDerbyMatchConfigEditor.cs`: добавлен `using FlatOutLike.Data.Configs.Pickups`, const `PickupConfigPath`, шаг 8b заполняет `_pickupConfig`
  - `FindProperty("_pickupConfig")` — null-safe: если поле не найдено в Scope → LogWarning без краша
  - `_pickupZones` заполняет PopulatePickupZones (отдельно), не DerbyMatchConfig

### Namespaces runtime-классов
- `FlatOutLike.Controllers.Pickups.RepairPickup`
- `FlatOutLike.Controllers.Pickups.NitroPickup`
- `FlatOutLike.Data.Configs.Pickups.PickupConfig`
- `FlatOutLike.Services.Pickups.PickupZone` (struct, поля: `point` Transform, `type` PickupType)
- `FlatOutLike.Models.PickupType` enum: Repair=0, Nitro=1

- `Assets/Development/Scripts/Editor/Setup/WirePickupRefsEditor.cs`
  - Меню: `FlatOutLike/Setup/Wire Pickup Refs`
  - Находит GameLifetimeScope в сцене, загружает PickupConfig по пути, собирает маркеры из `PickupZones/PickupZone_*`
  - Заполняет `_pickupConfig` и `_pickupZones` через SerializedObject
  - Тип зоны: `i % 2 == 0 → Repair (enumValueIndex=0)`, иначе `Nitro (1)`
  - Вызывает ApplyModifiedProperties + MarkSceneDirty; Undo collapse

### Порядок запуска для шага 5c (полный)
1. `FlatOutLike/Setup/Create Physics Layers` (уже был — Pickup слой там)
2. `FlatOutLike/Setup/Build Pickup Prefabs` ← NEW 5c
3. `FlatOutLike/Configs/Create Default PickupConfig` ← NEW 5c
4. `FlatOutLike/Spawn/Populate Pickup Zones` ← NEW 5c (нужна Derby сцена открытой; уже заполняет _pickupZones)
5. `FlatOutLike/Setup/Wire Pickup Refs` ← NEW 5c (standalone wire: _pickupConfig + переписать _pickupZones)
6. `FlatOutLike/Configs/Create Default DerbyMatchConfig` (также проводит _pickupConfig)

### Паттерн EnsureMaterial
Для URP/Lit: `Shader.Find("Universal Render Pipeline/Lit")`, затем `mat.SetColor("_BaseColor", color)`.
Fallback на `Shader.Find("Standard")` с LogWarning если URP не найден.

**Why:** шаг 5c пикапы — Repair восстанавливает HP, Nitro заряжает нитро; зоны расставлены вне
радиуса спавна машин (SpawnRadius=18), у углов арены на (±10, 0.5, ±10).

**How to apply:** при следующем добавлении pickup-типов: добавить entry в PickupConfig + маркер
в PopulatePickupZonesEditor. BuildPickupPrefabs поддерживает generic `BuildOrUpdatePrefab<T>`.
