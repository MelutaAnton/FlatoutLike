#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using FlatOutLike.Data.Configs.Camera;

namespace FlatOutLike.Editor.Configs
{
    /// <summary>
    /// Создаёт (или обновляет) Assets/Development/Data/Configs/Camera/OrbitCameraConfig_Default.asset.
    ///
    /// Поля:
    ///   _distance      = 6     (дистанция орбитальной камеры от водителя)
    ///   _height        = 2.5   (высота над водителем)
    ///   _orbitSpeed    = 30    (скорость вращения вокруг цели, градусов/сек)
    ///   _followDamping = 0.2   (сглаживание следования)
    ///
    /// Идемпотентность: если ассет уже существует — поля перезаписываются, GUID сохраняется.
    /// </summary>
    public static class CreateDefaultOrbitCameraConfigEditor
    {
        private const string AssetPath  = "Assets/Development/Data/Configs/Camera/OrbitCameraConfig_Default.asset";
        private const string FolderData = "Assets/Development/Data";
        private const string FolderCfg  = "Assets/Development/Data/Configs";
        private const string FolderCam  = "Assets/Development/Data/Configs/Camera";

        [MenuItem("FlatOutLike/Configs/Create Default OrbitCameraConfig")]
        public static void CreateDefaultOrbitCameraConfig()
        {
            Undo.SetCurrentGroupName("Create Default OrbitCameraConfig");

            // ── 1. Папки ─────────────────────────────────────────────────────
            EnsureFolder("Assets/Development", "Data");
            EnsureFolder(FolderData, "Configs");
            EnsureFolder(FolderCfg,  "Camera");

            // ── 2. Создаём или загружаем ассет (идемпотентно по пути) ─────────
            var config = AssetDatabase.LoadAssetAtPath<OrbitCameraConfig>(AssetPath);
            bool isNew = config == null;

            if (isNew)
            {
                config = ScriptableObject.CreateInstance<OrbitCameraConfig>();
                AssetDatabase.CreateAsset(config, AssetPath);
                AssetDatabase.SaveAssets();
                Undo.RegisterCreatedObjectUndo(config, "Create OrbitCameraConfig");
                Debug.Log("[Editor] Создан OrbitCameraConfig_Default.asset: " + AssetPath);
            }
            else
            {
                Debug.Log("[Editor] OrbitCameraConfig_Default.asset уже существует — обновляем поля.");
            }

            // ── 3. Заполняем поля через SerializedObject ──────────────────────
            var so = new SerializedObject(config);
            so.FindProperty("_distance").floatValue      = 6f;
            so.FindProperty("_height").floatValue         = 2.5f;
            so.FindProperty("_orbitSpeed").floatValue     = 30f;
            so.FindProperty("_followDamping").floatValue  = 0.2f;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();

            Selection.activeObject = config;
            EditorGUIUtility.PingObject(config);

            Debug.Log("[Editor] OrbitCameraConfig заполнен: _distance=6, _height=2.5, " +
                      "_orbitSpeed=30, _followDamping=0.2.");
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
