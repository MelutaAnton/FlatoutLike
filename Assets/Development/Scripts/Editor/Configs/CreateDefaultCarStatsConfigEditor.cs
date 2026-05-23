#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using FlatOutLike.Data.Configs.Car;

namespace FlatOutLike.Editor.Configs
{
    public static class CreateDefaultCarStatsConfigEditor
    {
        private const string AssetPath   = "Assets/Development/Data/Configs/Car/CarStatsConfig_Default.asset";
        private const string FolderData  = "Assets/Development/Data";
        private const string FolderCfg   = "Assets/Development/Data/Configs";
        private const string FolderCar   = "Assets/Development/Data/Configs/Car";

        [MenuItem("FlatOutLike/Configs/Create Default CarStatsConfig")]
        public static void CreateDefaultCarStatsConfig()
        {
            // ── Idempotency check ─────────────────────────────────────────────
            if (AssetDatabase.LoadAssetAtPath<CarStatsConfig>(AssetPath) != null)
            {
                Debug.Log("[Editor] CarStatsConfig_Default already exists at " + AssetPath);
                return;
            }

            // ── Ensure folder hierarchy exists ────────────────────────────────
            EnsureFolder("Assets/Development", "Data");
            EnsureFolder(FolderData,           "Configs");
            EnsureFolder(FolderCfg,            "Car");

            // ── Create asset ──────────────────────────────────────────────────
            var config = ScriptableObject.CreateInstance<CarStatsConfig>();
            AssetDatabase.CreateAsset(config, AssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Undo.RegisterCreatedObjectUndo(config, "Create CarStatsConfig_Default");

            Selection.activeObject        = config;
            EditorGUIUtility.PingObject(config);

            Debug.Log("[Editor] Created CarStatsConfig_Default at " + AssetPath);
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
