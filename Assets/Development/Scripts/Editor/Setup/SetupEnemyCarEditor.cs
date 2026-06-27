#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using FlatOutLike.Controllers.Car;
using FlatOutLike.Controllers.Enemies;
using FlatOutLike.Data.Configs.Car;
using FlatOutLike.Data.Configs.Enemies;
using FlatOutLike.Installers;

namespace FlatOutLike.Editor.Setup
{
    /// <summary>
    /// Спавнит машину-врага в активной тестовой сцене, добавляет EnemyCarController,
    /// заполняет все его SerializedField-ссылки и автоматически конвертирует материалы
    /// корпуса на damage-шейдер.
    ///
    /// Поиск машины игрока: первый CarController в сцене, у которого НЕТ соседнего
    /// EnemyCarController — это надёжно работает и для будущих multi-enemy раскладов.
    ///
    /// Идемпотентность: если EnemyCarController уже есть в сцене — обновляются только
    /// ссылки; новый GameObject не создаётся.
    ///
    /// Запускать ПОСЛЕ:
    ///   FlatOutLike/Setup/Build Driving Test Scene
    ///   FlatOutLike/Configs/Create Default EnemyPersonalityConfig
    ///
    /// Запускать ДО:
    ///   FlatOutLike/Setup/Fill GameLifetimeScope Refs (Step 4)
    /// </summary>
    public static class SetupEnemyCarEditor
    {
        private const string PersonalityConfigPath = "Assets/Development/Data/Configs/Enemies/EnemyPersonalityConfig_Aggressor.asset";
        private const string VehiclesPrefabsFolder = "Assets/Development/Prefabs/Vehicles";

        [MenuItem("FlatOutLike/Setup/Setup Enemy Car (Step 4)")]
        public static void SetupEnemyCar()
        {
            Undo.SetCurrentGroupName("Setup Enemy Car (Step 4)");
            int undoGroup = Undo.GetCurrentGroup();

            // ── 1. Проверяем, что в сцене есть GameLifetimeScope ──────────────
            var scope = Object.FindAnyObjectByType<GameLifetimeScope>();
            if (scope == null)
            {
                Debug.LogWarning("[Editor] GameLifetimeScope not found in scene. " +
                                 "Run 'FlatOutLike/Setup/Build Driving Test Scene' first.");
                return;
            }

            // ── 2. Ищем CarController игрока (без соседнего EnemyCarController) ──
            var playerCar = FindPlayerCar();
            if (playerCar == null)
            {
                Debug.LogWarning("[Editor] No player CarController found in scene. " +
                                 "Run 'FlatOutLike/Setup/Build Driving Test Scene' first.");
                return;
            }
            Debug.Log("[Editor] Found player car: " + playerCar.gameObject.name);

            // ── 3. Идемпотентность: EnemyCarController уже в сцене ────────────
            var existingEnemy = Object.FindAnyObjectByType<EnemyCarController>();
            if (existingEnemy != null)
            {
                Debug.Log("[Editor] EnemyCarController already in scene on '" +
                          existingEnemy.gameObject.name + "'. Refreshing refs only.");
                FillEnemyCarRefs(existingEnemy, playerCar);
                FinalizeScene();
                Undo.CollapseUndoOperations(undoGroup);
                Debug.Log("[Editor] Enemy car refs refreshed.");
                return;
            }

            // ── 4. Определяем prefab машины ───────────────────────────────────
            // Предпочитаем ровно тот же префаб, из которого был создан игрок.
            var prefabSource = PrefabUtility.GetCorrespondingObjectFromSource(playerCar.gameObject)
                               as GameObject;
            if (prefabSource == null)
            {
                Debug.Log("[Editor] Player car is not a plain prefab instance; " +
                          "falling back to Vehicles folder search.");
                prefabSource = FindFirstCarPrefab();
            }
            if (prefabSource == null)
            {
                Debug.LogWarning("[Editor] Could not find a car prefab in " + VehiclesPrefabsFolder +
                                 ". Run 'FlatOutLike/Setup/Build Car Prefab From Selected FBX' first.");
                return;
            }

            // ── 5. Спавним машину-врага ───────────────────────────────────────
            var enemyCarGo = (GameObject)PrefabUtility.InstantiatePrefab(prefabSource);
            Undo.RegisterCreatedObjectUndo(enemyCarGo, "Instantiate Enemy Car");
            enemyCarGo.name               = "EnemyCar";
            enemyCarGo.transform.position = new Vector3(0f, 1f, 25f);  // 25 м впереди игрока
            enemyCarGo.transform.rotation = Quaternion.Euler(0f, 180f, 0f); // лицом к игроку
            Debug.Log("[Editor] Instantiated EnemyCar from prefab '" + prefabSource.name +
                      "' at (0, 1, 25) facing player.");

            // ── 6a. Ставим _carId = 1 на CarController врага через SerializedObject ─
            var enemyCar = enemyCarGo.GetComponent<CarController>();
            if (enemyCar != null)
            {
                var carSo = new SerializedObject(enemyCar);
                carSo.FindProperty("_carId").intValue = 1;
                carSo.ApplyModifiedProperties();
                Debug.Log("[Editor] Set CarController._carId = 1 on EnemyCar.");
            }
            else
            {
                Debug.LogWarning("[Editor] No CarController found on EnemyCar root. " +
                                 "Check that the car prefab has CarController on its root.");
            }

            // ── 6b. Добавляем EnemyCarController ──────────────────────────────
            // Через Undo.AddComponent — чтобы добавление было откатываемым.
            var enemyController = enemyCarGo.GetComponent<EnemyCarController>();
            if (enemyController == null)
            {
                enemyController = Undo.AddComponent<EnemyCarController>(enemyCarGo);
                Debug.Log("[Editor] Added EnemyCarController to EnemyCar.");
            }
            else
            {
                Debug.Log("[Editor] EnemyCarController already present on EnemyCar, refreshing refs.");
            }

            // ── 6c. Заполняем SerializedField'ы EnemyCarController ────────────
            FillEnemyCarRefs(enemyController, playerCar);

            // ── 7. Конвертируем материалы damage-шейдера на машине врага ──────
            // Делегируем вынесенному public-методу, чтобы не дублировать логику
            // конвертации материалов и чтобы FindAnyObjectByType не нашёл не ту машину.
            if (enemyCar != null)
            {
                SetupCarDamageShaderEditor.ConvertCar(enemyCar);
                Debug.Log("[Editor] Damage shader conversion applied to EnemyCar.");
            }

            // ── 8. Сохраняем сцену ────────────────────────────────────────────
            FinalizeScene();

            Undo.CollapseUndoOperations(undoGroup);

            Debug.Log("[Editor] Enemy car setup complete. " +
                      "Next: run 'FlatOutLike/Setup/Fill GameLifetimeScope Refs (Step 4)' " +
                      "to register both cars in the DI container.");
        }

