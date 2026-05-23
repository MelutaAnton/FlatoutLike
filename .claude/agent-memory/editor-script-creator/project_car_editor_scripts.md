---
name: project-car-editor-scripts
description: Step 2 editor scripts for single-car driving test — config creators, car prefab builder, test scene builder. Key API quirks and project structure observations.
metadata:
  type: project
---

## Car editor scripts created (step 2 "one car, no enemies")

Four scripts under `Assets/Development/Scripts/Editor/`:
- `Configs/CreateDefaultCarStatsConfigEditor.cs` — `FlatOutLike/Configs/Create Default CarStatsConfig`
- `Configs/CreateDefaultChaseCameraConfigEditor.cs` — `FlatOutLike/Configs/Create Default ChaseCameraConfig`
- `Setup/BuildCarPrefabFromFbxEditor.cs` — `FlatOutLike/Setup/Build Car Prefab From Selected FBX`
- `Setup/BuildDrivingTestSceneEditor.cs` — `FlatOutLike/Setup/Build Driving Test Scene`

**Why:** Automates step 2 of the project bootstrap — no manual Inspector work.
**How to apply:** Run in order: Create CarStatsConfig → Create ChaseCameraConfig → Build Car Prefab (select ThirdParty prefab first) → Build Driving Test Scene.

## Key project structure facts

- `Assets/ThirdParty/Low Poly Destructible Cars/` contains Unity **prefabs** (`.prefab`), not FBX files. Meshes are `.asset` files in `/Mesh/`. Script 3 accepts `.prefab` or `.fbx` but the real workflow is selecting a prefab from `/Prefabs/` subfolder.
- `Assets/Development/Data/Configs/` existed but was empty (no Car/ or Camera/ subfolders) — scripts create them via `EnsureFolder`.
- `Assets/Development/Prefabs/Vehicles/` already exists.
- `Assets/Development/Scenes/` already exists (only Boot.unity inside).

## GameBootstrap namespace quirk

`GameBootstrap` class is in namespace `FlatOutLike` (root), NOT `FlatOutLike.GameBootstrap`. In editor scripts that are themselves in `FlatOutLike.Editor.Setup`, reference it with full qualifier `FlatOutLike.GameBootstrap` everywhere a type is needed. Do NOT add `using FlatOutLike.GameBootstrap` — that namespace does not exist.

## CarController SerializedField names (verified from source)

`_rigidbody`, `_driveWheels` (WheelCollider[]), `_steerWheels` (WheelCollider[]), `_wheelVisuals` (Transform[]), `_config` (CarStatsConfig).
Rear-wheel drive default: driveWheels = [RL, RR], steerWheels = [FL, FR].

## ChaseCarCameraBehaviour SerializedField names (verified)

`_target` (Transform), `_config` (ChaseCameraConfig). Has public `SetTarget(Transform)` but use SerializedObject approach to persist changes in scene.

## Light search pattern (Unity 6)

Use `Object.FindObjectsByType<Light>(FindObjectsSortMode.None)` (new API, not deprecated `FindObjectsOfType`), then filter by `l.type == LightType.Directional`.
