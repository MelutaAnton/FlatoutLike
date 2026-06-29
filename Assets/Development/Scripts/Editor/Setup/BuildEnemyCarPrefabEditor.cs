#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using FlatOutLike.Controllers.Car;
using FlatOutLike.Controllers.Enemies;
using FlatOutLike.Data.Configs.AI;
using FlatOutLike.Data.Configs.Car;
using FlatOutLike.Data.Configs.Enemies;
using FlatOutLike.Models;

namespace FlatOutLike.Editor.Setup
{
    /// <summary>
    /// Создаёт или обновляет Assets/Development/Prefabs/Vehicles/Car_Enemy.prefab:
    /// копирует Car_Sport_09 как основу, добавляет EnemyCarController, заполняет все
    /// его SerializedField-ссылки и назначает слой Vehicle рекурсивно.
    ///
    /// Переиспользует SetupCarDamageShaderEditor.ConvertCar для конвертации материалов —
    /// логика конвертации не дублируется.
    ///
    /// Примечание об Undo в prefab-контексте: операции внутри LoadPrefabContents
    /// используют preview-сцену, поэтому Undo.RecordObject не сохраняет историю.
    /// Сами данные (компоненты, материалы, ссылки) корректно сохраняются через
    /// PrefabUtility.SaveAsPrefabAsset.
    ///
    /// Идемпотентность: если Car_Enemy.prefab уже существует — обновляются только
    ///                  ссылки и слой; структура не пересоздаётся.
    ///
    /// Запускать ПОСЛЕ:
    ///   FlatOutLike/Setup/Create Physics Layers
    ///   FlatOutLike/Configs/Create Default AISensorConfig
    ///   FlatOutLike/Configs/Create Default EnemyPersonalityConfig
    /// </summary>
    public static class BuildEnemyCarPrefabEditor
    {
        private const string EnemyPrefabPath  = "Assets/Development/Prefabs/Vehicles/Car_Enemy.prefab";
        private const string PlayerPrefabPath = "Assets/Development/Prefabs/Vehicles/Car_Sport_09.prefab";
        private const string SensorConfigPath = "Assets/Development/Data/Configs/AI/AISensorConfig.asset";
        private const string PersonalityPath  = "Assets/Development/Data/Configs/Enemies/EnemyPersonalityConfig_Aggressor.asset";
        private const string CarStatsPath     = "Assets/Development/Data/Configs/Car/CarStatsConfig_Default.asset";

