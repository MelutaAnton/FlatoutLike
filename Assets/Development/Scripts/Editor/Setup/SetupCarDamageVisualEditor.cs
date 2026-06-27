#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using FlatOutLike.Controllers.Car;
using FlatOutLike.Data.Configs.Car;

namespace FlatOutLike.Editor.Setup
{
    /// <summary>
    /// Добавляет CarDamageVisualController к объекту машины в открытой сцене _TestDriving
    /// и заполняет все его SerializedField-ссылки:
    ///   _car            → CarController на том же корне
    ///   _statsConfig    → CarStatsConfig_Default.asset
    ///   _visualConfig   → CarDamageVisualConfig.asset
    ///   _bodyRenderer   → первый Renderer, найденный в детях
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
        private const string ScenePath       = "Assets/Development/Scenes/_TestDriving.unity";
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
            if (statsConfig == null)
                Debug.LogWarning("[Editor] CarStatsConfig_Default not found at " + StatsConfigPath +
                                 ". Run 'FlatOutLike/Configs/Create Default CarStatsConfig' first. " +
                                 "_statsConfig will be null.");

            var visualConfig = AssetDatabase.LoadAssetAtPath<CarDamageVisualConfig>(VisualConfigPath);
            if (visualConfig == null)
                Debug.LogWarning("[Editor] CarDamageVisualConfig not found at " + VisualConfigPath +
                                 ". Run 'FlatOutLike/Configs/Create Default CarDamageVisualConfig' first. " +
                                 "_visualConfig will be null.");

            // ── 5. Находим body-рендерер и сопутствующие компоненты ───────────
            // GetComponentInChildren ищет в глубину; первое попадание — меш корпуса машины.
            // У WheelCollider'ов по умолчанию нет MeshRenderer'ов, поэтому это безопасно.
            var bodyRenderer    = carGo.GetComponentInChildren<Renderer>();
            var bodyMeshFilter  = carGo.GetComponentInChildren<MeshFilter>();
            var skinnedRenderer = carGo.GetComponentInChildren<SkinnedMeshRenderer>();
            var detachableParts = carGo.GetComponentsInChildren<DetachablePart>();

            // ── 6. Заполняем SerializedField'ы через SerializedObject ─────────
            var so = new SerializedObject(damageVisual);

            so.FindProperty("_car").objectReferenceValue          = carController;
            so.FindProperty("_statsConfig").objectReferenceValue  = statsConfig;
            so.FindProperty("_visualConfig").objectReferenceValue = visualConfig;
            so.FindProperty("_bodyRenderer").objectReferenceValue = bodyRenderer;

            // Опционально: Strategy B (mesh swap)
            if (bodyMeshFilter != null)
                so.FindProperty("_bodyMeshFilter").objectReferenceValue = bodyMeshFilter;

            // Опционально: Strategy C (blend shapes)
            if (skinnedRenderer != null)
                so.FindProperty("_skinnedRenderer").objectReferenceValue = skinnedRenderer;

            // Отрывные детали (пустой массив ОК, если компонентов DetachablePart ещё нет)
            var partsProp = so.FindProperty("_detachableParts");
            partsProp.arraySize = detachableParts.Length;
            for (int i = 0; i < detachableParts.Length; i++)
                partsProp.GetArrayElementAtIndex(i).objectReferenceValue = detachableParts[i];

            so.ApplyModifiedProperties();

            Debug.Log("[Editor] CarDamageVisualController refs filled on " + carGo.name +
                      " — statsConfig=" + (statsConfig  != null ? statsConfig.name  : "NULL") +
                      ", visualConfig=" + (visualConfig != null ? visualConfig.name : "NULL") +
                      ", bodyRenderer=" + (bodyRenderer != null ? bodyRenderer.name : "NULL") +
                      ", detachableParts=" + detachableParts.Length + ".");

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
    }
}
#endif
