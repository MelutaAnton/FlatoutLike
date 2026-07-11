---
name: step10-pause-killfeed
description: Editor scripts for Step 10 Polish — Pause overlay (own Canvas, sortingOrder above mobile input) + Derby HUD kill-feed/Wrecked panel migration
metadata:
  type: project
---

Step 10 (Polish) added Pause overlay + Derby HUD kill-feed/Wrecked panel, built against a
contract for runtime types not yet compiled (same pattern as [[step8c-meta-ui]]).

Files:
- `Editor/UI/BuildPauseOverlayEditor.cs` (new) — menu `FlatOutLike/UI/Build Pause Overlay`,
  runs on the CURRENT gameplay scene (Derby_Arena_01, Stunts_HighJump). Creates its OWN
  Canvas "PauseOverlayCanvas" (NOT via `UIBuildHelpers.EnsureCanvasAndEventSystem` — that
  helper returns/reuses ANY existing Canvas, which here would grab the HUD canvas at
  sortingOrder=0). Mirrors `BuildMobileInputCanvasEditor`'s own-Canvas-GameObject pattern:
  `sortingOrder=60`, explicitly higher than `BuildMobileInputCanvasEditor`'s
  `MobileInputCanvas` (sortingOrder=50) so pause catches taps over mobile controls too.
  Ensures an EventSystem exists (small local helper) since this script may run before any
  other UI script has created one. Idempotent by `PauseOverlayView` component
  (`FindAnyObjectByType<T>(FindObjectsInactive.Include)` — panel starts SetActive(false), a
  plain `FindAnyObjectByType<T>()` with default args skips inactive objects and would false-
  negative on rebuild once the panel itself, not just its parent Canvas, is inactive).
- `Editor/UI/BuildDerbyHudEditor.cs` (extended) — added `KillFeedContainer` (top-left,
  VerticalLayoutGroup, 4x `KillFeedItem_N` slots each with a translucent black `Image`
  backdrop + `KillFeedItemView`+TMP label, RussoOne ~28px white — flat white text over
  gameplay is unreadable without a backdrop) and `WreckedPanel` (center, MatchEnd-style
  dim+card+title+2 buttons WATCH/LEAVE). Both start `SetActive(false)`. This is a
  **migration** of an already-shipped HUD (Step 8d) — `FindOrCreateChild` on the existing
  `DerbyHud` root makes re-running dobuild the new pieces without touching/duplicating the
  existing HpBar/NitroBar/AliveCounter.
- `Editor/Setup/BindMetaUIRefsEditor.cs` (extended) — added `BindPauseOverlayView` +
  `_pauseOverlayView` field, wired into the `BindAllInActiveScene` aggregator using the same
  `FindObjectsInactive.Include` idempotency note above.

Sprite reuse from `Racing Game Ui Pack` (via `UIBuildHelpers.LoadSprite`, auto-copied to
`Development/Art/UI/UiPack/`): card/panel background = `Buttons/png/button2.png` (same as
Garage/StuntsHub card bg), primary action button (RESUME/WATCH) = `Buttons/png/button1.png`
(same as MatchEnd's CONTINUE), secondary/leave action = `Buttons/png/back button.png` (same
semantic as Garage/StuntsHub's back button — reused here for LEAVE).

Not yet verified in the Unity Editor — runtime types (`PauseOverlayView`,
`KillFeedItemView`, `DerbyHudView` new fields, `GameLifetimeScope._pauseOverlayView`) were
being written in parallel by another agent at time of writing. First compile/run should be
checked for `Debug.LogWarning "поле не найдено"` messages indicating a field-name mismatch
against this contract.
