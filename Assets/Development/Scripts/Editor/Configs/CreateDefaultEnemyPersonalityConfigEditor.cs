#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using FlatOutLike.Data.Configs.Enemies;

namespace FlatOutLike.Editor.Configs
{
    /// <summary>
    /// Создаёт дефолтный ассет EnemyPersonalityConfig (Aggressor) либо синхронизирует
    /// недавно добавленные поля тюнинга, если ассет уже существует (GUID сохраняется).
    ///
    /// Поведение:
    ///   • Ассета нет → создаётся новый инстанс; C#-дефолты покрывают все поля.
    ///   • Ассет есть → перезаписываем дефолты для AI-tuning полей
    ///     (_stuckTime, _circleBreakTime, _graceTime, _ramAlignmentDot,
    ///     _minApproachSpeed, _approachTime, _obstacleAvoidDistance,
    ///     _obstacleSideAvoidDistance). Остальные поля (RamAggression, дистанции
    ///     и т.д.) намеренно не трогаются — чтобы ручной тюнинг из Inspector
    ///     сохранился.
    ///
    /// Идемпотентность: повторный запуск этого пункта меню безопасен — каждый раз
    /// пишутся те же значения, без дубликатов и без смены GUID.
    ///
    /// Null-safe: если поле не найдено ни на ассете, ни на инстансе с дефолтами
    /// (например, после смены схемы) — пишется warning и поле пропускается, без NRE.
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
            var existing = AssetDatabase.LoadAssetAtPath<EnemyPersonalityConfig>(AssetPath);

            if (existing != null)
            {
                SyncNewTuningFields(existing);
                return;
            }

            // ── Убеждаемся, что иерархия папок существует ─────────────────────
            EnsureFolder("Assets/Development", "Data");
            EnsureFolder(FolderData,           "Configs");
            EnsureFolder(FolderConfigs,        "Enemies");

            // ── Создание ассета ───────────────────────────────────────────────
            // Дефолты класса уже задают профиль Aggressor; дополнительная настройка не нужна.
            var config = ScriptableObject.CreateInstance<EnemyPersonalityConfig>();
            AssetDatabase.CreateAsset(config, AssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Undo.RegisterCreatedObjectUndo(config, "Create EnemyPersonalityConfig");

            Selection.activeObject = config;
            EditorGUIUtility.PingObject(config);

            Debug.Log("[Editor] Created EnemyPersonalityConfig (Aggressor defaults) at " + AssetPath);
        }

        /// <summary>
        /// Записывает C#-дефолты класса в поля тюнинга, которые были добавлены уже
        /// после создания ассета. Значения берутся из свежего временного инстанса,
        /// чтобы magic numbers здесь не дублировались — класс остаётся единственным
        /// источником истины. Каждое поле под защитой: если FindProperty вернул null
        /// (смена схемы), поле пропускается с warning'ом вместо NullReferenceException.
        /// </summary>
        private static void SyncNewTuningFields(EnemyPersonalityConfig asset)
        {
            // Поля для синхронизации — обновлять этот список при добавлении новых tuning-полей в класс.
            var fieldNames = new[]
            {
                "_stuckTime", "_circleBreakTime", "_graceTime",
                "_ramAlignmentDot", "_minApproachSpeed", "_approachTime",
                "_obstacleAvoidDistance", "_obstacleSideAvoidDistance"
            };

            // Поднимаем временный инстанс, чтобы прочитать C#-дефолты конструктора.
            var defaults   = ScriptableObject.CreateInstance<EnemyPersonalityConfig>();
            var soDefaults = new SerializedObject(defaults);

            // Читаем дефолтные значения с null-guard на стороне defaults.
            var defaultValues = new System.Collections.Generic.Dictionary<string, float>();
            foreach (var fieldName in fieldNames)
            {
                var propDefault = soDefaults.FindProperty(fieldName);
                if (propDefault == null)
                {
                    Debug.LogWarning($"[Editor] Field {fieldName} not found on defaults instance, skipping");
                    continue;
                }
                defaultValues[fieldName] = propDefault.floatValue;
            }

            Object.DestroyImmediate(defaults);

            // Применяем к существующему ассету — GUID не трогается.
            var soExisting = new SerializedObject(asset);
            foreach (var kvp in defaultValues)
            {
                var propExisting = soExisting.FindProperty(kvp.Key);
                if (propExisting == null)
                {
                    Debug.LogWarning($"[Editor] Field {kvp.Key} not found on existing asset, skipping");
                    continue;
                }
                propExisting.floatValue = kvp.Value;
            }
            soExisting.ApplyModifiedProperties();

            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();

            float stuckTime                 = defaultValues.TryGetValue("_stuckTime",                 out var s)  ? s  : float.NaN;
            float circleBreakTime           = defaultValues.TryGetValue("_circleBreakTime",           out var c)  ? c  : float.NaN;
            float graceTime                 = defaultValues.TryGetValue("_graceTime",                 out var g)  ? g  : float.NaN;
            float ramAlignmentDot           = defaultValues.TryGetValue("_ramAlignmentDot",           out var a)  ? a  : float.NaN;
            float minApproachSpeed          = defaultValues.TryGetValue("_minApproachSpeed",          out var m)  ? m  : float.NaN;
            float approachTime              = defaultValues.TryGetValue("_approachTime",              out var t)  ? t  : float.NaN;
            float obstacleAvoidDistance     = defaultValues.TryGetValue("_obstacleAvoidDistance",     out var o)  ? o  : float.NaN;
            float obstacleSideAvoidDistance = defaultValues.TryGetValue("_obstacleSideAvoidDistance", out var os) ? os : float.NaN;

            Debug.Log($"[Editor] Synced tuning fields on existing EnemyPersonalityConfig_Aggressor: " +
                      $"_stuckTime={stuckTime}, " +
                      $"_circleBreakTime={circleBreakTime}, " +
                      $"_graceTime={graceTime}, " +
                      $"_ramAlignmentDot={ramAlignmentDot}, " +
                      $"_minApproachSpeed={minApproachSpeed}, " +
                      $"_approachTime={approachTime}, " +
                      $"_obstacleAvoidDistance={obstacleAvoidDistance}, " +
                      $"_obstacleSideAvoidDistance={obstacleSideAvoidDistance}");
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
