#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using FlatOutLike.Controllers.Car;
using FlatOutLike.Data.Configs.Combat;
using FlatOutLike.Data.Configs.Vfx;
using FlatOutLike.Installers;

namespace FlatOutLike.Editor.Setup
{
    /// <summary>
    /// Fills GameLifetimeScope serialized references from assets and the active scene.
    ///
    /// Step 3 fields: _damageConfig, _vfxConfig
    /// Step 4 fields: _sceneCars (all CarController root GameObjects in the scene)
    ///
    /// All fields are filled in a single pass; the method is idempotent — re-running
    /// overwrites refs with the current scene state without duplicating anything.
    ///
    /// Run AFTER:
    ///   FlatOutLike/Configs/Create Default DamageConfig
    ///   FlatOutLike/Configs/Create Default VfxConfig
    ///   FlatOutLike/Setup/Build Driving Test Scene   (creates GameLifetimeScope)
    ///   FlatOutLike/Setup/Setup Enemy Car (Step 4)   (optional; adds enemy to _sceneCars)
    /// </summary>
    public static class FillGameLifetimeScopeRefsEditor
    {
        private const string DamageConfigPath = "Assets/Development/Data/Configs/Combat/DamageConfig.asset";
        private const string VfxConfigPath    = "Assets/Development/Data/Configs/Vfx/VfxConfig.asset";

        [MenuItem("FlatOutLike/Setup/Fill GameLifetimeScope Refs (Step 3)")]
        public static void FillRefs()
        {
            Undo.SetCurrentGroupName("Fill GameLifetimeScope Refs");
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

            // ── 2. Load Step 3 config assets ──────────────────────────────────
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

            var so = new SerializedObject(scope);

            // ── 3. Fill Step 3 config refs ────────────────────────────────────
            so.FindProperty("_damageConfig").objectReferenceValue = damageConfig;
            so.FindProperty("_vfxConfig").objectReferenceValue    = vfxConfig;

            Debug.Log("[Editor] GameLifetimeScope._damageConfig = " +
                      (damageConfig != null ? damageConfig.name : "NULL") +
                      ", _vfxConfig = " +
                      (vfxConfig    != null ? vfxConfig.name    : "NULL") + ".");

            // ── 4. Fill _sceneCars with all CarController roots in scene ──────
            // transform.root ensures we always store the topmost prefab root, not a
            // child GO — GameLifetimeScope.Configure() calls InjectGameObject(go)
            // which injects the entire hierarchy.
            var allCars  = Object.FindObjectsByType<CarController>(FindObjectsSortMode.None);
            var carsProp = so.FindProperty("_sceneCars");
            carsProp.arraySize = allCars.Length;
            for (int i = 0; i < allCars.Length; i++)
                carsProp.GetArrayElementAtIndex(i).objectReferenceValue =
                    allCars[i].transform.root.gameObject;

            Debug.Log("[Editor] GameLifetimeScope._sceneCars filled with " + allCars.Length + " car(s):");
            for (int i = 0; i < allCars.Length; i++)
                Debug.Log("[Editor]   [" + i + "] " + allCars[i].transform.root.gameObject.name);

            // ── Apply all changes at once ─────────────────────────────────────
            so.ApplyModifiedProperties();

            // ── 5. Mark scene dirty ───────────────────────────────────────────
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

            Undo.CollapseUndoOperations(undoGroup);

            Debug.Log("[Editor] GameLifetimeScope refs fill complete.");
        }

        /// <summary>
        /// Convenience alias for the Step 4 workflow entry point.
        /// Fills _damageConfig, _vfxConfig AND _sceneCars — identical to Step 3 item.
        /// Run this after 'Setup Enemy Car (Step 4)' so both player and enemy are registered.
        /// </summary>
        [MenuItem("FlatOutLike/Setup/Fill GameLifetimeScope Refs (Step 4)")]
        public static void FillRefsStep4() => FillRefs();
    }
}
#endif
