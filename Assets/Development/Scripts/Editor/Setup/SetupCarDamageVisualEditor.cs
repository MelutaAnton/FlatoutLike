#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using FlatOutLike.Editor.Tools;
using UnityEngine.SceneManagement;
using FlatOutLike.Controllers.Car;
using FlatOutLike.Data.Configs.Car;

namespace FlatOutLike.Editor.Setup
{
    /// <summary>
    /// Добавляет CarDamageVisualController к объекту машины в открытой сцене _TestDriving
    /// и заполняет его SerializedField-ссылки:
    ///   _car            → CarController на том же корне
    ///   _statsConfig    → CarStatsConfig_Default.asset
    ///   _visualConfig   → CarDamageVisualConfig.asset
    ///   _bodyMeshFilter → первый MeshFilter, найденный в детях (опционально)
    ///   _skinnedRenderer→ первый SkinnedMeshRenderer, найденный в детях (опционально)
    ///   _detachableParts→ все компоненты DetachablePart, найденные в детях
    ///
    /// Идемпотентность: повторный запуск обновляет ссылки, не дублируя компонент.
    ///
    /// Запускать ПОСЛЕ:
    ///   FlatOutLike/Setup/Build Driving Test Scene
    ///   FlatOutLike/Configs/Create Default CarStatsConfig
    ///   FlatOutLike/Configs/Create Default CarDamageVisualConfig
    /// </summary>
    public static class SetupCarDamageVisualEditor
    {
        // Временная тест-сцена шага 3 — в SceneKeys (runtime) её нет, ключ только здесь.
        private const string TestDrivingSceneKey = "_TestDriving";
        private static readonly string ScenePath = ScenePaths.For(TestDrivingSceneKey);
        private const string StatsConfigPath = "Assets/Development/Data/Configs/Car/CarStatsConfig_Default.asset";
        private const string VisualConfigPath= "Assets/Development/Data/Configs/Car/CarDamageVisualConfig.asset";

        [MenuItem("FlatOutLike/Setup/Setup Car Damage Visual")]
        public static void SetupCarDamageVisual()
        {
            Undo.SetCurrentGroupName("Setup Car Damage Visual");
            int undoGroup = Undo.GetCurrentGroup();

            // ── 1. Убеждаемся, что _TestDriving открыт ────────────────────────
            OpenSceneIfNeeded();

            // ── 2. Ищем машину по компоненту (надёжнее поиска по имени) ───────
            var carController = Object.FindAnyObjectByType<CarController>();
            if (carController == null)
            {
                Debug.LogWarning("[Editor] No CarController found in scene. " +
                                 "Run 'FlatOutLike/Setup/Build Driving Test Scene' first.");
                return;
            }

            var carGo = carController.gameObject;
            Debug.Log("[Editor] Found car: " + carGo.name);

            // ── 3. Добавляем CarDamageVisualController, если его ещё нет ──────
            var damageVisual = carGo.GetComponent<CarDamageVisualController>();
            if (damageVisual == null)
            {
                damageVisual = Undo.AddComponent<CarDamageVisualController>(carGo);
                Debug.Log("[Editor] Added CarDamageVisualController to " + carGo.name + ".");
            }
            else
            {
                Debug.Log("[Editor] CarDamageVisualController already present on " +
                          carGo.name + ", refreshing refs.");
            }

            // ── 4. Загружаем конфиги ──────────────────────────────────────────
            var statsConfig = AssetDatabase.LoadAssetAtPath<CarStatsConfig>(StatsConfigPath);
            var visualConfig = AssetDatabase.LoadAssetAtPath<CarDamageVisualConfig>(VisualConfigPath);

            // ── 5-6. Заполняем SerializedField-ссылки (общая логика с prefab-веткой) ──
            FillDamageVisualRefs(damageVisual, carGo, carController, statsConfig, visualConfig);

            // ── 7. Помечаем сцену как dirty ───────────────────────────────────
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

            Undo.CollapseUndoOperations(undoGroup);

            Debug.Log("[Editor] Car damage visual setup complete.");
        }

