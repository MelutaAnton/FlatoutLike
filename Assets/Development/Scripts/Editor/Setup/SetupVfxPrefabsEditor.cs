#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using FlatOutLike.Controllers.Vfx;

namespace FlatOutLike.Editor.Setup
{
    /// <summary>
    /// Select one or more particle prefab assets in the Project window and run this
    /// menu item. For each prefab the script will:
    ///   1. Set ParticleSystem.main.stopAction = Callback (required for pool return).
    ///   2. Set ParticleSystem.main.playOnAwake = false (effects are pool-driven, activated
    ///      via SetActive before Launch — playOnAwake fires on the old pooled position).
    ///   3. Add ParticleController component if absent.
    ///   4. Wire ParticleController._particleSystem to the ParticleSystem on the root.
    /// Operation is idempotent — re-running on already-configured prefabs is safe.
    /// </summary>
    public static class SetupVfxPrefabsEditor
    {
        [MenuItem("FlatOutLike/Setup/Setup VFX Prefabs")]
        public static void SetupVfxPrefabs()
        {
            Undo.SetCurrentGroupName("Setup VFX Prefabs");
            int undoGroup = Undo.GetCurrentGroup();

            var selected = Selection.gameObjects;
            if (selected == null || selected.Length == 0)
            {
                EditorUtility.DisplayDialog("Setup VFX Prefabs",
                    "No objects selected.\n\n" +
                    "Select one or more particle prefab assets in the Project window, " +
                    "then run FlatOutLike/Setup/Setup VFX Prefabs.",
                    "OK");
                return;
            }

            int processed = 0;

            foreach (var go in selected)
            {
                string assetPath = AssetDatabase.GetAssetPath(go);

                // ── Filter: only prefab assets ────────────────────────────────
                if (string.IsNullOrEmpty(assetPath) ||
                    !assetPath.EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase))
                {
                    Debug.LogWarning("[Editor] Skipping '" + go.name + "' — not a prefab asset.");
                    continue;
                }

                if (go.GetComponent<ParticleSystem>() == null)
                {
                    Debug.LogWarning("[Editor] Skipping '" + go.name +
                                     "' — no ParticleSystem on prefab root.");
                    continue;
                }

                // ── Open prefab contents for editing ──────────────────────────
                var root = PrefabUtility.LoadPrefabContents(assetPath);
                bool changed = false;

                try
                {
                    var ps = root.GetComponent<ParticleSystem>();
                    if (ps == null)
                    {
                        Debug.LogWarning("[Editor] Skipping '" + go.name +
                                         "' — ParticleSystem not found after loading prefab contents.");
                        continue;
                    }

                    // ── 1. Configure MainModule ───────────────────────────────
                    var main = ps.main;

                    // stopAction = Callback: required so ParticleController.OnParticleSystemStopped()
                    // fires and the pool receives the return callback.
                    if (main.stopAction != ParticleSystemStopAction.Callback)
                    {
                        main.stopAction = ParticleSystemStopAction.Callback;
                        changed = true;
                        Debug.Log("[Editor] '" + go.name + "' — set stopAction = Callback.");
                    }
                    else
                    {
                        Debug.Log("[Editor] '" + go.name + "' — stopAction already Callback, skipping.");
                    }

                    // playOnAwake = false: effects are pool-driven and activated via SetActive(true)
                    // before Launch(). With playOnAwake enabled the system fires on the old pooled
                    // position one frame before repositioning and can trigger the stop-callback early.
                    if (main.playOnAwake)
                    {
                        main.playOnAwake = false;
                        changed = true;
                        Debug.Log("[Editor] '" + go.name + "' — set playOnAwake = false.");
                    }
                    else
                    {
                        Debug.Log("[Editor] '" + go.name + "' — playOnAwake already false, skipping.");
                    }

                    // ── 2. Add ParticleController if not present ──────────────
                    var pc = root.GetComponent<ParticleController>();
                    if (pc == null)
                    {
                        pc = root.AddComponent<ParticleController>();
                        changed = true;
                        Debug.Log("[Editor] '" + go.name + "' — added ParticleController.");
                    }
                    else
                    {
                        Debug.Log("[Editor] '" + go.name + "' — ParticleController already present.");
                    }

                    // ── 3. Wire _particleSystem ───────────────────────────────
                    var sopc = new SerializedObject(pc);
                    var psProp = sopc.FindProperty("_particleSystem");
                    if (psProp.objectReferenceValue == null)
                    {
                        psProp.objectReferenceValue = ps;
                        sopc.ApplyModifiedProperties();
                        changed = true;
                        Debug.Log("[Editor] '" + go.name + "' — wired ParticleController._particleSystem.");
                    }
                    else
                    {
                        Debug.Log("[Editor] '" + go.name + "' — ParticleController._particleSystem already wired.");
                    }

                    // ── Save if anything changed ──────────────────────────────
                    if (changed)
                    {
                        PrefabUtility.SaveAsPrefabAsset(root, assetPath);
                        processed++;
                        Debug.Log("[Editor] Saved prefab '" + assetPath + "'.");
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Undo.CollapseUndoOperations(undoGroup);

            Debug.Log("[Editor] VFX prefab setup complete. Prefabs updated: " + processed + ".");
        }
    }
}
#endif
