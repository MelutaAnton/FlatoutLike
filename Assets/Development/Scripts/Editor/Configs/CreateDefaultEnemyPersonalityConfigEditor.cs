#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using FlatOutLike.Data.Configs.Enemies;

namespace FlatOutLike.Editor.Configs
{
    /// <summary>
    /// Создаёт и синхронизирует ассеты EnemyPersonalityConfig для трёх профилей:
    ///   • Aggressor    — агрессивный таранщик, целится в ближайшего (Nearest).
    ///   • Coward       — осторожный добивала, целится в самого слабого (Weakest).
    ///   • PlayerHunter — охотник на игрока, всегда преследует игрока (Player).
    ///
    /// Aggressor — базовый профиль; Coward и PlayerHunter копируются с него
    /// через AssetDatabase.CopyAsset, после чего переопределяются только
    /// отличающиеся поля. Такой подход сохраняет весь отыгранный тюнинг движения.
    ///
    /// Идемпотентность: повторный запуск безопасен — GUID ассетов не меняется,
    /// поля записываются теми же значениями.
    ///
    /// Порядок запуска:
    ///   1. FlatOutLike/Configs/Create Default EnemyPersonalityConfig  (Aggressor)
    ///   2. FlatOutLike/Configs/Create EnemyPersonalityConfig Coward
    ///   3. FlatOutLike/Configs/Create EnemyPersonalityConfig PlayerHunter
    /// </summary>
    public static class CreateDefaultEnemyPersonalityConfigEditor
    {
        private const string AssetPath        = "Assets/Development/Data/Configs/Enemies/EnemyPersonalityConfig_Aggressor.asset";
        private const string CowardPath       = "Assets/Development/Data/Configs/Enemies/EnemyPersonalityConfig_Coward.asset";
        private const string PlayerHunterPath = "Assets/Development/Data/Configs/Enemies/EnemyPersonalityConfig_PlayerHunter.asset";
        private const string FolderData       = "Assets/Development/Data";
        private const string FolderConfigs    = "Assets/Development/Data/Configs";
        private const string FolderEnemies    = "Assets/Development/Data/Configs/Enemies";

        // ─────────────────────────────────────────────────────────────────────
        // MenuItem: Aggressor (создать / синхронизировать новые поля)
        // ─────────────────────────────────────────────────────────────────────

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
            // C#-дефолты класса уже задают профиль Aggressor; дополнительная настройка не нужна.
            var config = ScriptableObject.CreateInstance<EnemyPersonalityConfig>();
            AssetDatabase.CreateAsset(config, AssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Undo.RegisterCreatedObjectUndo(config, "Create EnemyPersonalityConfig Aggressor");

            Selection.activeObject = config;
            EditorGUIUtility.PingObject(config);

            Debug.Log("[Editor] Создан EnemyPersonalityConfig (дефолты Aggressor) по пути " + AssetPath);
        }

        // ─────────────────────────────────────────────────────────────────────
        // MenuItem: Coward
        // ─────────────────────────────────────────────────────────────────────

        [MenuItem("FlatOutLike/Configs/Create EnemyPersonalityConfig Coward")]
        public static void CreateCowardProfile()
        {
            // Coward: целится в самого слабого, трусливый (retreatHpThreshold=0.35), менее агрессивный
            CreateOrSyncDerivedProfile(
                targetPath:          CowardPath,
                profileName:         "EnemyPersonalityConfig_Coward",
                targetSelectionMode: 1,      // Weakest
                retreatHpThreshold:  0.35f,
                ramAggression:       0.6f
            );
        }

        // ─────────────────────────────────────────────────────────────────────
        // MenuItem: PlayerHunter
        // ─────────────────────────────────────────────────────────────────────

