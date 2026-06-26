#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FlatOutLike.Editor.Setup
{
    /// <summary>
    /// Adds a static obstacle wall to _TestDriving for collision damage testing.
    /// The wall is placed at (0, 1.5, 20) — directly ahead of the car spawn point (0, 1, 0)
    /// on the +Z acceleration path. It is a BoxCollider without Rigidbody (infinite mass),
    /// which triggers the WallMassRatio branch in DamageService.
    ///
    /// Idempotent: checks for "CrashTestWall" by name before creating.
    /// </summary>
    public static class AddCrashWallToTestDrivingEditor
    {
        private const string ScenePath = "Assets/Development/Scenes/_TestDriving.unity";
        private const string WallName  = "CrashTestWall";

        [MenuItem("FlatOutLike/Setup/Add Crash Wall to Test Driving")]
        public static void AddCrashWall()
        {
            Undo.SetCurrentGroupName("Add Crash Wall to Test Driving");
            int undoGroup = Undo.GetCurrentGroup();

            // ── 1. Ensure _TestDriving is open ────────────────────────────────
            OpenSceneIfNeeded();

            // ── 2. Idempotency: check by unique wall name ─────────────────────
            var existing = GameObject.Find(WallName);
            if (existing != null)
            {
                Debug.Log("[Editor] CrashTestWall already exists in scene at " +
                          existing.transform.position + ", skipping.");
                return;
            }

            // ── 3. Create wall as a Cube primitive (comes with BoxCollider) ────
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Undo.RegisterCreatedObjectUndo(wall, "Create CrashTestWall");
            wall.name = WallName;

            // Position: 20 units ahead of car spawn point; lifted to Y=1.5 so the
            // box center aligns with the car body (car height ~1.4 units).
            // Scale: 10 wide × 3 tall × 0.5 deep — wide enough that the car cannot steer around it.
            wall.transform.position   = new Vector3(0f, 1.5f, 20f);
            wall.transform.localScale = new Vector3(10f, 3f, 0.5f);

            // ── 4. Confirm no Rigidbody (static = infinite mass for WallMassRatio) ──
            // CreatePrimitive never adds Rigidbody, but guard defensively.
            var rb = wall.GetComponent<Rigidbody>();
            if (rb != null)
            {
                Object.DestroyImmediate(rb);
                Debug.Log("[Editor] Removed Rigidbody from CrashTestWall (static obstacle).");
            }

            // BoxCollider is created by CreatePrimitive — confirm
            var col = wall.GetComponent<BoxCollider>();
            if (col == null)
            {
                Undo.AddComponent<BoxCollider>(wall);
                Debug.Log("[Editor] Added BoxCollider to CrashTestWall.");
            }

            // ── 5. Mark scene dirty ───────────────────────────────────────────
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

            Undo.CollapseUndoOperations(undoGroup);

            Debug.Log("[Editor] Added CrashTestWall at (0, 1.5, 20), scale (10×3×0.5). " +
                      "No Rigidbody — acts as infinite-mass wall for WallMassRatio test.");
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
