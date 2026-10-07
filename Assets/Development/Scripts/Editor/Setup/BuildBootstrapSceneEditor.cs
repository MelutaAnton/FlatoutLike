#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using FlatOutLike.Models;
using FlatOutLike.Editor.Tools;
using FlatOutLike.Installers;

namespace FlatOutLike.Editor.Setup
{
    public static class BuildBootstrapSceneEditor
    {
        private static readonly string ScenePath = ScenePaths.For(SceneKeys.Boot);
        private const string ScenesFolder = "Assets/Development/Scenes";
        private const string ScenesParent = "Assets/Development";

        [MenuItem("FlatOutLike/Setup/Build Bootstrap Scene")]
        public static void BuildBootstrapScene()
        {
            Undo.SetCurrentGroupName("Build Bootstrap Scene");
            int undoGroup = Undo.GetCurrentGroup();

            // ── 1. Open or create Boot.unity ──────────────────────────────────
            var scene = OpenOrCreateScene();

            // ── 2. GameLifetimeScope ──────────────────────────────────────────
            var existingScope = Object.FindAnyObjectByType<GameLifetimeScope>();
            if (existingScope != null)
            {
                Debug.Log("[Editor] Found existing 'GameLifetimeScope' (component GameLifetimeScope already present)");
            }
            else
            {
                var go = new GameObject("GameLifetimeScope");
                Undo.RegisterCreatedObjectUndo(go, "Create GameLifetimeScope");
                Undo.AddComponent<GameLifetimeScope>(go);
                Debug.Log("[Editor] Created GameObject 'GameLifetimeScope' with component GameLifetimeScope");
            }

            // ── 3. GameBootstrap ──────────────────────────────────────────────
            var existingBootstrap = Object.FindAnyObjectByType<GameBootstrap>();
            if (existingBootstrap != null)
            {
                Debug.Log("[Editor] Found existing 'GameBootstrap' (component GameBootstrap already present)");
            }
            else
            {
                var go = new GameObject("GameBootstrap");
                Undo.RegisterCreatedObjectUndo(go, "Create GameBootstrap");
                Undo.AddComponent<GameBootstrap>(go);
                Debug.Log("[Editor] Created GameObject 'GameBootstrap' with component GameBootstrap");
            }

            // ── 4. Save ───────────────────────────────────────────────────────
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("[Editor] Bootstrap scene saved at " + ScenePath);

            Undo.CollapseUndoOperations(undoGroup);

            Debug.Log("[Editor] Bootstrap scene build complete.");
        }

        // ─────────────────────────────────────────────────────────────────────
        private static UnityEngine.SceneManagement.Scene OpenOrCreateScene()
        {
            if (System.IO.File.Exists(
                    System.IO.Path.Combine(
                        System.IO.Directory.GetCurrentDirectory(), ScenePath)))
            {
                var existing = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                Debug.Log("[Editor] Opened existing Boot.unity at " + ScenePath);
                return existing;
            }

            // Ensure the Scenes folder exists as a Unity asset folder
            if (!AssetDatabase.IsValidFolder(ScenesFolder))
            {
                AssetDatabase.CreateFolder(ScenesParent, "Scenes");
                Debug.Log("[Editor] Created folder " + ScenesFolder);
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("[Editor] Created Boot.unity at " + ScenePath);
            return scene;
        }
    }
}
#endif
