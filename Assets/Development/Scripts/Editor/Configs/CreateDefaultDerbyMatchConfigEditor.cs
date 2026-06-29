#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using FlatOutLike.Controllers.Camera;
using FlatOutLike.Data.Configs.Car;
using FlatOutLike.Data.Configs.Combat;
using FlatOutLike.Data.Configs.Enemies;
using FlatOutLike.Data.Configs.Match;
using FlatOutLike.Data.Configs.Vfx;
using FlatOutLike.Installers;

namespace FlatOutLike.Editor.Configs
{
    /// <summary>
    /// Создаёт или обновляет ассет DerbyMatchConfig и проводит его в GameLifetimeScope
    /// активной Derby-сцены вместе со SpawnPoints.
    ///
    /// Что делает:
    ///   1. Создаёт (идемпотентно) Assets/Development/Data/Configs/Match/DerbyMatchConfig.asset.
    ///   2. Заполняет поля: _playerCarPrefab = Car_Sport_09, _playerStats = CarStatsConfig_Default,
    ///      _enemies[3] = { carPrefab=Car_Enemy, stats=Default } с профилями:
    ///        entry[0] = Aggressor, entry[1] = Coward, entry[2] = PlayerHunter.
    ///      _enemyCarPrefab на уровне конфига ОТСУТСТВУЕТ — prefab хранится внутри каждого entry.
    ///   3. Находит в активной сцене GameLifetimeScope и проставляет:
    ///        _derbyMatchConfig → новый ассет
    ///        _spawnPoints      → Transform'ы SpawnPoint_0..6 (из объекта SpawnPoints)
    ///        _damageConfig     → DamageConfig.asset
    ///        _vfxConfig        → VfxConfig.asset
    ///        _chaseCamera      → ChaseCarCameraBehaviour из активной сцены
    ///        _sceneCars        → очищает (арена использует фабрику, не ручной спавн)
    ///   4. ApplyModifiedProperties + MarkSceneDirty + SaveScene.
    ///
    /// Если scope или SpawnPoints не найдены — LogWarning без краша.
    ///
    /// Запускать ПОСЛЕ:
    ///   FlatOutLike/Scenes/Build Derby Arena
    ///   FlatOutLike/Spawn/Populate Derby Arena Spawn Points
    ///   FlatOutLike/Configs/Create Default DamageConfig
    ///   FlatOutLike/Configs/Create Default VfxConfig
    ///   FlatOutLike/Setup/Build Enemy Car Prefab
    /// </summary>
    public static class CreateDefaultDerbyMatchConfigEditor
    {
        private const string AssetPath         = "Assets/Development/Data/Configs/Match/DerbyMatchConfig.asset";
        private const string FolderData        = "Assets/Development/Data";
        private const string FolderConfigs     = "Assets/Development/Data/Configs";
        private const string FolderMatch       = "Assets/Development/Data/Configs/Match";

        private const string PlayerPrefabPath  = "Assets/Development/Prefabs/Vehicles/Car_Sport_09.prefab";
        private const string EnemyPrefabPath   = "Assets/Development/Prefabs/Vehicles/Car_Enemy.prefab";
        private const string CarStatsPath      = "Assets/Development/Data/Configs/Car/CarStatsConfig_Default.asset";
        private const string AggressorPath     = "Assets/Development/Data/Configs/Enemies/EnemyPersonalityConfig_Aggressor.asset";
        private const string CowardPath        = "Assets/Development/Data/Configs/Enemies/EnemyPersonalityConfig_Coward.asset";
        private const string PlayerHunterPath  = "Assets/Development/Data/Configs/Enemies/EnemyPersonalityConfig_PlayerHunter.asset";
        private const string DamageConfigPath  = "Assets/Development/Data/Configs/Combat/DamageConfig.asset";
        private const string VfxConfigPath     = "Assets/Development/Data/Configs/Vfx/VfxConfig.asset";

        private const int EnemyCount = 3;

