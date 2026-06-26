#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using FlatOutLike.Data.Configs.Combat;
using FlatOutLike.Data.Configs.Vfx;
using FlatOutLike.Installers;

namespace FlatOutLike.Editor.Setup
{
    /// <summary>
    /// Final pipeline step for Шаг 3 «Урон и визуал».
    /// Fills GameLifetimeScope._damageConfig and ._vfxConfig from the .asset files on disk.
    ///
    /// Run AFTER:
    ///   FlatOutLike/Configs/Create Default DamageConfig
    ///   FlatOutLike/Configs/Create Default VfxConfig
    ///   FlatOutLike/Setup/Build Driving Test Scene   (creates GameLifetimeScope in scene)
    /// </summary>
    public static class FillGameLifetimeScopeRefsEditor
    {
        private const string DamageConfigPath = "Assets/Development/Data/Configs/Combat/DamageConfig.asset";
        private const string VfxConfigPath    = "Assets/Development/Data/Configs/Vfx/VfxConfig.asset";

        [MenuItem("FlatOutLike/Setup/Fill GameLifetimeScope Refs (Step 3)")]
        public static void FillRefs()
        {
            Undo.SetCurrentGroupName("Fill GameLifetimeScope Refs (Step 3)");
            int undoGroup = Undo.GetCurrentGroup();

            // ── 1. Find GameLifetimeScope in the currently open scene ─────────
            var scope = Object.FindAnyObjectByType<GameLifetimeScope>();
            if (scope == null)
            {
                Debug.LogWarning("[Editor] GameLifetimeScope not found in scene. " +
                                 "Run 'FlatOutLike/Setup/Build Driving Test Scene' first.");
                return;
            }

            Debug.Log("[Editor] Found GameLifetimeScope on '" + scope.gameObject.name + "'.");

            // ── 2. Load config assets ─────────────────────────────────────────
            var damageConfig = AssetDatabase.LoadAssetAtPath<DamageConfig>(DamageConfigPath);
            if (damageConfig == null)
                Debug.LogWarning("[Editor] DamageConfig not found at " + DamageConfigPath +
                                 ". Run 'FlatOutLike/Configs/Create Default DamageConfig' first. " +
                                 "_damageConfig will remain null.");

            var vfxConfig = AssetDatabase.LoadAssetAtPath<VfxConfig>(VfxConfigPath);
            if (vfxConfig == null)
                Debug.LogWarning("[Editor] VfxConfig not found at " + VfxConfigPath +
                                 ". Run 'FlatOutLike/Configs/Create Default VfxConfig' first. " +
                                 "_vfxConfig will remain null.");

            // ── 3. Fill via SerializedObject ──────────────────────────────────
            var so = new SerializedObject(scope);
            so.FindProperty("_damageConfig").objectReferenceValue = damageConfig;
            so.FindProperty("_vfxConfig").objectReferenceValue    = vfxConfig;
            so.ApplyModifiedProperties();

            Debug.Log("[Editor] GameLifetimeScope._damageConfig = " +
                      (damageConfig != null ? damageConfig.name : "NULL") +
                      ", _vfxConfig = " +
                      (vfxConfig    != null ? vfxConfig.name    : "NULL") + ".");

            // ── 4. Mark scene dirty ───────────────────────────────────────────
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

            Undo.CollapseUndoOperations(undoGroup);

            Debug.Log("[Editor] GameLifetimeScope refs fill complete (Step 3).");
        }
    }
}
#endif
