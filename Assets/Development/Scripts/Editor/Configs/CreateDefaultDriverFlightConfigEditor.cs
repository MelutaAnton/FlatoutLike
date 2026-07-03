#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using FlatOutLike.Data.Configs.Driver;

namespace FlatOutLike.Editor.Configs
{
    /// <summary>
    /// Создаёт (или обновляет) Assets/Development/Data/Configs/Driver/DriverFlightConfig_Default.asset.
    ///
    /// Поля:
    ///   _steerInfluence          = 6    (влияние управления на вращение машины в полёте)
    ///   _steerSpeedPenalty       = 0.15 (штраф скорости при активном управлении в полёте)
    ///   _boostImpulse            = 8    (импульс буста прыжка)
    ///   _boostCount               = 1    (количество доступных бустов за один прыжок)
    ///   _rotationBonusThreshold  = 180  (порог поворота для бонуса за флип, градусы)
    ///
    /// Идемпотентность: если ассет уже существует — поля перезаписываются, GUID сохраняется.
    /// </summary>
    public static class CreateDefaultDriverFlightConfigEditor
    {
        private const string AssetPath  = "Assets/Development/Data/Configs/Driver/DriverFlightConfig_Default.asset";
        private const string FolderData = "Assets/Development/Data";
        private const string FolderCfg  = "Assets/Development/Data/Configs";
        private const string FolderDrv  = "Assets/Development/Data/Configs/Driver";

        [MenuItem("FlatOutLike/Configs/Create Default DriverFlightConfig")]
        public static void CreateDefaultDriverFlightConfig()
        {
            Undo.SetCurrentGroupName("Create Default DriverFlightConfig");

            // ── 1. Папки ─────────────────────────────────────────────────────
            EnsureFolder("Assets/Development", "Data");
            EnsureFolder(FolderData, "Configs");
            EnsureFolder(FolderCfg,  "Driver");

            // ── 2. Создаём или загружаем ассет (идемпотентно по пути) ─────────
            var config = AssetDatabase.LoadAssetAtPath<DriverFlightConfig>(AssetPath);
            bool isNew = config == null;

            if (isNew)
            {
                config = ScriptableObject.CreateInstance<DriverFlightConfig>();
                AssetDatabase.CreateAsset(config, AssetPath);
                AssetDatabase.SaveAssets();
                Undo.RegisterCreatedObjectUndo(config, "Create DriverFlightConfig");
                Debug.Log("[Editor] Создан DriverFlightConfig_Default.asset: " + AssetPath);
            }
            else
            {
                Debug.Log("[Editor] DriverFlightConfig_Default.asset уже существует — обновляем поля.");
            }

            // ── 3. Заполняем поля через SerializedObject ──────────────────────
            var so = new SerializedObject(config);
            so.FindProperty("_steerInfluence").floatValue         = 6f;
            so.FindProperty("_steerSpeedPenalty").floatValue      = 0.15f;
            so.FindProperty("_boostImpulse").floatValue           = 8f;
            so.FindProperty("_boostCount").intValue                = 1;
            so.FindProperty("_rotationBonusThreshold").floatValue = 180f;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();

            Selection.activeObject = config;
            EditorGUIUtility.PingObject(config);

            Debug.Log("[Editor] DriverFlightConfig заполнен: _steerInfluence=6, _steerSpeedPenalty=0.15, " +
                      "_boostImpulse=8, _boostCount=1, _rotationBonusThreshold=180.");
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