        // ─────────────────────────────────────────────────────────────────────
        private static void OpenSceneIfNeeded()
        {
            if (SceneManager.GetActiveScene().path == ScenePath) return;

            if (!System.IO.File.Exists(
                    System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), ScenePath)))
            {
                Debug.LogWarning("[Editor] _TestDriving.unity not found at " + ScenePath +
                                 ". Run 'FlatOutLike/Setup/Build Driving Test Scene' first.");
                return;
            }

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Debug.Log("[Editor] Opened _TestDriving.unity.");
        }

        /// <summary>
        /// Prefab-версия настройки (без сцены _TestDriving): добавляет/переиспользует
        /// CarDamageVisualController прямо на GameObject из PrefabUtility.LoadPrefabContents
        /// и заполняет ссылки. Используется другими editor-скриптами (например,
        /// CreateDefaultCarCatalogConfigEditor), которые дособирают новые префабы машин.
        /// Undo-группами и сохранением ассета НЕ управляет — это ответственность вызывающего.
        /// </summary>
        /// <param name="carGo">Корневой GameObject машины (внутри prefab-contents).</param>
        /// <param name="carController">CarController на том же корне.</param>
        /// <param name="statsConfigOverride">
        /// Конфиг характеристик конкретного варианта машины (heavy/light/...).
        /// Если null — используется CarStatsConfig_Default.
        /// </param>
        public static void SetupCarDamageVisualOnPrefab(
            GameObject carGo, CarController carController, CarStatsConfig statsConfigOverride)
        {
            var damageVisual = carGo.GetComponent<CarDamageVisualController>();
            if (damageVisual == null)
            {
                damageVisual = carGo.AddComponent<CarDamageVisualController>();
                Debug.Log("[Editor] Added CarDamageVisualController to " + carGo.name + " (prefab contents).");
            }
            else
            {
                Debug.Log("[Editor] CarDamageVisualController already present on " +
                          carGo.name + ", refreshing refs.");
            }

            var statsConfig = statsConfigOverride != null
                ? statsConfigOverride
                : AssetDatabase.LoadAssetAtPath<CarStatsConfig>(StatsConfigPath);
            var visualConfig = AssetDatabase.LoadAssetAtPath<CarDamageVisualConfig>(VisualConfigPath);

            FillDamageVisualRefs(damageVisual, carGo, carController, statsConfig, visualConfig);
        }

        // ─────────────────────────────────────────────────────────────────────
        /// <summary>
        /// Заполняет SerializedField-ссылки CarDamageVisualController. Общая логика для
        /// сценового (SetupCarDamageVisual) и prefab-based (SetupCarDamageVisualOnPrefab) путей.
        /// Не присваивает null поверх существующего значения поля — если ассет/объект не
        /// найден, поле остаётся как было, в консоль выводится предупреждение.
        /// Поле _bodyRenderer в текущей версии CarDamageVisualController отсутствует
        /// (рендереры собираются самим контроллером в рантайме через CollectBodyRenderers) —
        /// FindProperty на него не вызывается, чтобы не падать при рассинхроне со старым описанием.
        /// </summary>
        private static void FillDamageVisualRefs(
            CarDamageVisualController damageVisual, GameObject carGo, CarController carController,
            CarStatsConfig statsConfig, CarDamageVisualConfig visualConfig)
        {
            if (statsConfig == null)
                Debug.LogWarning("[Editor] CarStatsConfig не найден для " + carGo.name +
                                 ". Run 'FlatOutLike/Configs/Create Default CarStatsConfig' first. " +
                                 "_statsConfig оставлен без изменений.");

            if (visualConfig == null)
                Debug.LogWarning("[Editor] CarDamageVisualConfig не найден по " + VisualConfigPath +
                                 " для " + carGo.name + ". Run 'FlatOutLike/Configs/Create Default " +
                                 "CarDamageVisualConfig' first. _visualConfig оставлен без изменений.");

            // GetComponentInChildren ищет в глубину; первое попадание — меш корпуса машины.
            // У WheelCollider'ов по умолчанию нет MeshRenderer'ов, поэтому это безопасно.
            var bodyMeshFilter  = carGo.GetComponentInChildren<MeshFilter>();
            var skinnedRenderer = carGo.GetComponentInChildren<SkinnedMeshRenderer>();
            var detachableParts = carGo.GetComponentsInChildren<DetachablePart>();

            var so = new SerializedObject(damageVisual);

            SetIfPropertyExists(so, "_car", carController);
            if (statsConfig != null) SetIfPropertyExists(so, "_statsConfig", statsConfig);
            if (visualConfig != null) SetIfPropertyExists(so, "_visualConfig", visualConfig);

            // Опционально: Strategy B (mesh swap)
            if (bodyMeshFilter != null)
                SetIfPropertyExists(so, "_bodyMeshFilter", bodyMeshFilter);

            // Опционально: Strategy C (blend shapes)
            if (skinnedRenderer != null)
                SetIfPropertyExists(so, "_skinnedRenderer", skinnedRenderer);

            // Отрывные детали (пустой массив ОК, если компонентов DetachablePart ещё нет)
            var partsProp = so.FindProperty("_detachableParts");
            if (partsProp != null)
            {
                partsProp.arraySize = detachableParts.Length;
                for (int i = 0; i < detachableParts.Length; i++)
                    partsProp.GetArrayElementAtIndex(i).objectReferenceValue = detachableParts[i];
            }

            so.ApplyModifiedProperties();

            Debug.Log("[Editor] CarDamageVisualController refs filled on " + carGo.name +
                      " — statsConfig=" + (statsConfig  != null ? statsConfig.name  : "unchanged") +
                      ", visualConfig=" + (visualConfig != null ? visualConfig.name : "unchanged") +
                      ", detachableParts=" + detachableParts.Length + ".");
        }

        private static void SetIfPropertyExists(SerializedObject so, string propertyName, Object value)
        {
            var prop = so.FindProperty(propertyName);
            if (prop == null)
            {
                Debug.LogWarning("[Editor] Поле CarDamageVisualController." + propertyName +
                                 " не найдено — пропущено (возможно, устарело или ещё не добавлено).");
                return;
            }

            prop.objectReferenceValue = value;
        }
    }
}
#endif
