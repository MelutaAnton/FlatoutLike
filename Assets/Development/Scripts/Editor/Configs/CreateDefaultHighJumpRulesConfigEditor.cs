#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using FlatOutLike.Data.Configs.Stunts;

namespace FlatOutLike.Editor.Configs
{
    /// <summary>
    /// Создаёт (или обновляет) Assets/Development/Data/Configs/Stunts/HighJumpRulesConfig_Default.asset.
    ///
    /// Поля (композитный счёт scorePerMeter/flipBonus удалён — результат трюка только высота):
    ///   _heightToCoinRatio  = 5  (монет за метр высоты)
    ///   _flightTimeoutSeconds = 20 (максимальное время полёта до принудительного завершения, сек)
    ///
    /// Идемпотентность: если ассет уже существует — поля перезаписываются, GUID сохраняется.
    /// </summary>
    public static class CreateDefaultHighJumpRulesConfigEditor
    {
        private const string AssetPath  = "Assets/Development/Data/Configs/Stunts/HighJumpRulesConfig_Default.asset";
        private const string FolderData = "Assets/Development/Data";
        private const string FolderCfg  = "Assets/Development/Data/Configs";
        private const string FolderStn  = "Assets/Development/Data/Configs/Stunts";

        [MenuItem("FlatOutLike/Configs/Create Default HighJumpRulesConfig")]
        public static void CreateDefaultHighJumpRulesConfig()
        {
            Undo.SetCurrentGroupName("Create Default HighJumpRulesConfig");

            // ── 1. Папки ─────────────────────────────────────────────────────
            EnsureFolder("Assets/Development", "Data");
            EnsureFolder(FolderData, "Configs");
            EnsureFolder(FolderCfg,  "Stunts");

            // ── 2. Создаём или загружаем ассет (идемпотентно по пути) ─────────
            var config = AssetDatabase.LoadAssetAtPath<HighJumpRulesConfig>(AssetPath);
            bool isNew = config == null;

            if (isNew)
            {
                config = ScriptableObject.CreateInstance<HighJumpRulesConfig>();
                AssetDatabase.CreateAsset(config, AssetPath);
                AssetDatabase.SaveAssets();
                Undo.RegisterCreatedObjectUndo(config, "Create HighJumpRulesConfig");
                Debug.Log("[Editor] Создан HighJumpRulesConfig_Default.asset: " + AssetPath);
            }
            else
            {
                Debug.Log("[Editor] HighJumpRulesConfig_Default.asset уже существует — обновляем поля.");
            }

            // ── 3. Заполняем поля через SerializedObject ──────────────────────
            var so = new SerializedObject(config);
            so.FindProperty("_heightToCoinRatio").floatValue     = 5f;
            so.FindProperty("_flightTimeoutSeconds").floatValue  = 20f;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();

            Selection.activeObject = config;
            EditorGUIUtility.PingObject(config);

            Debug.Log("[Editor] HighJumpRulesConfig заполнен: _heightToCoinRatio=5, _flightTimeoutSeconds=20.");
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
