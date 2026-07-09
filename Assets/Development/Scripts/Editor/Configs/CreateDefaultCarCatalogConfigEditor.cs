#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using FlatOutLike.Data.Configs.Car;
using FlatOutLike.Editor.Setup;

namespace FlatOutLike.Editor.Configs
{
    /// <summary>
    /// Создаёт CarCatalogConfig_Default.asset (Step 8a: Meta-экономика) с тремя машинами:
    /// уже существующий Car_Sport_09 (стартовая, бесплатная) + две новые машины,
    /// собранные из пака "Low Poly Destructible Cars" через существующий конвейер
    /// BuildCarPrefabFromFbxEditor.BuildCarPrefabFromSelected() (выбор ассета через
    /// Selection.activeObject — единственный публичный вход в этот скрипт).
    ///
    /// BuildCarPrefabFromFbxEditor собирает только "голый" каркас машины (Rigidbody +
    /// колёса + CarController на слое Default). Этот скрипт довозит новый префаб до
    /// полной комплектации Sport_09 — той же цепочкой шагов, что применялась к нему:
    ///   1. Слой Vehicle рекурсивно на всю иерархию.
    ///   2. Damage-шейдер на материалах корпуса (SetupCarDamageShaderEditor.ConvertCar).
    ///   3. CarDamageVisualController + ссылки (SetupCarDamageVisualEditor.SetupCarDamageVisualOnPrefab).
    ///   4. Windshield (SetupWindshieldShatterEditor — обрабатывает все обнаруженные машины).
    ///   5. Водитель, ПОСЛЕДНИМ шагом (AttachDriverToCarPrefabsEditor — аналогично).
    ///
    /// Идемпотентность: существующие entries (по _id) и существующие prefab/config
    /// ассеты не пересоздаются — только дозаполняются недостающие. Шаги 1-3 выполняются
    /// на КАЖДОМ прогоне для Car_Tank_17/Car_Sport_05 (даже если prefab уже существует),
    /// чтобы префаб, собранный ранее лишь частично, дособирался, а не пересоздавался.
    ///
    /// Запускать ПОСЛЕ:
    ///   FlatOutLike/Configs/Create Default CarStatsConfig
    /// (Car_Sport_09.prefab и CarStatsConfig_Default.asset должны уже существовать).
    /// </summary>
    public static class CreateDefaultCarCatalogConfigEditor
    {
        private const string AssetPath      = "Assets/Development/Data/Configs/Car/CarCatalogConfig_Default.asset";
        private const string FolderData     = "Assets/Development/Data";
        private const string FolderCfg      = "Assets/Development/Data/Configs";
        private const string FolderCar      = "Assets/Development/Data/Configs/Car";
        private const string VehiclesRoot   = "Assets/Development/Prefabs/Vehicles";

        private const string DefaultStatsPath = "Assets/Development/Data/Configs/Car/CarStatsConfig_Default.asset";
        private const string HeavyStatsPath   = "Assets/Development/Data/Configs/Car/CarStatsConfig_Heavy.asset";
        private const string LightStatsPath   = "Assets/Development/Data/Configs/Car/CarStatsConfig_Light.asset";

        // Источники в ThirdParty-паке (содержимое пака не редактируется — только читается/копируется как есть).
        private const string ThirdPartyTankSource  = "Assets/ThirdParty/Low Poly Destructible Cars/Prefabs/Tank_17.prefab";
        private const string ThirdPartySportSource = "Assets/ThirdParty/Low Poly Destructible Cars/Prefabs/Sport_05.prefab";

        private const string HeavyPrefabPath = VehiclesRoot + "/Car_Tank_17.prefab";
        private const string LightPrefabPath = VehiclesRoot + "/Car_Sport_05.prefab";
        private const string StartPrefabPath = VehiclesRoot + "/Car_Sport_09.prefab";

        private struct EntryDef
        {
            public string Id;
            public string DisplayName;
            public string StatsPath;
            public int Price;
            public string PrefabPath;
        }

