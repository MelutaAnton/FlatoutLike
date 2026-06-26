---
name: project-di-lazy-singletons
description: VContainer lazy-singleton activation pattern — services with ctor side effects (EventBus.Subscribe) are created only when a consumer pulls them
metadata:
  type: project
---

Lazy `Lifetime.Singleton` registrations in `GameLifetimeScope` are NOT eagerly instantiated. A service whose constructor has side effects (e.g. `DamageService` ctor calls `_eventBus.Subscribe<CarCollisionEvent>`) only runs that ctor when something resolves it. In this project the trigger is a MonoBehaviour registered via `RegisterComponentInHierarchy<T>()` that lists the service in its `[Inject] Construct(...)` — VContainer auto-injects those at container build, which forces creation. Same for `VfxFactory` (created via `CarDamageVisualController` → `IVfxService` → `IVfxFactory`).

**Why:** Reviewing step 3, this looked like "DamageService never subscribes" but it does — the chain `CarController.Construct(IDamageService)` activates it at build. Don't flag it as a hard "never created" bug.

**How to apply:** When a pure-C# singleton's ctor subscribes to EventBus, verify a `RegisterComponentInHierarchy` consumer pulls it before flagging. Flag instead as a *robustness* note: if that consumer is ever absent (menu scene, removed registration), subscription/prewarm silently never happens — recommend `RegisterEntryPoint`/`IStartable` or `RegisterBuildCallback` eager-resolve for anything that MUST run regardless of consumers.
