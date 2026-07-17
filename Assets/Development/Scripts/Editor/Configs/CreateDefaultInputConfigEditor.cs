#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using FlatOutLike.Data.Configs.Input;

namespace FlatOutLike.Editor.Configs
{
    /// <summary>
    /// Создаёт единый на весь проект ассет InputConfig — переключает схему управления
    /// (клавиатура/мобильный HUD) во всех сценах одной галочкой (см. InputConfig).
    ///
    /// Early-exit, если ассет уже существует: тюнинг значения _forceMobileInput в
    /// Inspector'е (пользователь мог выключить мобильный HUD для конкретного теста)
    /// НЕ затирается повторным прогоном.
    ///
    /// При СОЗДАНИИ ассета _forceMobileInput выставляется в true — решение пользователя:
    /// на момент шага 9 все геймплейные сцены тестируются с мобильным HUD поверх PC.
    /// </summary>
    public static class CreateDefaultInputConfigEditor
    {
        private const string AssetPath  = "Assets/Development/Data/Configs/Input/InputConfig_Default.asset";
        private const string FolderData = "Assets/Development/Data";
        private const string FolderConfigs = "Assets/Development/Data/Configs";
        private const string FolderInput = "Assets/Development/Data/Configs/Input";

        [MenuItem("FlatOutLike/Configs/Create Default InputConfig")]
        public static void CreateDefaultInputConfig()
        {
            var config = EnsureAsset();
            Selection.activeObject = config;
            EditorGUIUtility.PingObject(config);
        }

        /// <summary>
        /// Возвращает существующий ассет либо создаёт новый с дефолтными значениями.
        /// Публичный метод — переиспользуется WireInputConfigEditor.
        /// </summary>
        public static InputConfig EnsureAsset()
        {
            var existing = AssetDatabase.LoadAssetAtPath<InputConfig>(AssetPath);
            if (existing != null)
            {
                Debug.Log("[Editor] InputConfig уже существует: " + AssetPath + " (тюнинг не тронут).");
                return existing;
            }

            EnsureFolder("Assets/Development", "Data");
            EnsureFolder(FolderData, "Configs");
            EnsureFolder(FolderConfigs, "Input");

            var config = ScriptableObject.CreateInstance<InputConfig>();
            AssetDatabase.CreateAsset(config, AssetPath);

            // Решение пользователя (шаг 9): сохраняем текущее поведение проекта —
            // сцены тестируются с мобильным HUD на PC.
            var so = new SerializedObject(config);
            var prop = so.FindProperty("_forceMobileInput");
            if (prop != null)
            {
                prop.boolValue = true;
                so.ApplyModifiedProperties();
            }
            else
            {
                Debug.LogWarning("[Editor] Поле InputConfig._forceMobileInput не найдено (рантайм ещё не скомпилирован?).");
            }

            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Undo.RegisterCreatedObjectUndo(config, "Create InputConfig");

            Debug.Log("[Editor] Создан InputConfig at " + AssetPath + " (_forceMobileInput = true).");
            return config;
        }

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
