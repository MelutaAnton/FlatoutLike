---
name: project-vcontainer-optional-params
description: VContainer 1.18.0 ignores C# default parameter values in [Inject] Construct — "optional DI" throws if type unregistered
metadata:
  type: project
---

VContainer 1.18.0 (jp.hadashikick.vcontainer) does NOT honor C# default parameter values (`Foo foo = null`) during method/constructor injection.

**Why:** `ReflectionInjector.InjectMethods` calls `resolver.ResolveOrParameter(...)` for every parameter, which falls through to `resolver.Resolve(type)` — that THROWS `VContainerException` when the type is not registered. There is no `HasDefaultValue`/`IsOptional` check anywhere in the package (grep returns nothing). The CodeGen path uses the same `Resolve`.

**How to apply:** Flag any `[Inject] Construct(..., SomeType x = null)` written to make a dependency "optional in test scenes". It will hard-throw when SomeType isn't registered, regardless of the default. Correct patterns instead: always register the type (register a fallback default instance when the serialized ref is null), inject a wrapper/registry that can return null, or split into a separate optional-resolve call. Seen in Step 6 CarController.Construct(DriverEjectConfig ejectConfig = null) — comment claimed it was tolerated; it is not. Related: [[project-di-lazy-singletons]].
