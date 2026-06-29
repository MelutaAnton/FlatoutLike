#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FlatOutLike.Editor.Setup
{
    /// <summary>
    /// Добавляет статическую стену-препятствие в _TestDriving для проверки урона
    /// от столкновений. Стена располагается в (0, 1.5, 20) — прямо по курсу машины
    /// от точки спавна (0, 1, 0) на трассе разгона по +Z. Это BoxCollider без
    /// Rigidbody (бесконечная масса), что включает ветку WallMassRatio в DamageService.
    ///
    /// Идемпотентность: перед созданием проверяет наличие "CrashTestWall" по имени.
    /// </summary>
    public static class AddCrashWallToTestDrivingEditor
    {
        private const string ScenePath = "Assets/Development/Scenes/_TestDriving.unity";
        // internal — чтобы CreatePhysicsLayersEditor мог назначить слой Wall по тому же имени
        internal const string WallName  = "CrashTestWall";

        [MenuItem("FlatOutLike/Setup/Add Crash Wall to Test Driving")]
        public static void AddCrashWall()
        {
            Undo.SetCurrentGroupName("Add Crash Wall to Test Driving");
            int undoGroup = Undo.GetCurrentGroup();

            // ── 1. Убеждаемся, что _TestDriving открыт ────────────────────────
            OpenSceneIfNeeded();

            // ── 2. Идемпотентность: проверка по уникальному имени стены ───────
            var existing = GameObject.Find(WallName);
            if (existing != null)
            {
                Debug.Log("[Editor] CrashTestWall already exists in scene at " +
                          existing.transform.position + ", skipping.");
                return;
            }

            // ── 3. Создаём стену как Cube-примитив (идёт с BoxCollider) ───────
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Undo.RegisterCreatedObjectUndo(wall, "Create CrashTestWall");
            wall.name = WallName;

            // Позиция: 20 единиц по курсу от точки спавна; поднята до Y=1.5, чтобы
            // центр коробки совпадал с корпусом машины (высота машины ~1.4 единицы).
            // Размеры: 10 в ширину × 3 в высоту × 0.5 в глубину — достаточно широкая, чтобы машина не объехала её.
            wall.transform.position   = new Vector3(0f, 1.5f, 20f);
            wall.transform.localScale = new Vector3(10f, 3f, 0.5f);

            // ── 4. Убеждаемся, что нет Rigidbody (статика = бесконечная масса для WallMassRatio) ──
            // CreatePrimitive никогда не добавляет Rigidbody, но проверяем на всякий случай.
            var rb = wall.GetComponent<Rigidbody>();
            if (rb != null)
            {
                Object.DestroyImmediate(rb);
                Debug.Log("[Editor] Removed Rigidbody from CrashTestWall (static obstacle).");
            }

            // BoxCollider создаётся CreatePrimitive — подтверждаем
            var col = wall.GetComponent<BoxCollider>();
            if (col == null)
            {
                Undo.AddComponent<BoxCollider>(wall);
                Debug.Log("[Editor] Added BoxCollider to CrashTestWall.");
            }

            // ── 5. Помечаем сцену как dirty ───────────────────────────────────
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
