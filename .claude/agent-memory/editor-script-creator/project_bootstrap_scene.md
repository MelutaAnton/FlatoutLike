---
name: project-bootstrap-scene
description: First editor script created — BuildBootstrapSceneEditor for Boot.unity scene setup
metadata:
  type: project
---

First Editor-script in the project: `BuildBootstrapSceneEditor`.

**Key facts:**
- MenuRoot actually used in scripts is `FlatOutLike` (not `Flatuot2_Like` from system prompt — CLAUDE.md and code-writer use `FlatOutLike` namespace throughout)
- `GameLifetimeScope` is in namespace `FlatOutLike.Installers`, derives from `VContainer.Unity.LifetimeScope`, no [SerializeField] fields — all registered via `builder.Register*`
- `GameBootstrap` is in root namespace `FlatOutLike`, is a MonoBehaviour registered via `builder.RegisterComponentInHierarchy<GameBootstrap>()`
- Scenes live in `Assets/Development/Scenes/` — folder existed (`.gitkeep`) but Boot.unity did not exist at session start
- Editor scripts folder `Assets/Development/Scripts/Editor/Setup/` was created fresh for this script

**Why:** Step 1 "Frame" of the project — creates the boot scene with just the two root objects needed for VContainer DI to boot.

**How to apply:** For future Editor scripts in this project, confirm MenuRoot = `FlatOutLike` (not `Flatuot2_Like`). No SerializeField filling needed on GameLifetimeScope until later steps add scene-object dependencies.
