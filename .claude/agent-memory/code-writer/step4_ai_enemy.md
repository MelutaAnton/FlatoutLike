---
name: step4-ai-enemy
description: Step 4 AI enemy car — AiInputProvider, EnemyAIService (FSM), EnemyCarController, registration changes
metadata:
  type: project
---

## Step 4: AI Enemy Car (2026-06-26)

### New files created
- `Models/AiState.cs` — enum `{ Idle, Pursue, Ram, Recover }`
- `Core/Input/AiInputProvider.cs` — writable IInputProvider for AI (one per enemy, NOT a DI singleton)
- `Data/Configs/Enemies/EnemyPersonalityConfig.cs` — SO with Aggressor defaults (RamTriggerDistance=12, RamExitDistance=18, RecoverDuration=1.2, NitroOnRam=true, SteerSharpness=1.5)
- `Services/AI/IEnemyAIService.cs` — holds AiPerception (readonly struct), AiBrainState (mutable struct), AiDecision (readonly struct), IEnemyAIService interface
- `Services/AI/EnemyAIService.cs` — stateless pure C# FSM; all state in ref AiBrainState; uses System.Math for Clamp, Vector3.Dot/Cross for geometry
- `Controllers/Enemies/EnemyCarController.cs` — MonoBehaviour, reads physics, builds AiPerception, calls IEnemyAIService.Tick, pushes result to AiInputProvider

### Modified files
- `Controllers/Car/CarController.cs` — added `public float MaxArmor` getter and `public void SetInputSource(IInputProvider)` method
- `Installers/GameLifetimeScope.cs` — added `[SerializeField] GameObject[] _sceneCars`, removed two RegisterComponentInHierarchy calls (CarController, CarDamageVisualController), added RegisterBuildCallback(container.InjectGameObject for each scene car), added `builder.Register<EnemyAIService>(Lifetime.Singleton).As<IEnemyAIService>()`

### Key architectural decisions
- `EnemyPersonalityConfig` NOT registered in DI globally — it's a SerializeField on EnemyCarController (different enemies can have different profiles)
- `AiInputProvider` created with `new` inside `EnemyCarController.Construct` — not a DI type; one instance per enemy
- `EnemyCarController.Construct` calls `_car.SetInputSource(_aiInput)` to override KeyboardInputProvider that CarController.Construct received from DI — DEPENDS on CarController being first in Inspector component order on the enemy prefab
- No IEventBus injected into EnemyCarController (no events needed in step 4 — avoids dead dependency)
- `Rigidbody.linearVelocity` (Unity 6 API) used in EnemyCarController.FixedUpdate
- `_statsConfig` field on EnemyCarController is reserved for future use; self MaxArmor comes from `_car.MaxArmor`

### FSM transitions
- Idle → Pursue: when dist <= PursueStartDistance (60m — almost always on small arena)
- Pursue → Ram: dist <= 12m AND forwardDot > 0.3
- Ram → Pursue: dist > 18m
- Ram → Recover: selfSpeed < 1.5 m/s (stuck/wedged)
- Recover → Pursue: RecoverTimer <= 0 (after 1.2s)
- Any state: if SelfHpRatio < RetreatHpThreshold → force Recover (Aggressor has 0, never fires)

**Why:** Step 4 of derby game — basic ramming AI for enemy car using existing CarController physics.
**How to apply:** When adding new enemy archetypes, create new EnemyPersonalityConfig.asset instances with different values. For factory-based spawning (Step 5), EnemyCarController.SetTarget() is the API to wire up the target.
