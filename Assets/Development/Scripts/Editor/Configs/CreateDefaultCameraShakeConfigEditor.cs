#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using FlatOutLike.Data.Configs.Camera;

namespace FlatOutLike.Editor.Configs
{
    /// <summary>
    /// Создаёт (или обновляет) Assets/Development/Data/Configs/Camera/CameraShakeConfig_Default.asset.
    ///
    /// Поля:
    ///   _minImpactSpeed = 4     (порог скорости удара, ниже которого тряски нет)
    ///   _speedToForce   = 0.08  (множитель избыточной скорости удара в силу импульса)
    ///   _maxForce       = 1.2   (потолок силы импульса)
    ///
    /// Идемпотентность: если ассет уже существует — поля перезаписываются, GUID сохраняется.
    /// </summary>
    public static class CreateDefaultCameraShakeConfigEditor
    {
        private const string AssetPath  = "Assets/Development/Data/Configs/Camera/CameraShakeConfig_Default.asset";
        private const string FolderData = "Assets/Development/Data";
        private const string FolderCfg  = "Assets/Development/Data/Configs";
        private const string FolderCam  = "Assets/Development/Data/Configs/Camera";

        [MenuItem("FlatOutLike/Configs/Create Default CameraShakeConfig")]
        public static void CreateDefaultCameraShakeConfig()
        {
            Undo.SetCurrentGroupName("Create Default CameraShakeConfig");

            // ── 1. Папки ─────────────────────────────────────────────────────
            EnsureFolder("Assets/Development", "Data");
            EnsureFolder(FolderData, "Configs");
            EnsureFolder(FolderCfg,  "Camera");

            // ── 2. Создаём или загружаем ассет (идемпотентно по пути) ─────────
            var config = AssetDatabase.LoadAssetAtPath<CameraShakeConfig>(AssetPath);
            bool isNew = config == null;

            if (isNew)
            {
                config = ScriptableObject.CreateInstance<CameraShakeConfig>();
                AssetDatabase.CreateAsset(config, AssetPath);
                AssetDatabase.SaveAssets();
                Undo.RegisterCreatedObjectUndo(config, "Create CameraShakeConfig");
                Debug.Log("[Editor] Создан CameraShakeConfig_Default.asset: " + AssetPath);
            }
            else
            {
                Debug.Log("[Editor] CameraShakeConfig_Default.asset уже существует — обновляем поля.");
            }

            // ── 3. Заполняем поля через SerializedObject ──────────────────────
            var so = new SerializedObject(config);
            so.FindProperty("_minImpactSpeed").floatValue = 4f;
            so.FindProperty("_speedToForce").floatValue   = 0.08f;
            so.FindProperty("_maxForce").floatValue       = 1.2f;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();

            Selection.activeObject = config;
            EditorGUIUtility.PingObject(config);

            Debug.Log("[Editor] CameraShakeConfig заполнен: _minImpactSpeed=4, _speedToForce=0.08, _maxForce=1.2.");
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
