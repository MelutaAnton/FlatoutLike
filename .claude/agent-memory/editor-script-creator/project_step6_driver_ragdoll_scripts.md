---
name: project_step6_driver_ragdoll_scripts
description: Step 6 (Eject + ragdoll driver + orbit camera) — 5 editor scripts written against a locked runtime-type contract before the parallel runtime code existed
metadata:
  type: project
---

Step 6 "Eject + ragdoll-водитель" (Derby) — orchestrator ran editor-script-creator IN PARALLEL
with a runtime-code agent implementing the actual C# classes, per a locked naming contract.
Compilation was expected to be red until the runtime agent finished — this is a valid workflow
pattern in this project: write Editor automation against a contract, not against code you can
currently compile.

## Files created
- `Assets/Development/Scripts/Editor/Configs/CreateDefaultDriverEjectConfigEditor.cs` — menu
  `FlatOutLike/Configs/Create Default DriverEjectConfig`, asset at
  `Assets/Development/Data/Configs/Driver/DriverEjectConfig_Default.asset`
  (`_forwardImpulse`=8, `_upImpulse`=6).
- `Assets/Development/Scripts/Editor/Configs/CreateDefaultOrbitCameraConfigEditor.cs` — menu
  `FlatOutLike/Configs/Create Default OrbitCameraConfig`, asset at
  `Assets/Development/Data/Configs/Camera/OrbitCameraConfig_Default.asset`
  (`_distance`=6, `_height`=2.5, `_orbitSpeed`=30, `_followDamping`=0.2).
- `Assets/Development/Scripts/Editor/Setup/BuildDriverRagdollPrefabEditor.cs` — menu
  `FlatOutLike/Setup/Build Driver Ragdoll Prefab`. Moves `SitDriver.anim` from
  `Assets/Content/Animations/` → `Assets/Development/Art/Animations/`, builds
  `AC_DriverSit.controller` (single default state "Sit"), builds
  `Assets/Development/Prefabs/Characters/Driver_Default.prefab` on top of a
  ThirdParty humanoid avatar (default source
  `Assets/ThirdParty/DriverAvatars/npc_casual_set_00/Prefabs/npc_csl_00_character_01m_01.prefab`,
  which DOES have Animator+humanoid Avatar already assigned). Ragdoll built PROGRAMMATICALLY
  (no Ragdoll Wizard, per CLAUDE.md Mixamo caveat) via `Animator.GetBoneTransform(HumanBodyBones...)`
  with a name-substring fallback (`NameFallbacks` dict) if `animator.isHuman` is false or a bone
  is missing. 11 bones (Hips/Spine/Head/Upper+LowerArm L+R/Upper+LowerLeg L+R), masses sum to 70kg,
  Box collider on Hips/Spine, Sphere on Head, Capsule on limbs (sized/oriented via
  `InverseTransformPoint` to the child bone + dominant-axis heuristic — approximate, meant to be
  hand-tuned afterward). `CharacterJoint.connectedBody` wired parent→child per a `BoneSetup[]` table.
- `Assets/Development/Scripts/Editor/Setup/AttachDriverToCarPrefabsEditor.cs` — menu
  `FlatOutLike/Setup/Attach Driver To Car Prefabs`. Uses `LoadPrefabContents`/`SaveAsPrefabAsset`
  on `Car_Sport_09.prefab` and `Car_Enemy.prefab` (same pattern as `BuildEnemyCarPrefabEditor`).
  Creates/reuses child `"DriverSeat"` (local pos const `(0, 0.6, 0.1)` — placeholder, needs manual
  tuning per car model), instantiates `Driver_Default.prefab` under it, fills `CarController._driver`.
- `Assets/Development/Scripts/Editor/Setup/SetupCameraRigEditor.cs` — menu
  `FlatOutLike/Setup/Setup Camera Rig (Step 6)`. Finds the GameObject holding
  `ChaseCarCameraBehaviour` (idempotent — checks component, not name), adds
  `OrbitDriverCameraBehaviour` + `CameraRig` alongside it, wires
  `CameraRig._chase/_orbit`, `OrbitDriverCameraBehaviour._config`, and
  `GameLifetimeScope._cameraRig/_driverEjectConfig/_orbitCameraConfig`.

## Recurring gotcha in this workflow
When a script references a `[SerializeField]` that a parallel runtime agent hasn't landed yet,
`SerializedObject.FindProperty("_x")` returns `null` (does not throw) — always null-check before
using the property, and `Debug.LogWarning` with "возможно рантайм ещё не скомпилировался" so the
user knows to re-run after the runtime PR lands. This pattern is now used across
`AttachDriverToCarPrefabsEditor` and `SetupCameraRigEditor`.

## Run order (documented to user)
Create Default configs (DriverEjectConfig + OrbitCameraConfig) → Build Driver Ragdoll Prefab →
Attach Driver To Car Prefabs → Setup Camera Rig (Step 6) in Derby_Arena_01.
