---
name: project-vcontainer-inject-order
description: VContainer InjectGameObject injects components in GetComponents (Inspector) order — relevant to multi-controller-per-car Construct ordering races
metadata:
  type: reference
---

VContainer `IObjectResolver.InjectGameObject(go)` (in `ObjectResolverUnityExtensions.cs`)
injects every MonoBehaviour returned by `current.GetComponents<MonoBehaviour>(buffer)`,
in that order, then recurses into children (parent components before child components).

`GetComponents` returns components in **Inspector component order**.

**Why it matters here:** the car prefab carries multiple [Inject] MonoBehaviours on one root
(`CarController`, `EnemyCarController`, `CarDamageVisualController`). When two of them write
the same field during `Construct` (e.g. `EnemyCarController.Construct` calls
`_car.SetInputSource(aiProvider)` while `CarController.Construct` assigns `_input = keyboard`),
correctness depends on which `Construct` runs last — i.e. on Inspector component order, which is
not enforced in code. Flag any such cross-Construct write as a fragile-ordering risk and prefer
order-independent guards (e.g. `if (_input == null)`) or deferring the write to `Start()`
(Start runs after all build-callback injection).

**How to apply:** when reviewing a prefab with 2+ injected MonoBehaviours that touch shared
state, do not accept "it works because component X is above Y in the Inspector" as safe.