        [MenuItem("FlatOutLike/Configs/Create Default DerbyMatchConfig")]
        public static void CreateDefaultDerbyMatchConfig()
        {
            Undo.SetCurrentGroupName("Create Default DerbyMatchConfig");
            int undoGroup = Undo.GetCurrentGroup();

            // ── 1. Убеждаемся, что иерархия папок существует ─────────────────
            EnsureFolder("Assets/Development",       "Data");
            EnsureFolder(FolderData,                 "Configs");
            EnsureFolder(FolderConfigs,              "Match");

            // ── 2. Создаём или загружаем ассет ───────────────────────────────
            var config = AssetDatabase.LoadAssetAtPath<DerbyMatchConfig>(AssetPath);
            bool isNew = config == null;

            if (isNew)
            {
                config = ScriptableObject.CreateInstance<DerbyMatchConfig>();
                AssetDatabase.CreateAsset(config, AssetPath);
                AssetDatabase.SaveAssets();
                Undo.RegisterCreatedObjectUndo(config, "Create DerbyMatchConfig");
                Debug.Log("[Editor] Создан DerbyMatchConfig.asset по пути " + AssetPath);
            }
            else
            {
                Debug.Log("[Editor] DerbyMatchConfig.asset уже существует — обновляем поля.");
            }

            // ── 3. Загружаем ассеты для ссылок ───────────────────────────────
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (playerPrefab == null)
                Debug.LogWarning("[Editor] Car_Sport_09.prefab не найден по " + PlayerPrefabPath);

            var enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefabPath);
            if (enemyPrefab == null)
                Debug.LogWarning("[Editor] Car_Enemy.prefab не найден по " + EnemyPrefabPath +
                                 ". Запусти 'FlatOutLike/Setup/Build Enemy Car Prefab' сначала.");

            var carStats = AssetDatabase.LoadAssetAtPath<CarStatsConfig>(CarStatsPath);
            if (carStats == null)
                Debug.LogWarning("[Editor] CarStatsConfig_Default не найден по " + CarStatsPath);

            var aggressorProfile = AssetDatabase.LoadAssetAtPath<EnemyPersonalityConfig>(AggressorPath);
            if (aggressorProfile == null)
                Debug.LogWarning("[Editor] EnemyPersonalityConfig_Aggressor не найден по " + AggressorPath +
                                 ". Запусти 'FlatOutLike/Configs/Create Default EnemyPersonalityConfig' сначала.");

            var cowardProfile = AssetDatabase.LoadAssetAtPath<EnemyPersonalityConfig>(CowardPath);
            if (cowardProfile == null)
                Debug.LogWarning("[Editor] EnemyPersonalityConfig_Coward не найден по " + CowardPath +
                                 ". Запусти 'FlatOutLike/Configs/Create EnemyPersonalityConfig Coward' сначала.");

            var playerHunterProfile = AssetDatabase.LoadAssetAtPath<EnemyPersonalityConfig>(PlayerHunterPath);
            if (playerHunterProfile == null)
                Debug.LogWarning("[Editor] EnemyPersonalityConfig_PlayerHunter не найден по " + PlayerHunterPath +
                                 ". Запусти 'FlatOutLike/Configs/Create EnemyPersonalityConfig PlayerHunter' сначала.");

            // Профили в порядке enemy entry: 0=Aggressor, 1=Coward, 2=PlayerHunter
            var profiles = new EnemyPersonalityConfig[] { aggressorProfile, cowardProfile, playerHunterProfile };

            // ── 4. Заполняем поля DerbyMatchConfig через SerializedObject ─────
            var configSo = new SerializedObject(config);

            configSo.FindProperty("_playerCarPrefab").objectReferenceValue = playerPrefab;
            configSo.FindProperty("_playerStats").objectReferenceValue     = carStats;
            // _enemyCarPrefab на уровне DerbyMatchConfig удалено — prefab теперь в каждом EnemySpawnEntry

