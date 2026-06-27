#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using FlatOutLike.Data.Configs.Combat;

namespace FlatOutLike.Editor.Configs
{
    public static class CreateDefaultDamageConfigEditor
    {
        private const string AssetPath    = "Assets/Development/Data/Configs/Combat/DamageConfig.asset";
        private const string FolderData   = "Assets/Development/Data";
        private const string FolderCfg    = "Assets/Development/Data/Configs";
        private const string FolderCombat = "Assets/Development/Data/Configs/Combat";

        [MenuItem("FlatOutLike/Configs/Create Default DamageConfig")]
        public static void CreateDefaultDamageConfig()
        {
            // ── Проверка идемпотентности ──────────────────────────────────────
            if (AssetDatabase.LoadAssetAtPath<DamageConfig>(AssetPath) != null)
            {
                Debug.Log("[Editor] DamageConfig already exists at " + AssetPath);
                return;
            }

            // ── Убеждаемся, что иерархия папок существует ─────────────────────
            EnsureFolder("Assets/Development", "Data");
            EnsureFolder(FolderData,           "Configs");
            EnsureFolder(FolderCfg,            "Combat");

            // ── Создание ассета ───────────────────────────────────────────────
            var config = ScriptableObject.CreateInstance<DamageConfig>();
            AssetDatabase.CreateAsset(config, AssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // ── Явные дефолтные значения (DamageScale=1, MinImpactSpeed=3, WallMassRatio=1) ──
            // Field-initializers класса уже совпадают с этими значениями; задаём явно для ясности.
            var so = new SerializedObject(config);
            so.FindProperty("_damageScale").floatValue    = 1f;
            so.FindProperty("_minImpactSpeed").floatValue = 3f;
            so.FindProperty("_wallMassRatio").floatValue  = 1f;
            so.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();

            Undo.RegisterCreatedObjectUndo(config, "Create DamageConfig");

            Selection.activeObject      = config;
            EditorGUIUtility.PingObject(config);

            Debug.Log("[Editor] Created DamageConfig at " + AssetPath);
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
