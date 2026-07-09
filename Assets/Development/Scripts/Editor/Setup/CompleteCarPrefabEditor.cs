#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using FlatOutLike.Controllers.Car;
using FlatOutLike.Data.Configs.Car;
using FlatOutLike.Models;

namespace FlatOutLike.Editor.Setup
{
    /// <summary>
    /// Доводит «голый» каркас prefab-ассета машины (результат
    /// BuildCarPrefabFromFbxEditor) до полной комплектации Sport_09 — той же
    /// цепочкой шагов, что применялась к нему:
    ///   1. (опц.) Переключение CarController._config на вариантный CarStatsConfig.
    ///   2. Слой Vehicle рекурсивно на всю иерархию.
    ///   3. Damage-шейдер на материалах корпуса (SetupCarDamageShaderEditor.ConvertCar).
    ///   4. CarDamageVisualController + ссылки (SetupCarDamageVisualEditor.SetupCarDamageVisualOnPrefab).
    ///   5. Windshield и водитель — общими скриптами по ВСЕМ обнаруженным машинам
    ///      (SetupWindshieldShatterEditor / AttachDriverToCarPrefabsEditor, водитель последним).
    ///
    /// Меню работает по выделению: выдели один или несколько prefab-ассетов машин
    /// (CarController на корне) в Assets/Development/Prefabs/Vehicles/ и запусти.
    /// Идемпотентно: каждый шаг переиспользует уже существующие компоненты/материалы,
    /// повторный запуск ничего не дублирует.
    ///
    /// Используется и напрямую (новая машина в каталог руками), и из
    /// CreateDefaultCarCatalogConfigEditor (машины каталога по умолчанию).
    /// </summary>
    public static class CompleteCarPrefabEditor
    {
        [MenuItem("FlatOutLike/Setup/Complete Car Prefab From Selected")]
        public static void CompleteSelectedCarPrefabs()
        {
            Undo.SetCurrentGroupName("Complete Car Prefab From Selected");
            int undoGroup = Undo.GetCurrentGroup();

            int completed = 0;
            foreach (var obj in Selection.gameObjects)
            {
                string path = AssetDatabase.GetAssetPath(obj);
                if (string.IsNullOrEmpty(path) || !path.EndsWith(".prefab"))
                {
                    Debug.LogWarning("[Editor] '" + obj.name + "' — не prefab-ассет. Пропущен. " +
                                      "Выдели prefab в Project-окне, не объект сцены.");
                    continue;
                }

                var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (root == null || root.GetComponent<CarController>() == null)
                {
                    Debug.LogWarning("[Editor] '" + path + "' — нет CarController на корне. Пропущен. " +
                                      "Сначала собери каркас: 'FlatOutLike/Setup/Build Car Prefab From Selected FBX'.");
                    continue;
                }

                // Статы не подменяем (statsForVariant = null): у машины остаётся конфиг,
                // назначенный при сборке каркаса; вариант при желании меняется в Inspector.
                CompletePrefab(path, null);
                completed++;
            }

            if (completed == 0)
            {
                Debug.LogWarning("[Editor] Complete Car Prefab: не выделено ни одного подходящего префаба машины.");
                Undo.CollapseUndoOperations(undoGroup);
                return;
            }

            // Общие для всех машин шаги. Оба скрипта сами обнаруживают ВСЕ префабы
            // в Vehicles/ (см. CarPrefabDiscovery); порядок как у Sport_09 — водитель последним.
            SetupWindshieldShatterEditor.SetupWindshieldShatter();
            AttachDriverToCarPrefabsEditor.AttachDriverToCarPrefabs();

            Undo.CollapseUndoOperations(undoGroup);
            Debug.Log("[Editor] Complete Car Prefab From Selected завершён. Обработано машин: " + completed + ".");
        }

        /// <summary>
        /// Пер-префабная часть комплектации: вариантный CarStatsConfig (если передан) →
        /// слой Vehicle → damage-шейдер → damage-визуал. Windshield и водитель сюда
        /// намеренно не входят — они обрабатывают все обнаруженные машины разом
        /// и вызываются один раз ПОСЛЕ этого метода (см. CompleteSelectedCarPrefabs).
        /// </summary>
        public static void CompletePrefab(string prefabPath, CarStatsConfig statsForVariant)
        {
            var contents = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var carController = contents.GetComponentInChildren<CarController>(true);
                if (carController == null)
                {
                    Debug.LogWarning("[Editor] CarController не найден в " + prefabPath + ". " +
                                      "Пайплайн комплектации пропущен.");
                    return;
                }

                // ── 1. Переключаем на конфиг конкретного варианта (heavy/light), если передан.
                // BuildCarPrefabFromFbxEditor всегда подключает CarStatsConfig_Default —
                // для баланса машин каталога нужен свой вариант. null = конфиг не трогаем.
                if (statsForVariant != null)
                {
                    var carSo = new SerializedObject(carController);
                    var configProp = carSo.FindProperty("_config");
                    if (configProp != null)
                    {
                        configProp.objectReferenceValue = statsForVariant;
                        carSo.ApplyModifiedProperties();
                        Debug.Log("[Editor] " + prefabPath + ": CarController._config = " +
                                  AssetDatabase.GetAssetPath(statsForVariant) + ".");
                    }
                }

                // ── 2. Слой Vehicle рекурсивно на всю иерархию (см. BuildEnemyCarPrefabEditor).
                int vehicleLayer = LayerMask.NameToLayer(Layers.Vehicle);
                if (vehicleLayer >= 0)
                {
                    SetLayerRecursively(contents, vehicleLayer);
                    Debug.Log("[Editor] Слой '" + Layers.Vehicle + "' назначен " + prefabPath + " рекурсивно.");
                }
                else
                {
                    Debug.LogWarning("[Editor] Слой '" + Layers.Vehicle + "' не зарегистрирован. " +
                                      "Запусти 'FlatOutLike/Setup/Create Physics Layers' сначала — " +
                                      prefabPath + " остаётся на слое Default.");
                }

                // ── 3. Damage-шейдер на материалах корпуса ──────────────────────
                SetupCarDamageShaderEditor.ConvertCar(carController);
                Debug.Log("[Editor] Damage-шейдер конвертирован на " + prefabPath + ".");

                // ── 4. CarDamageVisualController + ссылки (свой вариант статов) ─
                SetupCarDamageVisualEditor.SetupCarDamageVisualOnPrefab(contents, carController, statsForVariant);

                PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
                Debug.Log("[Editor] Пайплайн комплектации сохранён: " + prefabPath);
            }
            finally
            {
                // UnloadPrefabContents ОБЯЗАТЕЛЕН в finally — уничтожает preview-сцену
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        /// <summary>Рекурсивно назначает <paramref name="layer"/> объекту и всей его иерархии.</summary>
        private static void SetLayerRecursively(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform)
                SetLayerRecursively(child.gameObject, layer);
        }
    }
}
#endif
