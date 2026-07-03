---
name: project-test-asmdef-vcontainer
description: EditMode test asmdef references only FlatOutLike; instantiating a service that implements VContainer.Unity.ITickable may need a VContainer ref (CS0012)
metadata:
  type: project
---
`Assets/Development/Tests/EditMode/FlatOutLike.Tests.EditMode.asmdef` references `["FlatOutLike","UnityEngine.TestRunner","UnityEditor.TestRunner"]` + nunit — NOT `VContainer`.

**Why:** Until Step 7 no test instantiated a class implementing `VContainer.Unity.ITickable`/`IStartable`. Step 7's `HighJumpSessionServiceTests` does `new HighJumpSessionService(...)` (class implements ITickable). Constructing a type whose implemented-interface lives in an unreferenced assembly can raise CS0012 ("ITickable is defined in an assembly that is not referenced"), which fails the WHOLE EditMode test assembly.

**How to apply:** When a test newly instantiates a service implementing a VContainer.Unity interface, verify the test asmdef references `VContainer` (or that compilation passes). Trivial fix: add `"VContainer"` to the test asmdef references. Watch item — behavior depends on Roslyn interface-binding; confirm by actually compiling in Unity.