        [MenuItem("FlatOutLike/Configs/Create EnemyPersonalityConfig PlayerHunter")]
        public static void CreatePlayerHunterProfile()
        {
            // PlayerHunter: всегда преследует игрока, не отступает, максимально агрессивен
            CreateOrSyncDerivedProfile(
                targetPath:          PlayerHunterPath,
                profileName:         "EnemyPersonalityConfig_PlayerHunter",
                targetSelectionMode: 2,      // Player
                retreatHpThreshold:  0f,
                ramAggression:       1f
            );
        }

        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Создаёт производный профиль на базе Aggressor (через CopyAsset),
        /// если ассет ещё не существует, затем переопределяет только отличающиеся поля.
        /// Если ассет уже есть — только перезаписывает поля (GUID сохраняется).
        /// Null-safe: при отсутствующем поле пишет Warning, не бросает NRE.
        /// </summary>
        private static void CreateOrSyncDerivedProfile(
            string targetPath,
            string profileName,
            int    targetSelectionMode,
            float  retreatHpThreshold,
            float  ramAggression)
        {
            // Aggressor должен существовать — он источник для копирования
            var aggressorAsset = AssetDatabase.LoadAssetAtPath<EnemyPersonalityConfig>(AssetPath);
            if (aggressorAsset == null)
            {
                Debug.LogWarning("[Editor] EnemyPersonalityConfig_Aggressor не найден по " + AssetPath +
                                 ". Сначала запусти 'FlatOutLike/Configs/Create Default EnemyPersonalityConfig'.");
                return;
            }

            EnsureFolder("Assets/Development", "Data");
            EnsureFolder(FolderData,    "Configs");
            EnsureFolder(FolderConfigs, "Enemies");

            bool isNew = AssetDatabase.LoadAssetAtPath<EnemyPersonalityConfig>(targetPath) == null;

            if (isNew)
            {
                // Копируем Aggressor как базу — наследует весь отыгранный тюнинг движения
                bool copied = AssetDatabase.CopyAsset(AssetPath, targetPath);
                if (!copied)
                {
                    Debug.LogError("[Editor] Не удалось скопировать " + AssetPath + " → " + targetPath);
                    return;
                }
                AssetDatabase.Refresh();
                Debug.Log("[Editor] Скопирован Aggressor → " + targetPath + " как база для " + profileName + ".");
            }

            var profile = AssetDatabase.LoadAssetAtPath<EnemyPersonalityConfig>(targetPath);
            if (profile == null)
            {
                Debug.LogError("[Editor] Не удалось загрузить " + targetPath);
                return;
            }

            // ── Переопределяем только отличающиеся поля ──────────────────────
            var so = new SerializedObject(profile);

            var propMode = so.FindProperty("_targetSelectionMode");
            if (propMode != null)
                propMode.enumValueIndex = targetSelectionMode;
            else
                Debug.LogWarning("[Editor] Поле _targetSelectionMode не найдено на " + profileName);

            var propRetreat = so.FindProperty("_retreatHpThreshold");
            if (propRetreat != null)
                propRetreat.floatValue = retreatHpThreshold;
            else
                Debug.LogWarning("[Editor] Поле _retreatHpThreshold не найдено на " + profileName);

            var propAggression = so.FindProperty("_ramAggression");
            if (propAggression != null)
                propAggression.floatValue = ramAggression;
            else
                Debug.LogWarning("[Editor] Поле _ramAggression не найдено на " + profileName);

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();

            if (isNew)
                Undo.RegisterCreatedObjectUndo(profile, "Create " + profileName);

            Debug.Log($"[Editor] {(isNew ? "Создан" : "Обновлён")} {profileName}: " +
                      $"_targetSelectionMode={targetSelectionMode}, " +
                      $"_retreatHpThreshold={retreatHpThreshold}, " +
                      $"_ramAggression={ramAggression}.");
        }

        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Записывает C#-дефолты класса в поля тюнинга, добавленные уже после создания
        /// Aggressor.asset. Float-поля синхронизируются через floatValue, enum-поля —
        /// через enumValueIndex. Оба набора читаются до DestroyImmediate временного инстанса.
        /// Каждое поле защищено: null от FindProperty → Warning, без NRE.
        /// </summary>
        private static void SyncNewTuningFields(EnemyPersonalityConfig asset)
        {
            // Float-поля для синхронизации — обновлять при добавлении новых tuning-полей в класс
            var floatFieldNames = new[]
            {
                "_stuckTime", "_circleBreakTime", "_graceTime",
                "_ramAlignmentDot", "_minApproachSpeed", "_approachTime",
                "_obstacleAvoidDistance", "_obstacleSideAvoidDistance",
                "_recoverDuration"
            };

            // Enum-поля для синхронизации (для Aggressor: _targetSelectionMode = Nearest = 0)
            var enumFieldNames = new[] { "_targetSelectionMode" };

            // Поднимаем временный инстанс, чтобы прочитать C#-дефолты конструктора
            var defaults   = ScriptableObject.CreateInstance<EnemyPersonalityConfig>();
            var soDefaults = new SerializedObject(defaults);

            // Читаем float-дефолты
            var defaultFloatValues = new System.Collections.Generic.Dictionary<string, float>();
            foreach (var fieldName in floatFieldNames)
            {
                var propDefault = soDefaults.FindProperty(fieldName);
                if (propDefault == null)
                {
                    Debug.LogWarning($"[Editor] Поле {fieldName} не найдено на defaults, пропускаем");
                    continue;
                }
                defaultFloatValues[fieldName] = propDefault.floatValue;
            }

            // Читаем enum-дефолты ДО уничтожения временного инстанса
            var defaultEnumValues = new System.Collections.Generic.Dictionary<string, int>();
            foreach (var fieldName in enumFieldNames)
            {
                var propDefault = soDefaults.FindProperty(fieldName);
                if (propDefault == null)
                {
                    Debug.LogWarning($"[Editor] Поле {fieldName} не найдено на defaults, пропускаем");
                    continue;
                }
                defaultEnumValues[fieldName] = propDefault.enumValueIndex;
            }

            Object.DestroyImmediate(defaults);

            // Применяем к существующему ассету — GUID не трогается
            var soExisting = new SerializedObject(asset);

            foreach (var kvp in defaultFloatValues)
            {
                var propExisting = soExisting.FindProperty(kvp.Key);
                if (propExisting == null)
                {
                    Debug.LogWarning($"[Editor] Поле {kvp.Key} не найдено на существующем ассете, пропускаем");
                    continue;
                }
                propExisting.floatValue = kvp.Value;
            }

            foreach (var kvp in defaultEnumValues)
            {
                var propExisting = soExisting.FindProperty(kvp.Key);
                if (propExisting == null)
                {
                    Debug.LogWarning($"[Editor] Поле {kvp.Key} не найдено на существующем ассете, пропускаем");
                    continue;
                }
                propExisting.enumValueIndex = kvp.Value;
            }

            soExisting.ApplyModifiedProperties();
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();

            // Логируем итог синхронизации
            float stuckTime                 = defaultFloatValues.TryGetValue("_stuckTime",                 out var sv)  ? sv  : float.NaN;
            float circleBreakTime           = defaultFloatValues.TryGetValue("_circleBreakTime",           out var cv)  ? cv  : float.NaN;
            float graceTime                 = defaultFloatValues.TryGetValue("_graceTime",                 out var gv)  ? gv  : float.NaN;
            float ramAlignmentDot           = defaultFloatValues.TryGetValue("_ramAlignmentDot",           out var av)  ? av  : float.NaN;
            float minApproachSpeed          = defaultFloatValues.TryGetValue("_minApproachSpeed",          out var mv)  ? mv  : float.NaN;
            float approachTime              = defaultFloatValues.TryGetValue("_approachTime",              out var tv)  ? tv  : float.NaN;
            float obstacleAvoidDistance     = defaultFloatValues.TryGetValue("_obstacleAvoidDistance",     out var ov)  ? ov  : float.NaN;
            float obstacleSideAvoidDistance = defaultFloatValues.TryGetValue("_obstacleSideAvoidDistance", out var osv) ? osv : float.NaN;
            float recoverDuration           = defaultFloatValues.TryGetValue("_recoverDuration",           out var rv)  ? rv  : float.NaN;
            int   targetSelectionMode       = defaultEnumValues.TryGetValue("_targetSelectionMode",        out var em)  ? em  : -1;

            Debug.Log($"[Editor] Synced tuning fields on EnemyPersonalityConfig_Aggressor: " +
                      $"_stuckTime={stuckTime}, _circleBreakTime={circleBreakTime}, " +
                      $"_graceTime={graceTime}, _ramAlignmentDot={ramAlignmentDot}, " +
                      $"_minApproachSpeed={minApproachSpeed}, _approachTime={approachTime}, " +
                      $"_obstacleAvoidDistance={obstacleAvoidDistance}, " +
                      $"_obstacleSideAvoidDistance={obstacleSideAvoidDistance}, " +
                      $"_recoverDuration={recoverDuration}, " +
                      $"_targetSelectionMode={targetSelectionMode}");
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
