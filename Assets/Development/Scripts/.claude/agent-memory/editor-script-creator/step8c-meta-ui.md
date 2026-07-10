---
name: step8c-meta-ui
description: Editor pipeline that builds meta-screen UI (MainMenu/Garage/Stunts_Hub/MatchEnd) against a runtime View contract written in parallel by another agent
metadata:
  type: project
---

Step 8c (наполнение UI мета-экранов) built 8 Editor scripts against a **contract**
(field names + types) for runtime Views/StuntsCatalogConfig/GameLifetimeScope fields
that did not exist yet at write time (a parallel runtime agent wrote them). Key lesson:
when working off a contract, only rely on the exact surface the contract promises
(serialized field names) — do NOT assume convenience methods/properties exist (e.g.
`StuntsCatalogConfig.TryGetEntry(StuntType, out entry)` or `entry.Title` were NOT
promised, only `_entries` with `_type/_title/_sceneKey/_enabled/_preview` sub-fields).
Read config data via `SerializedObject`/`FindPropertyRelative` instead of assuming a
runtime API shape — this compiles regardless of what public methods the other agent
eventually adds.

Files (all under `Assets/Development/Scripts/Editor/`):
- `UI/UIBuildHelpers.cs` — reusable idempotent helpers (FindOrCreateChild, Anchor/Stretch,
  LoadSprite w/ auto Texture-Type-to-Sprite import fix, CreateLabel/CreateButton/CreatePanel,
  EnsureCanvasAndEventSystem for scenes without a pre-built Canvas). No [MenuItem] — utility
  class, same pattern as existing `Editor/Tools/CarPrefabDiscovery.cs`.
- `Setup/BindMetaUIRefsEditor.cs` — single place that fills GameLifetimeScope's
  `_mainMenuView/_garageView/_stuntsHubView/_matchEndView/_stuntsCatalogConfig`. Each
  Build*UIEditor calls its Bind* method right after creating its View (mirrors
  `SetupMetaConfigsEditor` pattern — null-safe, never overwrites with null).
- `Tools/GenerateTmpFontAssetsEditor.cs` — `TMP_FontAsset.CreateFontAsset(font)` bakes a
  usable atlas **synchronously** (default ASCII set) without needing the TMP Font Asset
  Creator window; then `fontAsset.TryAddCharacters(cyrillicSet, out _)` extends it. Sub-assets
  (`fontAsset.material`, each `fontAsset.atlasTextures[i]`) must be added via
  `AssetDatabase.AddObjectToAsset` AFTER `AssetDatabase.CreateAsset` or the atlas won't persist.
- `Tools/GenerateCarIconsEditor.cs` — `AssetPreview.GetAssetPreview` is async; poll it in a
  bounded loop (`Thread.Sleep(50)` × ~100 iters) since there's no editor-script-friendly
  callback. Preview textures ARE readable — `EncodeToPNG()` works directly.
- `Configs/CreateDefaultStuntsCatalogConfigEditor.cs` — idempotent by `_type` enum index
  (mirrors `CreateDefaultCarCatalogConfigEditor`'s idempotent-by-`_id` pattern).
- `UI/BuildMainMenuUIEditor.cs`, `BuildGarageUIEditor.cs`, `BuildStuntsHubUIEditor.cs`,
  `BuildMatchEndUIEditor.cs` — one script per meta scene, each independently runnable via
  its own menu item (operates on the currently *active* scene, doesn't open scenes itself).
  `BuildMatchEndUIEditor` is the odd one out — Derby_Arena_01 has no pre-built UICanvas
  (unlike the three menu scenes assembled by `BuildMetaScenesEditor`), so it calls
  `UIBuildHelpers.EnsureCanvasAndEventSystem()` itself.
- `Wizards/BuildMetaUIEditor.cs` — aggregator; menu path is explicitly under `FlatOutLike/UI/...`
  per this project's request even though it's structurally a Wizard (skill's category-by-menu-path
  vs. category-by-folder distinction — folder placement follows the skill's function-based rule,
  menu path followed the explicit spec). Opens/saves/restores scenes via try/finally, same
  pattern as `BuildMetaScenesEditor`.

Full menu list added:
- `FlatOutLike/Tools/Generate TMP Font Assets`
- `FlatOutLike/Tools/Generate Car Icons`
- `FlatOutLike/Configs/Create Default StuntsCatalogConfig`
- `FlatOutLike/UI/Build MainMenu UI` / `Build Garage UI` / `Build StuntsHub UI` / `Build MatchEnd UI`
- `FlatOutLike/UI/Build Meta UI (Step 8c)` (aggregator)
- `FlatOutLike/Setup/Bind Meta UI Refs (Step 8c)` (manual re-bind, not in original spec but added
  for symmetry with `SetupMetaConfigsEditor`)

Not yet verified in the Unity Editor (runtime View classes didn't exist yet when these
were written) — first compile after the runtime agent lands its Views/GameLifetimeScope
fields/StuntsCatalogConfig should be checked for field-name mismatches (warnings will show
up as `Debug.LogWarning` "поле не найдено" if any SerializeField name differs from the
contract used here).
