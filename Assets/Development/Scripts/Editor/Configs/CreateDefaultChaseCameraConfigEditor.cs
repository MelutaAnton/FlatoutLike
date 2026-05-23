#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using FlatOutLike.Data.Configs.Camera;

namespace FlatOutLike.Editor.Configs
{
    public static class CreateDefaultChaseCameraConfigEditor
    {
        private const string AssetPath      = "Assets/Development/Data/Configs/Camera/ChaseCameraConfig_Default.asset";
        private const string FolderData     = "Assets/Development/Data";
        private const string FolderCfg      = "Assets/Development/Data/Configs";
        private const string FolderCamera   = "Assets/Development/Data/Configs/Camera";

        [MenuItem("FlatOutLike/Configs/Create Default ChaseCameraConfig")]
        public static void CreateDefaultChaseCameraConfig()
        {
            // ── Idempotency check ─────────────────────────────────────────────
            if (AssetDatabase.LoadAssetAtPath<ChaseCameraConfig>(AssetPath) != null)
            {
                Debug.Log("[Editor] ChaseCameraConfig_Default already exists at " + AssetPath);
                return;
            }

            // ── Ensure folder hierarchy exists ────────────────────────────────
            EnsureFolder("Assets/Development", "Data");
            EnsureFolder(FolderData,           "Configs");
            EnsureFolder(FolderCfg,            "Camera");

            // ── Create asset ──────────────────────────────────────────────────
            var config = ScriptableObject.CreateInstance<ChaseCameraConfig>();
            AssetDatabase.CreateAsset(config, AssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Undo.RegisterCreatedObjectUndo(config, "Create ChaseCameraConfig_Default");

            Selection.activeObject        = config;
            EditorGUIUtility.PingObject(config);

            Debug.Log("[Editor] Created ChaseCameraConfig_Default at " + AssetPath);
        }

        // ─────────────────────────────────────────────────────────────────────
        private static void EnsureFolder(string parent, string folderName)
        {
            string full = parent + "/" + folderName;
            if (!AssetDatabase.IsValidFolder(full))
            {
                AssetDatabase.CreateFolder(parent, folderName);
                Debug.Log("[Editor] Created folder " + full);
            }
        }
    }
}
#endif
