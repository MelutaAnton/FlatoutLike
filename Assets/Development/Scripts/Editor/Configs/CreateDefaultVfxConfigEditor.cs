#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using FlatOutLike.Data.Configs.Vfx;

namespace FlatOutLike.Editor.Configs
{
    public static class CreateDefaultVfxConfigEditor
    {
        private const string AssetPath = "Assets/Development/Data/Configs/Vfx/VfxConfig.asset";
        private const string FolderData = "Assets/Development/Data";
        private const string FolderCfg  = "Assets/Development/Data/Configs";
        private const string FolderVfx  = "Assets/Development/Data/Configs/Vfx";

        [MenuItem("FlatOutLike/Configs/Create Default VfxConfig")]
        public static void CreateDefaultVfxConfig()
        {
            // ── Проверка идемпотентности ──────────────────────────────────────
            if (AssetDatabase.LoadAssetAtPath<VfxConfig>(AssetPath) != null)
            {
                Debug.Log("[Editor] VfxConfig already exists at " + AssetPath);
                return;
            }

            // ── Убеждаемся, что иерархия папок существует ─────────────────────
            EnsureFolder("Assets/Development", "Data");
            EnsureFolder(FolderData,           "Configs");
            EnsureFolder(FolderCfg,            "Vfx");

            // ── Создание ассета ───────────────────────────────────────────────
            var config = ScriptableObject.CreateInstance<VfxConfig>();
            AssetDatabase.CreateAsset(config, AssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // ── Явно задаём entries как пустой массив (заполнить после создания VFX-префабов) ──
            var so = new SerializedObject(config);
            so.FindProperty("_entries").arraySize = 0;
            so.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();

            Undo.RegisterCreatedObjectUndo(config, "Create VfxConfig");

            Selection.activeObject      = config;
            EditorGUIUtility.PingObject(config);

            Debug.Log("[Editor] Created VfxConfig at " + AssetPath +
                      " (_entries is empty — add VfxEntry items after creating VFX prefabs).");
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