        [MenuItem("FlatOutLike/Setup/Build Enemy Car Prefab")]
        public static void BuildEnemyCarPrefab()
        {
            // ── 1. Если Car_Enemy.prefab отсутствует — копируем из Car_Sport_09 ──
            string fullEnemyPath = System.IO.Path.Combine(
                System.IO.Directory.GetCurrentDirectory(), EnemyPrefabPath);

            if (!System.IO.File.Exists(fullEnemyPath))
            {
                string fullPlayerPath = System.IO.Path.Combine(
                    System.IO.Directory.GetCurrentDirectory(), PlayerPrefabPath);

                if (!System.IO.File.Exists(fullPlayerPath))
                {
                    Debug.LogWarning("[Editor] Car_Sport_09.prefab не найден по пути " + PlayerPrefabPath +
                                     ". Сначала создай игровой префаб машины через " +
                                     "'FlatOutLike/Setup/Build Car Prefab From Selected FBX'.");
                    return;
                }

                bool copied = AssetDatabase.CopyAsset(PlayerPrefabPath, EnemyPrefabPath);
                if (!copied)
                {
                    Debug.LogError("[Editor] Не удалось скопировать Car_Sport_09 → Car_Enemy. " +
                                   "Проверь права на запись в папку Prefabs/Vehicles.");
                    return;
                }

                AssetDatabase.Refresh();
                Debug.Log("[Editor] Создан Car_Enemy.prefab на основе Car_Sport_09.prefab.");
            }
            else
            {
                Debug.Log("[Editor] Car_Enemy.prefab уже существует — обновляем компоненты и ссылки.");
            }

            // ── 2. Загружаем prefab-contents в изолированную среду ────────────
            // LoadPrefabContents создаёт временную preview-сцену; все изменения
            // внутри блока try сохраняются через SaveAsPrefabAsset в finally.
            var prefabRoot = PrefabUtility.LoadPrefabContents(EnemyPrefabPath);
            if (prefabRoot == null)
            {
                Debug.LogError("[Editor] Не удалось загрузить содержимое " + EnemyPrefabPath);
                return;
            }

            try
            {
                // ── 3. Добавляем EnemyCarController, если его нет ────────────
                var enemyController = prefabRoot.GetComponent<EnemyCarController>();
                if (enemyController == null)
                {
                    // В prefab-contents контексте используем AddComponent напрямую
                    // (Undo здесь не применим — preview-сцена уничтожается при Unload).
                    enemyController = prefabRoot.AddComponent<EnemyCarController>();
                    Debug.Log("[Editor] Добавлен EnemyCarController на Car_Enemy.prefab.");
                }
                else
                {
                    Debug.Log("[Editor] EnemyCarController уже есть на Car_Enemy.prefab — обновляем ссылки.");
                }

                // ── 4. Читаем соседние компоненты с того же GameObject ────────
                var carController = prefabRoot.GetComponent<CarController>();
                if (carController == null)
                    Debug.LogWarning("[Editor] CarController не найден на корне Car_Enemy.prefab. " +
                                     "Убедись, что базовый префаб содержит CarController на корневом объекте.");

                // Rigidbody читаем через SerializedObject CarController, как в SetupEnemyCarEditor —
                // это гарантирует ровно ту же ссылку, что уже назначена в CarController._rigidbody.
                Rigidbody rb = null;
                if (carController != null)
                {
                    var carSo = new SerializedObject(carController);
                    rb = carSo.FindProperty("_rigidbody").objectReferenceValue as Rigidbody;
                }
                // Fallback: ищем GetComponent, если CarController._rigidbody ещё не назначен
                if (rb == null)
                    rb = prefabRoot.GetComponent<Rigidbody>();
                if (rb == null)
                    Debug.LogWarning("[Editor] Rigidbody не найден на Car_Enemy.prefab. " +
                                     "Ссылка EnemyCarController._rigidbody будет null.");

                // ── 5. Загружаем конфиги из ассетов ─────────────────────────
                var sensorConfig = AssetDatabase.LoadAssetAtPath<AISensorConfig>(SensorConfigPath);
                if (sensorConfig == null)
                    Debug.LogWarning("[Editor] AISensorConfig не найден по " + SensorConfigPath +
                                     ". Запусти 'FlatOutLike/Configs/Create Default AISensorConfig' сначала.");

                var personality = AssetDatabase.LoadAssetAtPath<EnemyPersonalityConfig>(PersonalityPath);
                if (personality == null)
                    Debug.LogWarning("[Editor] EnemyPersonalityConfig_Aggressor не найден по " + PersonalityPath +
                                     ". Запусти 'FlatOutLike/Configs/Create Default EnemyPersonalityConfig' сначала.");

                var carStats = AssetDatabase.LoadAssetAtPath<CarStatsConfig>(CarStatsPath);
                if (carStats == null)
                    Debug.LogWarning("[Editor] CarStatsConfig_Default не найден по " + CarStatsPath);

                // ── 6. Заполняем SerializedField-ссылки EnemyCarController ───
                // _target намеренно оставляем null: в рантайме CarFactory вызывает
                // enemyController.Configure(profile, stats, playerController), что
                // задаёт цель до InjectGameObject → до Construct.
                var so = new SerializedObject(enemyController);
                so.FindProperty("_car").objectReferenceValue          = carController;
                so.FindProperty("_rigidbody").objectReferenceValue    = rb;
                so.FindProperty("_sensorConfig").objectReferenceValue = sensorConfig;
                so.FindProperty("_profile").objectReferenceValue      = personality;
                so.FindProperty("_statsConfig").objectReferenceValue  = carStats;
                so.ApplyModifiedProperties();

                Debug.Log("[Editor] Car_Enemy.prefab: refs заполнены — " +
                          "_car="          + (carController != null ? carController.name : "NULL") +
                          ", _rigidbody="  + (rb            != null ? rb.name            : "NULL") +
                          ", _sensorConfig=" + (sensorConfig != null ? sensorConfig.name : "NULL") +
                          ", _profile="    + (personality   != null ? personality.name   : "NULL") +
                          ", _statsConfig=" + (carStats     != null ? carStats.name      : "NULL") + ".");

                // ── 7. Назначаем слой Vehicle рекурсивно ─────────────────────
                int vehicleLayer = LayerMask.NameToLayer(Layers.Vehicle);
                if (vehicleLayer >= 0)
                {
                    SetLayerRecursively(prefabRoot, vehicleLayer);
                    Debug.Log("[Editor] Слой '" + Layers.Vehicle + "' назначен Car_Enemy.prefab рекурсивно.");
                }
                else
                {
                    Debug.LogWarning("[Editor] Слой '" + Layers.Vehicle + "' не зарегистрирован. " +
                                     "Запусти 'FlatOutLike/Setup/Create Physics Layers' сначала — " +
                                     "без этого слоя AI-сенсоры не отфильтруют машины врагов.");
                }

                // ── 8. Конвертируем материалы кузова на damage-шейдер ────────
                // Делегируем SetupCarDamageShaderEditor.ConvertCar — логика не дублируется.
                // В prefab-contents контексте Undo.RecordObject внутри ConvertCar — no-op,
                // но назначение материалов корректно сохранится через SaveAsPrefabAsset ниже.
                if (carController != null)
                {
                    SetupCarDamageShaderEditor.ConvertCar(carController);
                    Debug.Log("[Editor] Damage-шейдер конвертирован на Car_Enemy.prefab.");
                }

                // ── 9. Сохраняем prefab-contents обратно в ассет ─────────────
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, EnemyPrefabPath);
                Debug.Log("[Editor] Car_Enemy.prefab сохранён: " + EnemyPrefabPath);
            }
            finally
            {
                // UnloadPrefabContents ОБЯЗАТЕЛЕН в finally — уничтожает preview-сцену
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }

            AssetDatabase.Refresh();

            Debug.Log("[Editor] Build Enemy Car Prefab завершён. " +
                      "Следующий шаг: FlatOutLike/Scenes/Build Derby Arena.");
        }

        // ─────────────────────────────────────────────────────────────────────
        /// <summary>
        /// Рекурсивно назначает <paramref name="layer"/> объекту и всей его иерархии.
        /// Аналог логики в CreatePhysicsLayersEditor (там метод private).
        /// </summary>
        private static void SetLayerRecursively(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform)
                SetLayerRecursively(child.gameObject, layer);
        }
    }
}
#endif
