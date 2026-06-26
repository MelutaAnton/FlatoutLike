#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using FlatOutLike.Data.Configs.Enemies;

namespace FlatOutLike.Editor.Configs
{
    /// <summary>
    /// Creates the default Aggressor EnemyPersonalityConfig asset.
    /// Default field values in the class already match the Aggressor profile
    /// (RamAggression=1, RetreatHpThreshold=0, NitroOnRam=true, etc.),
    /// so no extra SerializedObject overrides are needed.
    ///
    /// Idempotent: if the asset already exists at the target path it is left untouched.
    /// </summary>
    public static class CreateDefaultEnemyPersonalityConfigEditor
    {
        private const string AssetPath     = "Assets/Development/Data/Configs/Enemies/EnemyPersonalityConfig_Aggressor.asset";
        private const string FolderData    = "Assets/Development/Data";
        private const string FolderConfigs = "Assets/Development/Data/Configs";
        private const string FolderEnemies = "Assets/Development/Data/Configs/Enemies";

        [MenuItem("FlatOutLike/Configs/Create Default EnemyPersonalityConfig")]
        public static void CreateDefaultEnemyPersonalityConfig()
        {
            // ── Idempotency ───────────────────────────────────────────────────
            if (AssetDatabase.LoadAssetAtPath<EnemyPersonalityConfig>(AssetPath) != null)
            {
                Debug.Log("[Editor] EnemyPersonalityConfig_Aggressor already exists at " + AssetPath +
                          ". Skipping creation.");
                return;
            }

            // ── Ensure folder hierarchy ───────────────────────────────────────
            EnsureFolder("Assets/Development", "Data");
            EnsureFolder(FolderData,           "Configs");
            EnsureFolder(FolderConfigs,        "Enemies");

            // ── Create asset ──────────────────────────────────────────────────
            // Class defaults already define the Aggressor profile; no extra setup required.
            var config = ScriptableObject.CreateInstance<EnemyPersonalityConfig>();
            AssetDatabase.CreateAsset(config, AssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Undo.RegisterCreatedObjectUndo(config, "Create EnemyPersonalityConfig");

            Selection.activeObject = config;
            EditorGUIUtility.PingObject(config);

            Debug.Log("[Editor] Created EnemyPersonalityConfig (Aggressor defaults) at " + AssetPath);
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
