---
name: project-vfx-naming-divergence
description: VFX subsystem is spelled Vfx* in code but VFX* in CLAUDE.md/skills — check both spellings, potential rename hot spot
metadata:
  type: project
---

As of step 3, the VFX subsystem is implemented with PascalCase-`Vfx` names: `VfxEffectType`, `VfxConfig`, `IVfxService`/`VfxService`, `IVfxFactory`/`VfxFactory`, `ParticleController`. But `CLAUDE.md` ("VFX: VFXEffectType enum + VFXConfig") and the `add-vfx-effect` skill instruct editing `VFXEffectType`, `VFXConfig`, `IVFXService.PlayEffect(...)` — all-caps `VFX`.

**Why:** This is a real divergence: a future skill-driven edit would target symbols that don't exist. Flagged in the step-3 review.

**How to apply:** When reviewing VFX-related changes, grep for BOTH `Vfx` and `VFX`. If this gets reconciled (one side renamed), update or delete this memory — verify current spelling with grep before relying on it.
