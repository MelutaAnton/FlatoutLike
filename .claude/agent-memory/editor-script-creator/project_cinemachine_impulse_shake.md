---
name: project_cinemachine_impulse_shake
description: Camera shake via Cinemachine 3.1 Impulse — editor setup in SetupCinemachineCameraRigEditor.EnsureImpulse(), field/defaults gotchas
type: project
---

Camera shake system (parallel runtime work): `CinemachineCameraShaker : MonoBehaviour, ICameraShaker`
(`FlatOutLike.Controllers.Camera`, field `_impulseSource`), `CameraShakeConfig` (SO) +
`CreateDefaultCameraShakeConfigEditor` (asset at
`Assets/Development/Data/Configs/Camera/CameraShakeConfig_Default.asset`, menu
`FlatOutLike/Configs/Create Default CameraShakeConfig`). `GameLifetimeScope` already has
`_cameraShakeConfig` / `_cameraShaker` fields (lines ~96-97).

Editor wiring lives in `SetupCinemachineCameraRigEditor.cs`:
- `EnsureImpulse(chaseGo, orbitGo)` — public static, called as step 6 of `Setup()`. Adds
  `CinemachineImpulseListener` (package: `Unity.Cinemachine`, extends `CinemachineExtension`,
  addable via plain `Undo.AddComponent`) to both CM_ChaseCamera and CM_OrbitCamera. Adds
  `CinemachineImpulseSource` + `CinemachineCameraShaker` to Main Camera (co-located with
  `CinemachineBrain`), fills `_impulseSource` via SerializedObject.
- `EnsureImpulseListener(vcamGo)` — private helper.
- Both are reusable the same way `EnsureChaseCamera`/`EnsureOrbitCamera`/`EnsureCameraRig` are
  (see [[project_bootstrap_scene]]-style Ensure* convention in this file).

Field/default gotchas found in CM 3.1.4 package source
(`Library/PackageCache/com.unity.cinemachine@*/Runtime/Impulse/`):
- `CinemachineImpulseSource.ImpulseDefinition` is a plain **public field** (not `[SerializeField]`
  private) of type `CinemachineImpulseDefinition` — set sub-fields directly on the component
  instance (`impulseSource.ImpulseDefinition.ImpulseShape = ...`), no SerializedObject needed for
  it; still wrap in `Undo.RecordObject`/`EditorUtility.SetDirty` for editor-safety.
- `CinemachineImpulseSource.Reset()` (auto-invoked by `Undo.AddComponent`, same as Editor's "Add
  Component") sets `ImpulseShape=Bump, ImpulseDuration=0.2, ImpulseType=Uniform,
  DissipationDistance=100, DissipationRate=0.25, ImpulseChannel=1`. **Uniform is the default but
  wrong for a per-collision arena shake** — the runtime `CinemachineCameraShaker.Shake()` passes
  the actual world-space impact position specifically so shakes dissipate with distance; the
  editor script must explicitly override `ImpulseType = Dissipating` (chose
  `DissipationDistance = 40f` for the 60x60 derby arena, `ImpulseDuration = 0.25f`, keep
  `ImpulseShape = Bump`).
- `CinemachineImpulseListener.Reset()` sets `ChannelMask=1, Gain=1` — matches source's default
  `ImpulseChannel=1` out of the box, no override needed after `Undo.AddComponent`. Field
  initializers alone (`ChannelMask=0, Gain=0`) would silently mute everything — only relevant if
  someone constructs it without Reset (shouldn't happen via `Undo.AddComponent`).
- Amplitude/force is NOT config in these components — it comes from the `velocity` vector param
  of `GenerateImpulseAtPositionWithVelocity(pos, velocity)`, called from game code
  (`CinemachineCameraShaker.Shake(position, force)` does `Vector3.down * force`).

Scope-filling for `_cameraShaker`/`_cameraShakeConfig` was added to the *existing*
`SetupCameraRigEditor.cs` (Step 6 camera rig script) rather than a new file — that's already the
established place GameLifetimeScope camera fields get filled (`_cameraRig`,
`_driverEjectConfig`, `_orbitCameraConfig`).

Run order: `FlatOutLike/Configs/Create Default CameraShakeConfig` (once) →
`FlatOutLike/Setup/Setup Camera Rig (Step 6)` (calls `SetupCinemachineCameraRigEditor.Setup()`
internally, which now also runs `EnsureImpulse`).
