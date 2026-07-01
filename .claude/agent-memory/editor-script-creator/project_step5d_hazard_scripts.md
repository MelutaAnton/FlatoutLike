---
name: step5d-hazard-scripts
description: Step 5d — 3 Editor-скрипта для hazard-префабов, DestructibleConfig и расстановки на арене
metadata:
  type: project
---

## Три скрипта Step 5d (Hazards)

### Скрипт 1: BuildHazardPrefabsEditor.cs
**Путь:** `Assets/Development/Scripts/Editor/Setup/BuildHazardPrefabsEditor.cs`
**Меню:** `FlatOutLike/Setup/Build Hazard Prefabs`

Паттерн создания hazard-префаба из ThirdParty:
```
root = new GameObject(name)
visual = PrefabUtility.InstantiatePrefab(thirdPartyPrefab, root.transform)  ← сохраняет prefab-link
visual.name = "Visual"
// добавить Rigidbody/Collider/Script на root
PrefabUtility.SaveAsPrefabAsset(root, outPath)
Object.DestroyImmediate(root)  // в finally
```

**Bounds для коллайдера:** `Renderer.bounds` (world-space) из дочерних рендереров работает в Editor.
BoxCollider.center = `bounds.center - root.transform.position` (root всегда на origin).

**Конфигурация объектов:**
- Hazard_Barrel (barrel_001.prefab): Rigidbody(20), BoxCollider, слой Hazard
- Hazard_ConcreteBlock (barrier_001.prefab): Rigidbody(150), BoxCollider, слой Hazard
- Hazard_Fence (fence_001.prefab): static (без RB), BoxCollider, DestructibleObjectController, слой Hazard; _config=null (заполняет скрипт 2)
- Debris_Plank_01 (deadwood_001.prefab): Rigidbody(3), BoxCollider, DebrisPieceController + _rigidbody, слой Default
- Debris_Plank_02 (deadwood_005.prefab): то же
- Debris_Scrap_01 (scrap_metal_001.prefab): то же
- Hazard_Tire (tire_001.prefab): Rigidbody(8), convex MeshCollider, слой Default
- Hazard_Wheel (car_wheel_002.prefab): Rigidbody(10), convex MeshCollider, слой Default (prefab существует!)
- Hazard_TireStack (tires_001.prefab): Rigidbody(20), CapsuleCollider, слой Default
- Hazard_Cactus_01/02/03 (cactus_001/006/011.prefab): Rigidbody(10), CapsuleCollider Y-axis, слой Default

**Рантайм-типы (другой агент):**
- `FlatOutLike.Controllers.Destructible.DestructibleObjectController` — поля: `_config`, `_intactVisual`, `_collider`
- `FlatOutLike.Controllers.Destructible.DebrisPieceController` — поле: `_rigidbody`
- `FlatOutLike.Data.Configs.Destructible.DestructibleConfig` — поля: `_breakImpulseThreshold`, `_debrisPrefabs`, `_debrisPieceCount`, `_debrisLifetime`, `_debrisScatterForce`, `_debrisPoolSize`

### Скрипт 2: CreateDefaultDestructibleConfigEditor.cs
**Путь:** `Assets/Development/Scripts/Editor/Configs/CreateDefaultDestructibleConfigEditor.cs`
**Меню:** `FlatOutLike/Configs/Create Default DestructibleConfig`

Создаёт `Assets/Development/Data/Configs/Destructible/DestructibleConfig.asset`.
Значения: breakImpulse=10, pieceCount=4, lifetime=5, scatterForce=200, poolSize=8.
_debrisPrefabs[3] = [Debris_Plank_01, Debris_Plank_02, Debris_Scrap_01].
Затем через LoadPrefabContents → SO → SaveAsPrefabAsset привязывает _config к Hazard_Fence.prefab.

### Скрипт 3: PopulateDerbyArenaHazardsEditor.cs
**Путь:** `Assets/Development/Scripts/Editor/Setup/PopulateDerbyArenaHazardsEditor.cs`
**Меню:** `FlatOutLike/Setup/Populate Derby Arena Hazards`

Открывает Derby_Arena_01.unity (если не открыта). Создаёт "Hazards" parent.
Расстановка (арена 60x60, spawn-ring radius=18, hazards ≤ radius=13):
- Ядро (radius 4-6): 2 Barrel + 1 ConcreteBlock + 2 Fence = 5 объектов
- TireStack ring (radius=8, 120°): 3 объекта
- Tire/Wheel ring (radius=10, 45°): 8 объектов (чередование)
- Cactus ring (radius=13, 45°, сдвиг 22.5°): 8 объектов
Итого: 24 инстанса.

### Порядок запуска Step 5d
1. Create Physics Layers
2. Build Derby Arena
3. Build Hazard Prefabs  ← скрипт 1
4. Create Default DestructibleConfig  ← скрипт 2
5. Populate Derby Arena Hazards  ← скрипт 3

### Нюансы
- `ConvexMesh` collider на root ссылается на `sharedMesh` из `MeshFilter` дочернего объекта — работает корректно если visual-child не имеет offset трансформа
- Fallback ConvexMesh → BoxCollider при отсутствии MeshFilter (с LogWarning)
- `Renderer.bounds` в edit mode возвращает корректные world-space bounds для инстансов в сцене (даже вне Play Mode)
- `PrefabUtility.InstantiatePrefab` при создании prefab из temp-GO: prefab-link сохраняется в итоговом ассете
