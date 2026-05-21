---
name: project-architecture-bootstrap
description: Core infrastructure established in step 1 — namespaces, services, DI registrations, and key patterns for FlatOutLike project
metadata:
  type: project
---

## Namespace Convention

Root namespace is `FlatOutLike.<Layer>`, NOT `Flatuot2_Like` (CLAUDE.md has a typo — the actual namespace used is `FlatOutLike`):
- Models: `FlatOutLike.Models`
- Events: `FlatOutLike.Core.Events`
- StateMachine: `FlatOutLike.Core.StateMachine`
- Input: `FlatOutLike.Core.Input`
- Services: `FlatOutLike.Services.<Domain>`
- Installers: `FlatOutLike.Installers`
- Bootstrap: `FlatOutLike`

**Why:** User explicitly specified `FlatOutLike.<Layer>` in the task — CLAUDE.md `RootNamespace` field says `Flatuot2_Like` but that's a legacy placeholder mismatch.

**How to apply:** Always use `FlatOutLike` namespace prefix when writing new code for this project.

## Editor Menu Prefix

`FlatOutLike/<Category>/<Action>` — NOT `TowerShooter/...` (CLAUDE.md line 216 is stale).

## Services Registered in GameLifetimeScope (step 1)

| Type | Registration | Interface |
|------|-------------|-----------|
| EventBus | `Register<EventBus>(Singleton).As<IEventBus>()` | IEventBus |
| KeyboardInputProvider | `Register<KeyboardInputProvider>(Singleton).As<IInputProvider>()` | IInputProvider |
| InputRouter | `Register<InputRouter>(Singleton)` (self — no interface) | — |
| PlayerPrefsProfileStorage | `Register<PlayerPrefsProfileStorage>(Singleton).As<IProfileStorage>()` | IProfileStorage |
| MetaProgressService | `Register<MetaProgressService>(Singleton).As<IMetaProgressService>()` | IMetaProgressService |
| GameFlowService | `Register<GameFlowService>(Singleton).As<IGameFlowService>()` | IGameFlowService |
| GameBootstrap | `RegisterComponentInHierarchy<GameBootstrap>()` | MonoBehaviour |

## Key Design Decisions

- **StateMachine<GameState>**: generic FSM with push/pop stack. Push does NOT call Exit on current. Pop does NOT call Enter on restored state.
- **GameFlowService** owns StateMachine — publishes `GameStateChangedEvent`, `GamePausedEvent`, `GameResumedEvent` on transitions.
- **InputRouter** is pure C# IDisposable — subscribes to GameStateChangedEvent, maps GameState → InputContext.
- **EmptyState** stub in GameFlowService covers all GameState values until real state classes are added in step 2+.
- **PlayerPrefs key** `"FlatOutLike.Profile.Coins"` — single source of truth in PlayerPrefsProfileStorage.

## GameBootstrap Update Loop

`Update()` calls `_router.Tick()` then `_flow.Tick()` — guards with `_initialized` flag because OnEnable fires before Construct().