        [MenuItem("FlatOutLike/Configs/Create Default CarCatalogConfig")]
        public static void CreateDefaultCarCatalogConfig()
        {
            Undo.SetCurrentGroupName("Create Default CarCatalogConfig");
            int undoGroup = Undo.GetCurrentGroup();

            EnsureFolder("Assets/Development", "Data");
            EnsureFolder(FolderData,           "Configs");
            EnsureFolder(FolderCfg,            "Car");

            // ── 1. Проверяем стартовую машину и её конфиг ──────────────────────
            if (AssetDatabase.LoadAssetAtPath<CarStatsConfig>(DefaultStatsPath) == null)
            {
                Debug.LogWarning("[Editor] CarStatsConfig_Default не найден по " + DefaultStatsPath +
                                  ". Запусти 'FlatOutLike/Configs/Create Default CarStatsConfig' сначала.");
            }
            if (AssetDatabase.LoadAssetAtPath<GameObject>(StartPrefabPath) == null)
            {
                Debug.LogWarning("[Editor] " + StartPrefabPath + " не найден. Ожидается, что стартовая машина " +
                                  "уже собрана (Step 5). CarCatalogConfig всё равно будет создан, но с пустой " +
                                  "ссылкой на prefab для этой записи.");
            }

            // ── 2. Создаём варианты CarStatsConfig для новых машин ─────────────
            var heavyStats = EnsureStatsVariant(HeavyStatsPath, isHeavy: true);
            var lightStats = EnsureStatsVariant(LightStatsPath, isHeavy: false);

            // ── 3. Собираем/дособираем prefab'ы через существующий конвейер ────
            EnsureCarPrefabBuilt(ThirdPartyTankSource, HeavyPrefabPath, heavyStats);
            EnsureCarPrefabBuilt(ThirdPartySportSource, LightPrefabPath, lightStats);

            // ── 3.5. Общие для всех машин шаги — windshield и водитель ─────────
            // Оба скрипта сами обнаруживают ВСЕ префабы в Vehicles/ (см. CarPrefabDiscovery),
            // поэтому достаточно вызвать их один раз после того, как новые префабы собраны.
            // Порядок как в оригинальной цепочке Sport_09: windshield, водитель — последним.
            SetupWindshieldShatterEditor.SetupWindshieldShatter();
            Debug.Log("[Editor] Windshield Shatter дособран для всех обнаруженных машин.");

            AttachDriverToCarPrefabsEditor.AttachDriverToCarPrefabs();
            Debug.Log("[Editor] Водитель дособран для всех обнаруженных машин.");

            // ── 4. Создаём/дозаполняем CarCatalogConfig ────────────────────────
            var config = AssetDatabase.LoadAssetAtPath<CarCatalogConfig>(AssetPath);
            bool isNewAsset = config == null;
            if (isNewAsset)
            {
                config = ScriptableObject.CreateInstance<CarCatalogConfig>();
                AssetDatabase.CreateAsset(config, AssetPath);
                Undo.RegisterCreatedObjectUndo(config, "Create CarCatalogConfig_Default");
                Debug.Log("[Editor] Создан CarCatalogConfig_Default по " + AssetPath);
            }
            else
            {
                Debug.Log("[Editor] CarCatalogConfig_Default уже существует — дозаполняем недостающие entries.");
            }

            var entries = new[]
            {
                new EntryDef
                {
                    Id = "car_sport_09", DisplayName = "Sport 09",
                    StatsPath = DefaultStatsPath, Price = 0, PrefabPath = StartPrefabPath,
                },
                new EntryDef
                {
                    Id = "car_tank_17", DisplayName = "Tank 17",
                    StatsPath = HeavyStatsPath, Price = 500, PrefabPath = HeavyPrefabPath,
                },
                new EntryDef
                {
                    Id = "car_sport_05", DisplayName = "Sport 05",
                    StatsPath = LightStatsPath, Price = 1200, PrefabPath = LightPrefabPath,
                },
            };

            var so = new SerializedObject(config);
            var entriesProp = so.FindProperty("_entries");
            if (entriesProp == null)
            {
                Debug.LogWarning("[Editor] Поле CarCatalogConfig._entries не найдено. " +
                                  "Возможно, рантайм Step 8a ещё не скомпилировался.");
            }
            else
            {
                foreach (var def in entries)
                {
                    int existingIndex = FindEntryIndexById(entriesProp, def.Id);
                    if (existingIndex >= 0)
                    {
                        Debug.Log("[Editor] Entry '" + def.Id + "' уже существует — пропускаем.");
                        continue;
                    }

                    int newIndex = entriesProp.arraySize;
                    entriesProp.arraySize++;
                    var entry = entriesProp.GetArrayElementAtIndex(newIndex);

                    entry.FindPropertyRelative("_id").stringValue          = def.Id;
                    entry.FindPropertyRelative("_displayName").stringValue = def.DisplayName;
                    entry.FindPropertyRelative("_price").intValue          = def.Price;

                    var statsAsset = AssetDatabase.LoadAssetAtPath<CarStatsConfig>(def.StatsPath);
                    if (statsAsset != null)
                        entry.FindPropertyRelative("_stats").objectReferenceValue = statsAsset;
                    else
                        Debug.LogWarning("[Editor] CarStatsConfig не найден по " + def.StatsPath +
                                          " для entry '" + def.Id + "'. Поле _stats оставлено пустым.");

                    var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(def.PrefabPath);
                    if (prefabAsset != null)
                        entry.FindPropertyRelative("_prefab").objectReferenceValue = prefabAsset;
                    else
                        Debug.LogWarning("[Editor] Prefab не найден по " + def.PrefabPath +
                                          " для entry '" + def.Id + "'. Поле _prefab оставлено пустым.");

                    // _icon намеренно не прошиваем — иконка добавляется художником вручную.

                    Debug.Log("[Editor] Добавлена entry '" + def.Id + "' (price=" + def.Price + ").");
                }

                so.ApplyModifiedProperties();
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = config;
            EditorGUIUtility.PingObject(config);

            Undo.CollapseUndoOperations(undoGroup);

            Debug.Log("[Editor] Create Default CarCatalogConfig завершён.");
        }

        private static int FindEntryIndexById(SerializedProperty entriesProp, string id)
        {
            for (int i = 0; i < entriesProp.arraySize; i++)
            {
                var idProp = entriesProp.GetArrayElementAtIndex(i).FindPropertyRelative("_id");
                if (idProp != null && idProp.stringValue == id)
                    return i;
            }
            return -1;
        }

        private static CarStatsConfig EnsureStatsVariant(string path, bool isHeavy)
        {
            var existing = AssetDatabase.LoadAssetAtPath<CarStatsConfig>(path);
            if (existing != null)
            {
                Debug.Log("[Editor] " + path + " уже существует — пропускаем.");
                return existing;
            }

            var defaultConfig = AssetDatabase.LoadAssetAtPath<CarStatsConfig>(DefaultStatsPath);
            if (defaultConfig == null)
            {
                Debug.LogWarning("[Editor] CarStatsConfig_Default не найден — не могу создать вариант " + path +
                                  ". Запусти 'FlatOutLike/Configs/Create Default CarStatsConfig' сначала.");
                return null;
            }

            if (!AssetDatabase.CopyAsset(DefaultStatsPath, path))
            {
                Debug.LogWarning("[Editor] Не удалось скопировать " + DefaultStatsPath + " -> " + path);
                return null;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var variant = AssetDatabase.LoadAssetAtPath<CarStatsConfig>(path);
            var so = new SerializedObject(variant);

            void SetIfPresent(string field, float value)
            {
                var prop = so.FindProperty(field);
                if (prop != null) prop.floatValue = value;
            }

            if (isHeavy)
            {
                // Тяжёлая/бронированная: больше брони и массы, ниже скорость и разгон.
                SetIfPresent("_maxArmor",  180f);
                SetIfPresent("_mass",      1600f);
                SetIfPresent("_topSpeed",  22f);
                SetIfPresent("_acceleration", 2800f);
            }
            else
            {
                // Лёгкая/быстрая: меньше брони и массы, выше скорость и разгон.
                SetIfPresent("_maxArmor",  60f);
                SetIfPresent("_mass",      900f);
                SetIfPresent("_topSpeed",  40f);
                SetIfPresent("_acceleration", 4200f);
            }

            so.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();

            Undo.RegisterCreatedObjectUndo(variant, "Create " + System.IO.Path.GetFileNameWithoutExtension(path));
            Debug.Log("[Editor] Создан вариант CarStatsConfig: " + path + " (isHeavy=" + isHeavy + ").");

            return variant;
        }

        private static void EnsureCarPrefabBuilt(string sourceAssetPath, string targetPrefabPath, CarStatsConfig statsForVariant)
        {
            bool alreadyExists = AssetDatabase.LoadAssetAtPath<GameObject>(targetPrefabPath) != null;

            if (!alreadyExists)
            {
                var sourceAsset = AssetDatabase.LoadAssetAtPath<GameObject>(sourceAssetPath);
                if (sourceAsset == null)
                {
                    Debug.LogWarning("[Editor] Источник " + sourceAssetPath + " не найден в ThirdParty-паке. " +
                                      "Prefab " + targetPrefabPath + " не собран.");
                    return;
                }

                // Переиспользуем существующий конвейер сборки машины из ThirdParty-FBX/prefab —
                // он валидирует selection сам, поэтому просто подставляем selection и вызываем его.
                var previousSelection = Selection.activeObject;
                Selection.activeObject = sourceAsset;
                try
                {
                    BuildCarPrefabFromFbxEditor.BuildCarPrefabFromSelected();
                }
                finally
                {
                    Selection.activeObject = previousSelection;
                }

                if (AssetDatabase.LoadAssetAtPath<GameObject>(targetPrefabPath) == null)
                {
                    Debug.LogWarning("[Editor] Prefab " + targetPrefabPath + " не найден после сборки. " +
                                      "Пайплайн комплектации пропущен.");
                    return;
                }

                Debug.Log("[Editor] Собран каркас " + targetPrefabPath + " (BuildCarPrefabFromFbxEditor).");
            }
            else
            {
                Debug.Log("[Editor] " + targetPrefabPath + " уже существует — пропускаем сборку каркаса, " +
                          "дособираем недостающие шаги комплектации.");
            }

            // Дособираем префаб до полной комплектации Sport_09: слой, damage-шейдер,
            // damage-визуал, переключение на вариантный CarStatsConfig. Выполняется на КАЖДОМ
            // прогоне, а не только при первой сборке, — чтобы частично собранный на прошлом
            // прогоне префаб дособрался, а не остался неполным. Сама цепочка вынесена в
            // CompleteCarPrefabEditor — она же доступна руками через меню
            // 'FlatOutLike/Setup/Complete Car Prefab From Selected' для любых новых машин.
            CompleteCarPrefabEditor.CompletePrefab(targetPrefabPath, statsForVariant);
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
