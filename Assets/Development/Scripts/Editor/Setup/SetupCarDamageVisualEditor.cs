#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using FlatOutLike.Controllers.Car;
using FlatOutLike.Data.Configs.Car;

namespace FlatOutLike.Editor.Setup
{
    /// <summary>
    /// Adds CarDamageVisualController to the car object in the open _TestDriving scene
    /// and fills all its SerializedField references:
    ///   _car            → CarController on the same root
    ///   _statsConfig    → CarStatsConfig_Default.asset
    ///   _visualConfig   → CarDamageVisualConfig.asset
    ///   _bodyRenderer   → first Renderer found in children
    ///   _bodyMeshFilter → first MeshFilter found in children (optional)
    ///   _skinnedRenderer→ first SkinnedMeshRenderer found in children (optional)
    ///   _detachableParts→ all DetachablePart components found in children
    ///
    /// Idempotent: re-running refreshes references without duplicating the component.
    ///
    /// Run AFTER:
    ///   FlatOutLike/Setup/Build Driving Test Scene
    ///   FlatOutLike/Configs/Create Default CarStatsConfig
    ///   FlatOutLike/Configs/Create Default CarDamageVisualConfig
    /// </summary>
    public static class SetupCarDamageVisualEditor
    {
        private const string ScenePath       = "Assets/Development/Scenes/_TestDriving.unity";
        private const string StatsConfigPath = "Assets/Development/Data/Configs/Car/CarStatsConfig_Default.asset";
        private const string VisualConfigPath= "Assets/Development/Data/Configs/Car/CarDamageVisualConfig.asset";

        [MenuItem("FlatOutLike/Setup/Setup Car Damage Visual")]
        public static void SetupCarDamageVisual()
        {
            Undo.SetCurrentGroupName("Setup Car Damage Visual");
            int undoGroup = Undo.GetCurrentGroup();

            // ── 1. Ensure _TestDriving is open ────────────────────────────────
            OpenSceneIfNeeded();

            // ── 2. Find car by component (robust vs. name search) ─────────────
            var carController = Object.FindAnyObjectByType<CarController>();
            if (carController == null)
            {
                Debug.LogWarning("[Editor] No CarController found in scene. " +
                                 "Run 'FlatOutLike/Setup/Build Driving Test Scene' first.");
                return;
            }

            var carGo = carController.gameObject;
            Debug.Log("[Editor] Found car: " + carGo.name);

            // ── 3. Add CarDamageVisualController if not present ───────────────
            var damageVisual = carGo.GetComponent<CarDamageVisualController>();
            if (damageVisual == null)
            {
                damageVisual = Undo.AddComponent<CarDamageVisualController>(carGo);
                Debug.Log("[Editor] Added CarDamageVisualController to " + carGo.name + ".");
            }
            else
            {
                Debug.Log("[Editor] CarDamageVisualController already present on " +
                          carGo.name + ", refreshing refs.");
            }

            // ── 4. Load configs ───────────────────────────────────────────────
            var statsConfig = AssetDatabase.LoadAssetAtPath<CarStatsConfig>(StatsConfigPath);
            if (statsConfig == null)
                Debug.LogWarning("[Editor] CarStatsConfig_Default not found at " + StatsConfigPath +
                                 ". Run 'FlatOutLike/Configs/Create Default CarStatsConfig' first. " +
                                 "_statsConfig will be null.");

            var visualConfig = AssetDatabase.LoadAssetAtPath<CarDamageVisualConfig>(VisualConfigPath);
            if (visualConfig == null)
                Debug.LogWarning("[Editor] CarDamageVisualConfig not found at " + VisualConfigPath +
                                 ". Run 'FlatOutLike/Configs/Create Default CarDamageVisualConfig' first. " +
                                 "_visualConfig will be null.");

            // ── 5. Locate body renderer and related components ────────────────
            // GetComponentInChildren searches depth-first; the first hit is the car body mesh.
            // WheelColliders do not have MeshRenderers by default, so this is safe.
            var bodyRenderer    = carGo.GetComponentInChildren<Renderer>();
            var bodyMeshFilter  = carGo.GetComponentInChildren<MeshFilter>();
            var skinnedRenderer = carGo.GetComponentInChildren<SkinnedMeshRenderer>();
            var detachableParts = carGo.GetComponentsInChildren<DetachablePart>();

            // ── 6. Fill SerializedFields via SerializedObject ─────────────────
            var so = new SerializedObject(damageVisual);

            so.FindProperty("_car").objectReferenceValue          = carController;
            so.FindProperty("_statsConfig").objectReferenceValue  = statsConfig;
            so.FindProperty("_visualConfig").objectReferenceValue = visualConfig;
            so.FindProperty("_bodyRenderer").objectReferenceValue = bodyRenderer;

            // Optional: Strategy B (mesh swap)
            if (bodyMeshFilter != null)
                so.FindProperty("_bodyMeshFilter").objectReferenceValue = bodyMeshFilter;

            // Optional: Strategy C (blend shapes)
            if (skinnedRenderer != null)
                so.FindProperty("_skinnedRenderer").objectReferenceValue = skinnedRenderer;

            // Detachable parts (empty array is fine if no DetachablePart components exist yet)
            var partsProp = so.FindProperty("_detachableParts");
            partsProp.arraySize = detachableParts.Length;
            for (int i = 0; i < detachableParts.Length; i++)
                partsProp.GetArrayElementAtIndex(i).objectReferenceValue = detachableParts[i];

            so.ApplyModifiedProperties();

            Debug.Log("[Editor] CarDamageVisualController refs filled on " + carGo.name +
                      " — statsConfig=" + (statsConfig  != null ? statsConfig.name  : "NULL") +
                      ", visualConfig=" + (visualConfig != null ? visualConfig.name : "NULL") +
                      ", bodyRenderer=" + (bodyRenderer != null ? bodyRenderer.name : "NULL") +
                      ", detachableParts=" + detachableParts.Length + ".");

            // ── 7. Mark scene dirty ───────────────────────────────────────────
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

            Undo.CollapseUndoOperations(undoGroup);

            Debug.Log("[Editor] Car damage visual setup complete.");
        }

        // ─────────────────────────────────────────────────────────────────────
        private static void OpenSceneIfNeeded()
        {
            if (SceneManager.GetActiveScene().path == ScenePath) return;

            if (!System.IO.File.Exists(
                    System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), ScenePath)))
            {
                Debug.LogWarning("[Editor] _TestDriving.unity not found at " + ScenePath +
                                 ". Run 'FlatOutLike/Setup/Build Driving Test Scene' first.");
                return;
            }

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Debug.Log("[Editor] Opened _TestDriving.unity.");
        }
    }
}
#endif
