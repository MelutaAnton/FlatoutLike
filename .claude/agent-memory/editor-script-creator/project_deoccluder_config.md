---
name: project_deoccluder_config
description: DeoccluderConfig SO wiring — new CreateDefaultDeoccluderConfigEditor + _deoccluderConfig field added to CinemachineChaseCameraBehaviour/CinemachineOrbitDriverCameraBehaviour in SetupCinemachineCameraRigEditor
type: project
---

Follow-up to [[project_cinemachine_impulse_shake]]: `EnsureDeoccluder()` in
`SetupCinemachineCameraRigEditor.cs` used to hardcode CinemachineDeoccluder tuning values
directly on the component. A parallel runtime task moved that tuning into a data-driven SO —
`DeoccluderConfig` (`FlatOutLike.Data.Configs.Camera`, fields `_collideAgainst`,
`_transparentLayers`, `_minimumDistanceFromTarget`, `_cameraRadius`, `_damping`,
`_dampingWhenOccluded`, `_smoothingTime`) — consumed at runtime by
`CinemachineChaseCameraBehaviour` / `CinemachineOrbitDriverCameraBehaviour` via a new
`_deoccluderConfig` field.

Editor side (this agent, 2026-07-14):
- New `Assets/Development/Scripts/Editor/Configs/CreateDefaultDeoccluderConfigEditor.cs` —
  menu `FlatOutLike/Configs/Create Default DeoccluderConfig`. Same LayerMask-collection pattern
  as `EnsureDeoccluder()`/`CreateDefaultAISensorConfigEditor` (only add `Layers.Wall`/`Hazard`/
  `Vehicle`/`Pickup` if `LayerMask.NameToLayer(...) >= 0`, else warn to run
  `FlatOutLike/Setup/Create Physics Layers`). Idempotent by **early-exit**, not upsert (unlike
  AISensorConfig's SyncFields pattern) — if the asset already exists, log and return, so manual
  designer tuning on the asset survives re-runs.
- `SetupCinemachineCameraRigEditor.cs`: added `DeoccluderConfigPath` const + private helper
  `FillDeoccluderConfigRef(SerializedObject so, string behaviourTypeName)`, called from both
  `EnsureChaseCamera`/`EnsureOrbitCamera` right before `so.ApplyModifiedProperties()`. Loads the
  asset, null-safe (missing asset or missing field → warning, existing field value not
  overwritten — never assign null, per editor-files.md rule).
- `EnsureDeoccluder()` itself was intentionally left untouched per task spec — its baked values
  remain the fallback for scenes where the menu hasn't been re-run; the new runtime
  `_deoccluderConfig` field is expected to override them once the runtime side reads it (not
  this agent's concern — that's the parallel code-writer task).

Run order: `FlatOutLike/Configs/Create Default DeoccluderConfig` (once, after Physics Layers) →
`FlatOutLike/Setup/Setup Cinemachine Camera Rig`.
