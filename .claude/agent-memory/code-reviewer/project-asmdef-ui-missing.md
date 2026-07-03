---
name: project-asmdef-ui-missing
description: FlatOutLike/FlatOutLike.Editor asmdefs don't reference UnityEngine.UI; any first UI usage needs the ref added or the whole assembly fails to compile
metadata:
  type: project
---
As of Step 7, `Assets/Development/Scripts/FlatOutLike.asmdef` references only `["VContainer"]` and `FlatOutLike.Editor.asmdef` only `["FlatOutLike","VContainer"]`. Neither references `UnityEngine.UI` (com.unity.ugui is installed, v2.0.0). `Assets/Development/Scripts/Views/UI` is currently EMPTY — no runtime code uses `UnityEngine.UI` yet.

**Why:** UnityEngine.UI ships as its own asmdef in the ugui package; asmdef-to-asmdef references are NOT transitive/auto — a consuming asmdef must list `"UnityEngine.UI"` explicitly. `noEngineReferences:false` covers core UnityEngine modules but NOT ugui.

**How to apply:** When reviewing any new code that does `using UnityEngine.UI;` (Text/Canvas/Image/Button/CanvasScaler/GraphicRaycaster), verify the owning asmdef gained a `"UnityEngine.UI"` reference. If not → CS0234 "namespace 'UI' does not exist in 'UnityEngine'", which breaks the ENTIRE assembly (and, for FlatOutLike, all EditMode tests that reference it). Flag as blocker. First offender was `Controllers/Stunts/HighJumpDebugOverlayController.cs` + `Editor/Setup/BuildHighJumpSceneEditor.cs`.
