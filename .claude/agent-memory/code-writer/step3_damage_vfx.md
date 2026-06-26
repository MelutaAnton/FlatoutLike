---
name: step3-damage-vfx
description: Step 3 damage & VFX stack — new services, events, configs, factories added in vertical slice step 3
metadata:
  type: project
---

## New Services Added (Step 3)

| Type | Registration | Interface |
|------|-------------|-----------|
| DamageService | `Register<DamageService>(Singleton).As<IDamageService>()` | IDamageService |
| VfxFactory | `Register<VfxFactory>(Singleton).As<IVfxFactory>()` | IVfxFactory |
| VfxService | `Register<VfxService>(Singleton).As<IVfxService>()` | IVfxService |
| CarDamageVisualController | `RegisterComponentInHierarchy<CarDamageVisualController>()` | MonoBehaviour |

## New SO Configs (Step 3)

- `DamageConfig` — `menuName = "FlatOutLike/Combat/DamageConfig"`. Fields: DamageScale (1), MinImpactSpeed (3), WallMassRatio (1).
- `CarDamageVisualConfig` — `menuName = "FlatOutLike/Car/CarDamageVisualConfig"`. Contains `DamageStage[]` with hpThreshold, materials, decalPrefabsToSpawn, damagedMesh, blendShapeWeights, vfxToActivate, enableDetachableParts.
- `VfxConfig` — `menuName = "FlatOutLike/Vfx/VfxConfig"`. Contains `VfxEntry[] { type, prefab, poolSize }`.

Both registered via `builder.RegisterInstance(_damageConfig)` / `builder.RegisterInstance(_vfxConfig)` as SerializeField on GameLifetimeScope.

## New Events (Core/Events/Events.Derby.cs)

- `CarCollisionEvent(CarId Attacker, CarId Victim, float ImpactSpeed, Vector3 Point)` — published by CarController.OnCollisionEnter
- `CarDamagedEvent(CarId Target, float Damage, float RemainingHp)` — published by DamageService
- `CarDestroyedEvent(CarId Target, CarId Killer)` — published by DamageService

## New Enums (Models)

- `VfxEffectType` in `FlatOutLike.Models`: `CollisionSparks`, `DamageSmoke`, `DamageFire`

## CarController Changes

Added `[SerializeField] private int _carId = 0` + `public CarId CarId => new CarId(_carId)`.
Construct now requires `IDamageService damage, IEventBus eventBus` — calls `damage.RegisterCar(CarId, maxArmor, mass)` and stores `_eventBus`.
`OnCollisionEnter` publishes `CarCollisionEvent` with otherCar's CarId as Attacker (or CarId.None for walls).

## VfxFactory Pattern

- Pure C# service with private per-effect `Queue<ParticleController>` pools (no generic IPoolManager in step 3).
- `Dictionary<ParticleController, VfxEffectType> _controllerTypes` for reverse pool-return lookup.
- Pre-warms pools on construction from VfxConfig entries.
- After `pool.Get()`: calls `resolver.InjectGameObject(go)` + `controller.Launch()` — NOT in OnEnable.
- `ParticleController.OnParticleSystemStopped()` → callback → controller deactivated → returned to pool.
- **Requires prefab to have ParticleSystem.Stop Action = Callback** (set by editor script).

## Subscription Pattern for CarDamageVisualController

Subscribes to `CarDamagedEvent` in BOTH `Construct` AND `OnEnable` (with `_initialized` guard).
- Construct fires: subscribes directly (because OnEnable already fired without subscribing).
- OnEnable on re-enable: `if (!_initialized) return; _eventBus.Subscribe(...)`.
- OnDisable: unsubscribes.
- OnDestroy: unsubscribes (safety net).

## Stage Selection Algorithm

Stages sorted descending by hpThreshold [1.0, 0.66, 0.33].
Active stage = stage with minimum hpThreshold where hpThreshold >= currentRatio.
Fallback when ratio > all thresholds: index 0 (Pristine).
