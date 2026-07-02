#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using FlatOutLike.Data.Configs.Driver;

namespace FlatOutLike.Editor.Configs
{
    /// <summary>
    /// Создаёт (или обновляет) Assets/Development/Data/Configs/Driver/DriverEjectConfig_Default.asset.
    ///
    /// Поля:
    ///   _forwardImpulse         = 8   (импульс выброса водителя вперёд при eject)
    ///   _upImpulse              = 6   (импульс выброса водителя вверх при eject)
    ///   _carVelocityInheritance = 0.6 (доля скорости машины, наследуемая водителем)
    ///
    /// Идемпотентность: если ассет уже существует — поля перезаписываются, GUID сохраняется.
    /// </summary>
    public static class CreateDefaultDriverEjectConfigEditor
    {
        private const string AssetPath   = "Assets/Development/Data/Configs/Driver/DriverEjectConfig_Default.asset";
        private const string FolderData  = "Assets/Development/Data";
        private const string FolderCfg   = "Assets/Development/Data/Configs";
        private const string FolderDrv   = "Assets/Development/Data/Configs/Driver";

        [MenuItem("FlatOutLike/Configs/Create Default DriverEjectConfig")]
        public static void CreateDefaultDriverEjectConfig()
        {
            Undo.SetCurrentGroupName("Create Default DriverEjectConfig");

            // ── 1. Папки ─────────────────────────────────────────────────────
            EnsureFolder("Assets/Development", "Data");
            EnsureFolder(FolderData, "Configs");
            EnsureFolder(FolderCfg,  "Driver");

            // ── 2. Создаём или загружаем ассет (идемпотентно по пути) ─────────
            var config = AssetDatabase.LoadAssetAtPath<DriverEjectConfig>(AssetPath);
            bool isNew = config == null;

            if (isNew)
            {
                config = ScriptableObject.CreateInstance<DriverEjectConfig>();
                AssetDatabase.CreateAsset(config, AssetPath);
                AssetDatabase.SaveAssets();
                Undo.RegisterCreatedObjectUndo(config, "Create DriverEjectConfig");
                Debug.Log("[Editor] Создан DriverEjectConfig_Default.asset: " + AssetPath);
            }
            else
            {
                Debug.Log("[Editor] DriverEjectConfig_Default.asset уже существует — обновляем поля.");
            }

            // ── 3. Заполняем поля через SerializedObject ──────────────────────
            var so = new SerializedObject(config);
            so.FindProperty("_forwardImpulse").floatValue          = 8f;
            so.FindProperty("_upImpulse").floatValue               = 6f;
            so.FindProperty("_carVelocityInheritance").floatValue  = 0.6f;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();

            Selection.activeObject = config;
            EditorGUIUtility.PingObject(config);

            Debug.Log("[Editor] DriverEjectConfig заполнен: _forwardImpulse=8, _upImpulse=6, _carVelocityInheritance=0.6.");
        }

        // ─────────────────────────────────────────────────────────────────────
        private static void EnsureFolder(string parent, string folderName)
        {
            string full = parent + "/" + folderName;
            if (!AssetDatabase.IsValidFolder(full))
            {
                AssetDatabase.CreateFolder(parent, folderName);
                Debug.Log("[Editor] Создана папка: " + full);
            }
        }
    }
}
#endif
