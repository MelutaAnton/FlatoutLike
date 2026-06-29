#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using FlatOutLike.Data.Configs.AI;
using FlatOutLike.Controllers.Enemies;

namespace FlatOutLike.Editor.Configs
{
    /// <summary>
    /// Создаёт дефолтный ассет AISensorConfig либо синхронизирует поля,
    /// если ассет уже существует (GUID сохраняется).
    ///
    /// Поведение:
    ///   • Ассета нет → создаётся новый инстанс с дефолтными значениями из C#.
    ///   • Ассет есть → перезаписываем все поля геометрии лучей дефолтами
    ///     (_rayOriginUp, _rayLength, _raySideAngle, _sideRayLength, _obstacleLayerMask).
    ///
    /// После создания / обновления ассета скрипт ищет EnemyCarController в активной
    /// сцене и проставляет ему ссылку _sensorConfig. Если контроллер не найден —
    /// пишется warning без исключения.
    ///
    /// Идемпотентность: повторный запуск безопасен — ссылки перезаписываются текущим
    /// ассетом, дублирования нет, GUID не меняется.
    /// </summary>
    public static class CreateDefaultAISensorConfigEditor
    {
        private const string AssetPath     = "Assets/Development/Data/Configs/AI/AISensorConfig.asset";
        private const string FolderData    = "Assets/Development/Data";
        private const string FolderConfigs = "Assets/Development/Data/Configs";
        private const string FolderAI      = "Assets/Development/Data/Configs/AI";

        [MenuItem("FlatOutLike/Configs/Create Default AISensorConfig")]
        public static void CreateDefaultAISensorConfig()
        {
            var existing = AssetDatabase.LoadAssetAtPath<AISensorConfig>(AssetPath);

            if (existing != null)
            {
                SyncFields(existing);
                FillEnemyControllerRef(existing);
                return;
            }

            // ── Убеждаемся, что иерархия папок существует ─────────────────────
            EnsureFolder("Assets/Development", "Data");
            EnsureFolder(FolderData,           "Configs");
            EnsureFolder(FolderConfigs,        "AI");

            // ── Создание ассета ───────────────────────────────────────────────
            // C#-дефолты класса уже задают правильные значения геометрии.
            var config = ScriptableObject.CreateInstance<AISensorConfig>();
            AssetDatabase.CreateAsset(config, AssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Undo.RegisterCreatedObjectUndo(config, "Create AISensorConfig");

            Selection.activeObject = config;
            EditorGUIUtility.PingObject(config);

            Debug.Log("[Editor] Created AISensorConfig at " + AssetPath);

            FillEnemyControllerRef(config);
        }

        /// <summary>
        /// Записывает дефолтные значения в поля существующего ассета (upsert-стратегия).
        /// GUID ассета при этом не меняется — все существующие ссылки в сцене/префабах
        /// продолжают работать после синхронизации.
        /// </summary>
        private static void SyncFields(AISensorConfig asset)
        {
            var so = new SerializedObject(asset);

            SetFloat(so, "_rayOriginUp",   0.5f);
            SetFloat(so, "_rayLength",    12.0f);
            SetFloat(so, "_raySideAngle", 30.0f);
            SetFloat(so, "_sideRayLength", 2.5f);

            // LayerMask сериализуется как int; ~0 = Everything.
            var maskProp = so.FindProperty("_obstacleLayerMask");
            if (maskProp != null)
                maskProp.intValue = ~0;
            else
                Debug.LogWarning("[Editor] Field _obstacleLayerMask not found on AISensorConfig, skipping");

            so.ApplyModifiedProperties();

            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();

            Debug.Log("[Editor] Synced fields on existing AISensorConfig: " +
                      "_rayOriginUp=0.5, _rayLength=12, _raySideAngle=30, " +
                      "_sideRayLength=2.5, _obstacleLayerMask=Everything (~0)");
        }

        /// <summary>
        /// Проставляет ссылку _sensorConfig на EnemyCarController в активной сцене.
        /// Если контроллер не найден — предупреждение, без исключения.
        /// </summary>
        private static void FillEnemyControllerRef(AISensorConfig config)
        {
            var controller = Object.FindAnyObjectByType<EnemyCarController>();

            if (controller == null)
            {
                Debug.LogWarning("[Editor] EnemyCarController not found in active scene. " +
                                 "Open the test scene and re-run 'FlatOutLike/Configs/Create Default AISensorConfig' " +
                                 "to fill _sensorConfig automatically.");
                return;
            }

            var so   = new SerializedObject(controller);
            var prop = so.FindProperty("_sensorConfig");

            if (prop == null)
            {
                Debug.LogWarning("[Editor] Field _sensorConfig not found on EnemyCarController, skipping.");
                return;
            }

            prop.objectReferenceValue = config;
            so.ApplyModifiedProperties();

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

            Debug.Log("[Editor] EnemyCarController._sensorConfig = " + config.name +
                      " (сцена помечена dirty).");
        }

        // ─────────────────────────────────────────────────────────────────────

        private static void SetFloat(SerializedObject so, string fieldName, float value)
        {
            var prop = so.FindProperty(fieldName);
            if (prop != null)
                prop.floatValue = value;
            else
                Debug.LogWarning($"[Editor] Field {fieldName} not found on AISensorConfig, skipping");
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
