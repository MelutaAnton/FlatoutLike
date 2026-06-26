---
name: step3-damage-vfx-scripts
description: Step 3 editor scripts for combat damage + VFX visual system (DamageConfig, VfxConfig, CarDamageVisualConfig, ParticleController setup, CarDamageVisualController wiring, crash wall, GameLifetimeScope refs fill)
metadata:
  type: project
---

## Editor scripts created for Step 3 (Урон и визуал)

7 scripts total, all under `Assets/Development/Scripts/Editor/`.

### Configs (Editor/Configs/)

| File | MenuItem | Asset created |
|---|---|---|
| `CreateDefaultDamageConfigEditor.cs` | `FlatOutLike/Configs/Create Default DamageConfig` | `Assets/Development/Data/Configs/Combat/DamageConfig.asset` |
| `CreateDefaultVfxConfigEditor.cs` | `FlatOutLike/Configs/Create Default VfxConfig` | `Assets/Development/Data/Configs/Vfx/VfxConfig.asset` |
| `CreateDefaultCarDamageVisualConfigEditor.cs` | `FlatOutLike/Configs/Create Default CarDamageVisualConfig` | `Assets/Development/Data/Configs/Car/CarDamageVisualConfig.asset` |

### Setup (Editor/Setup/)

| File | MenuItem | What it does |
|---|---|---|
| `SetupVfxPrefabsEditor.cs` | `FlatOutLike/Setup/Setup VFX Prefabs` | On selected prefab assets: set stopAction=Callback, add ParticleController, wire _particleSystem |
| `SetupCarDamageVisualEditor.cs` | `FlatOutLike/Setup/Setup Car Damage Visual` | Add CarDamageVisualController to car in _TestDriving, fill all SerializedFields |
| `AddCrashWallToTestDrivingEditor.cs` | `FlatOutLike/Setup/Add Crash Wall to Test Driving` | Create static BoxCollider wall at (0,1.5,20) for collision test |
| `FillGameLifetimeScopeRefsEditor.cs` | `FlatOutLike/Setup/Fill GameLifetimeScope Refs (Step 3)` | Fill _damageConfig and _vfxConfig on GameLifetimeScope |

## Key field names for serialized classes

**DamageConfig** (`FlatOutLike.Data.Configs.Combat`): `_damageScale`, `_minImpactSpeed`, `_wallMassRatio`

**VfxConfig** (`FlatOutLike.Data.Configs.Vfx`): `_entries` (VfxEntry[] — empty by default, fill after VFX prefabs created)

**CarDamageVisualConfig** (`FlatOutLike.Data.Configs.Car`): `_stages` (DamageStage[])
- DamageStage struct fields (PUBLIC, no underscore): `hpThreshold`, `materials`, `decalPrefabsToSpawn`, `damagedMesh`, `blendShapeWeights`, `vfxToActivate`, `enableDetachableParts`
- 3 stages: Pristine(1.0) / Dented(0.66) / Wrecked(0.33, enableDetachableParts=true)
- Pristine.materials filled from `Car_Sport_09.prefab` first Renderer.sharedMaterials

**CarDamageVisualController** (`FlatOutLike.Controllers.Car`): `_statsConfig`, `_visualConfig`, `_car`, `_bodyRenderer`, `_bodyMeshFilter`, `_skinnedRenderer`, `_detachableParts`

**GameLifetimeScope** (`FlatOutLike.Installers`): `_damageConfig`, `_vfxConfig` — BOTH present (added in Step 3)

**ParticleController** (`FlatOutLike.Controllers.Vfx`): `_particleSystem`

## VFX prefab setup pattern

Uses `PrefabUtility.LoadPrefabContents(path)` + `SaveAsPrefabAsset(root, path)` + `UnloadPrefabContents(root)`.
ParticleSystem.stopAction set via `var main = ps.main; main.stopAction = ParticleSystemStopAction.Callback` — property setter writes through to native.

## Recommended run order for Step 3

1. `FlatOutLike/Configs/Create Default DamageConfig`
2. `FlatOutLike/Configs/Create Default VfxConfig`
3. `FlatOutLike/Configs/Create Default CarDamageVisualConfig`
4. `FlatOutLike/Setup/Setup VFX Prefabs` (select particle prefabs in Project first)
5. `FlatOutLike/Setup/Setup Car Damage Visual`
6. `FlatOutLike/Setup/Add Crash Wall to Test Driving`
7. `FlatOutLike/Setup/Fill GameLifetimeScope Refs (Step 3)`

**Why:** script 3 may need Car_Sport_09.prefab to exist (created by script from step 2 workflow). Scripts 5-7 need the scene and configs from prior steps.
**How to apply:** When asked to create additional Step 3 scripts or extend damage/VFX system, check these field names and the run order above.