            // ── 5. Заполняем массив _enemies (Serializable struct) ────────────
            // struct EnemySpawnEntry { GameObject carPrefab; EnemyPersonalityConfig profile; CarStatsConfig stats; }
            // Шаг 5b: entry[0]=Aggressor, entry[1]=Coward, entry[2]=PlayerHunter.
            // carPrefab и stats одинаковые для всех — дифференциация только в профиле.
            var enemiesProp = configSo.FindProperty("_enemies");
            enemiesProp.arraySize = EnemyCount;

            for (int i = 0; i < EnemyCount; i++)
            {
                var element = enemiesProp.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("carPrefab").objectReferenceValue = enemyPrefab;
                // Каждый враг получает свой профиль: 0=Aggressor, 1=Coward, 2=PlayerHunter.
                // Если профиль не загружен (null) — поле не трогаем, не падаем.
                // Guard i < profiles.Length: при EnemyCount > числа профилей лишние entry
                // просто остаются без профиля (не IndexOutOfRange).
                if (i < profiles.Length && profiles[i] != null)
                    element.FindPropertyRelative("profile").objectReferenceValue = profiles[i];
                element.FindPropertyRelative("stats").objectReferenceValue       = carStats;
            }

            configSo.ApplyModifiedProperties();
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();

            Debug.Log("[Editor] DerbyMatchConfig заполнен: " +
                      "_playerCarPrefab=" + (playerPrefab != null ? playerPrefab.name : "NULL") +
                      ", _playerStats=" + (carStats != null ? carStats.name : "NULL") +
                      ", _enemies[" + EnemyCount + "] carPrefab=" + (enemyPrefab != null ? enemyPrefab.name : "NULL") +
                      " profiles=[" +
                      (aggressorProfile != null    ? aggressorProfile.name    : "NULL") + "," +
                      (cowardProfile != null       ? cowardProfile.name       : "NULL") + "," +
                      (playerHunterProfile != null ? playerHunterProfile.name : "NULL") +
                      "] stats=Default (шаг 5b).");

            // ── 6. Находим GameLifetimeScope в активной сцене ─────────────────
            var scope = Object.FindAnyObjectByType<GameLifetimeScope>();
            if (scope == null)
            {
                Debug.LogWarning("[Editor] GameLifetimeScope не найден в активной сцене. " +
                                 "Открой Derby_Arena_01.unity и повтори запуск этого скрипта " +
                                 "для заполнения ссылок в Composition Root.");
                Undo.CollapseUndoOperations(undoGroup);
                return;
            }

            Debug.Log("[Editor] Найден GameLifetimeScope на '" + scope.gameObject.name + "'.");

            // ── 7. Заполняем ссылки на GameLifetimeScope ─────────────────────
            var scopeSo = new SerializedObject(scope);

            // Конфиг Derby-матча
            scopeSo.FindProperty("_derbyMatchConfig").objectReferenceValue = config;
            Debug.Log("[Editor] GameLifetimeScope._derbyMatchConfig = DerbyMatchConfig.");

            // Шаг 3-конфиги (нужны и в Derby-арене для DamageService / VfxService)
            var damageConfig = AssetDatabase.LoadAssetAtPath<DamageConfig>(DamageConfigPath);
            if (damageConfig == null)
                Debug.LogWarning("[Editor] DamageConfig не найден по " + DamageConfigPath +
                                 ". Запусти 'FlatOutLike/Configs/Create Default DamageConfig'.");
            scopeSo.FindProperty("_damageConfig").objectReferenceValue = damageConfig;

            var vfxConfig = AssetDatabase.LoadAssetAtPath<VfxConfig>(VfxConfigPath);
            if (vfxConfig == null)
                Debug.LogWarning("[Editor] VfxConfig не найден по " + VfxConfigPath +
                                 ". Запусти 'FlatOutLike/Configs/Create Default VfxConfig'.");
            scopeSo.FindProperty("_vfxConfig").objectReferenceValue = vfxConfig;