        // ─────────────────────────────────────────────────────────────────────
        /// <summary>
        /// Заполняет все SerializedField-ссылки на EnemyCarController.
        /// Читает _config и _rigidbody с соседнего CarController через SerializedObject,
        /// чтобы использовать ровно уже назначенные ссылки, а не свежий GetComponent.
        /// </summary>
        private static void FillEnemyCarRefs(EnemyCarController enemyController, CarController playerCar)
        {
            var enemyCarGo = enemyController.gameObject;
            var enemyCar   = enemyCarGo.GetComponent<CarController>();

            // Читаем _config и _rigidbody с CarController через SerializedObject —
            // они могут отличаться от результатов GetComponent, если у префаба есть overrides.
            CarStatsConfig statsConfig = null;
            Rigidbody      rigidbody   = null;

            if (enemyCar != null)
            {
                var carSo   = new SerializedObject(enemyCar);
                statsConfig = carSo.FindProperty("_config").objectReferenceValue    as CarStatsConfig;
                rigidbody   = carSo.FindProperty("_rigidbody").objectReferenceValue as Rigidbody;
            }

            // Fallback, если поля SO ещё не привязаны (свежий инстанс префаба)
            if (rigidbody == null)
                rigidbody = enemyCarGo.GetComponent<Rigidbody>();

            var profile = AssetDatabase.LoadAssetAtPath<EnemyPersonalityConfig>(PersonalityConfigPath);
            if (profile == null)
                Debug.LogWarning("[Editor] EnemyPersonalityConfig_Aggressor not found at " +
                                 PersonalityConfigPath +
                                 ". Run 'FlatOutLike/Configs/Create Default EnemyPersonalityConfig' first. " +
                                 "_profile will remain null.");

            var so = new SerializedObject(enemyController);
            so.FindProperty("_car").objectReferenceValue         = enemyCar;
            so.FindProperty("_target").objectReferenceValue      = playerCar;
            so.FindProperty("_statsConfig").objectReferenceValue = statsConfig;
            so.FindProperty("_profile").objectReferenceValue     = profile;
            so.FindProperty("_rigidbody").objectReferenceValue   = rigidbody;
            so.ApplyModifiedProperties();

            Debug.Log("[Editor] EnemyCarController refs filled on '" + enemyCarGo.name + "'" +
                      " — _car="        + (enemyCar    != null ? enemyCar.name    : "NULL") +
                      ", _target="      + (playerCar   != null ? playerCar.name   : "NULL") +
                      ", _statsConfig=" + (statsConfig != null ? statsConfig.name : "NULL") +
                      ", _profile="     + (profile     != null ? profile.name     : "NULL") +
                      ", _rigidbody="   + (rigidbody   != null ? rigidbody.name   : "NULL") + ".");
        }

        /// <summary>
        /// Возвращает первый CarController в сцене, у которого НЕТ компонента
        /// EnemyCarController на том же GameObject — это и есть игрок.
        /// </summary>
        private static CarController FindPlayerCar()
        {
            var allCars = Object.FindObjectsByType<CarController>(FindObjectsSortMode.None);
            foreach (var car in allCars)
            {
                if (car.GetComponent<EnemyCarController>() == null)
                    return car;
            }
            return null;
        }

        /// <summary>
        /// Fallback-поиск префаба: находит первый Car_*-префаб в папке Vehicles.
        /// Повторяет логику BuildDrivingTestSceneEditor.FindFirstCarPrefab().
        /// </summary>
        private static GameObject FindFirstCarPrefab()
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { VehiclesPrefabsFolder });
            foreach (string guid in guids)
            {
                string path     = AssetDatabase.GUIDToAssetPath(guid);
                string fileName = System.IO.Path.GetFileNameWithoutExtension(path);
                if (fileName.StartsWith("Car_", System.StringComparison.OrdinalIgnoreCase))
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab != null)
                    {
                        Debug.Log("[Editor] Fallback prefab search: found " + path);
                        return prefab;
                    }
                }
            }
            return null;
        }

        private static void FinalizeScene()
        {
            var activeScene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
            Debug.Log("[Editor] Scene saved.");
        }
    }
}
#endif