            // _sceneCars очищаем: Derby-арена использует CarFactory (_derbyMatchConfig path),
            // а не ручной спавн через _sceneCars (_TestDriving path). Пути взаимоисключающие.
            var sceneCarsProp = scopeSo.FindProperty("_sceneCars");
            sceneCarsProp.arraySize = 0;
            Debug.Log("[Editor] GameLifetimeScope._sceneCars очищен (используется путь CarFactory).");

            // Chase-камера: ищем в сцене компонент, проставляем ссылку в scope.
            // _target у камеры не трогаем — GameLifetimeScope.SetTarget вызывается при спавне игрока.
            var chaseCamera = Object.FindAnyObjectByType<ChaseCarCameraBehaviour>();
            if (chaseCamera == null)
            {
                Debug.LogWarning("[Editor] ChaseCarCameraBehaviour не найден в активной сцене. " +
                                 "Убедись, что 'FlatOutLike/Scenes/Build Derby Arena' уже выполнен. " +
                                 "Поле _chaseCamera в GameLifetimeScope останется пустым.");
            }
            else
            {
                Debug.Log("[Editor] Найден ChaseCarCameraBehaviour на '" + chaseCamera.gameObject.name + "'.");
            }
            scopeSo.FindProperty("_chaseCamera").objectReferenceValue = chaseCamera;
            Debug.Log("[Editor] GameLifetimeScope._chaseCamera = " +
                      (chaseCamera != null ? chaseCamera.gameObject.name : "NULL") + ".");

            // ── 8. Заполняем _spawnPoints из дочерних объектов SpawnPoints ────
            FillSpawnPoints(scopeSo);

            // ── 9. Применяем и сохраняем ─────────────────────────────────────
            scopeSo.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());

            Undo.CollapseUndoOperations(undoGroup);

            Debug.Log("[Editor] Create Default DerbyMatchConfig завершён — " +
                      "ассет создан, GameLifetimeScope проводка выполнена.");
        }

        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Заполняет GameLifetimeScope._spawnPoints Transform'ами из дочерних объектов
        /// SpawnPoints, отсортированных по имени (SpawnPoint_0..6 → правильный порядок).
        /// </summary>
        private static void FillSpawnPoints(SerializedObject scopeSo)
        {
            var spawnPointsGo = GameObject.Find("SpawnPoints");
            if (spawnPointsGo == null)
            {
                Debug.LogWarning("[Editor] Объект SpawnPoints не найден в сцене. " +
                                 "Запусти 'FlatOutLike/Spawn/Populate Derby Arena Spawn Points' сначала. " +
                                 "Поле _spawnPoints в GameLifetimeScope останется пустым.");
                return;
            }

            // Собираем дочерние Transform'ы и сортируем по имени
            var transforms = new List<Transform>();
            foreach (Transform child in spawnPointsGo.transform)
                transforms.Add(child);

            // Сортировка по имени: SpawnPoint_0, SpawnPoint_1, ..., SpawnPoint_6
            // string.Compare правильно обрабатывает числовые суффиксы до 9 включительно
            transforms.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.Ordinal));

            var spawnProp = scopeSo.FindProperty("_spawnPoints");
            spawnProp.arraySize = transforms.Count;
            for (int i = 0; i < transforms.Count; i++)
                spawnProp.GetArrayElementAtIndex(i).objectReferenceValue = transforms[i];

            Debug.Log("[Editor] GameLifetimeScope._spawnPoints заполнен: " +
                      transforms.Count + " точек [" +
                      string.Join(", ", transforms.ConvertAll(t => t.name)) + "].");
        }

        // ─────────────────────────────────────────────────────────────────────

        private static void EnsureFolder(string parent, string folderName)
        {
            string full = parent + "/" + folderName;
            if (!AssetDatabase.IsValidFolder(full))
            {
                AssetDatabase.CreateFolder(parent, folderName);
                Debug.Log("[Editor] Создана папка " + full);
            }
        }
    }
}
#endif
