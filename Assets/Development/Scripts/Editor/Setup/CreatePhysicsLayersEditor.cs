#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using FlatOutLike.Models;
using FlatOutLike.Controllers.Car;

namespace FlatOutLike.Editor.Setup
{
    /// <summary>
    /// Регистрирует физические слои (Vehicle/Wall/Hazard/Pickup/Floor) в TagManager
    /// и назначает их объектам активной сцены.
    ///
    /// Слой Floor регистрируется в TagManager, но НЕ назначается объектам сцены —
    /// пол переносится и размечается вручную (специфический нейминг объектов пола
    /// зависит от сцены). Матрица коллизий не трогается: Floor по умолчанию
    /// сталкивается со всеми слоями — это нужное поведение (машины/ragdoll/пикапы
    /// физически стоят на полу). Слой Floor используется отдельно в DamageConfig
    /// как маска исключения урона (см. CreateDefaultDamageConfigEditor).
    ///
    /// ПОРЯДОК ЗАПУСКА:
    ///   1. FlatOutLike/Setup/Create Physics Layers  ← этот скрипт
    ///   2. FlatOutLike/Setup/Setup Enemy Car
    ///   3. FlatOutLike/Configs/Create Default AISensorConfig
    ///
    /// Идемпотентность: повторный запуск безопасен — существующие слои не дублируются,
    /// слои объектов перезаписываются тем же значением.
    /// </summary>
    public static class CreatePhysicsLayersEditor
    {
        [MenuItem("FlatOutLike/Setup/Create Physics Layers")]
        public static void CreatePhysicsLayers()
        {
            Undo.SetCurrentGroupName("Create Physics Layers");
            int undoGroup = Undo.GetCurrentGroup();

            // ── Шаг 1: регистрируем слои в ProjectSettings/TagManager.asset ──
            RegisterAllLayers();

            // ── Шаг 2: назначаем слои объектам активной сцены ─────────────────
            // LayerMask.NameToLayer возвращает корректный индекс после ApplyModifiedProperties.
            AssignSceneLayers();

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Undo.CollapseUndoOperations(undoGroup);

            Debug.Log("[Editor] Create Physics Layers: завершено.");
        }

        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Загружает TagManager один раз и последовательно регистрирует все слои проекта.
        /// </summary>
        private static void RegisterAllLayers()
        {
            var tagManager = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layersProp = tagManager.FindProperty("layers");

            bool anyChanges = false;
            anyChanges |= EnsureLayer(layersProp, Layers.Vehicle);
            anyChanges |= EnsureLayer(layersProp, Layers.Wall);
            anyChanges |= EnsureLayer(layersProp, Layers.Hazard);
            anyChanges |= EnsureLayer(layersProp, Layers.Pickup);
            anyChanges |= EnsureLayer(layersProp, Layers.Floor);

            if (anyChanges)
            {
                tagManager.ApplyModifiedProperties();
                Debug.Log("[Editor] TagManager обновлён — новые слои сохранены.");
            }
        }

        /// <summary>
        /// Добавляет слой <paramref name="layerName"/> в TagManager, если его ещё нет.
        /// Пользовательские слои занимают индексы 8–31; 0–7 зарезервированы движком.
        /// </summary>
        /// <returns>true — слой добавлен (нужен ApplyModifiedProperties), false — уже существовал.</returns>
        private static bool EnsureLayer(SerializedProperty layersProp, string layerName)
        {
            // Проверяем: слой уже зарегистрирован?
            for (int i = 0; i < layersProp.arraySize; i++)
            {
                if (layersProp.GetArrayElementAtIndex(i).stringValue == layerName)
                {
                    Debug.Log($"[Editor] Слой '{layerName}' уже существует (index {i}), пропускаем.");
                    return false;
                }
            }

            // Ищем первый свободный пользовательский слот (индексы 8..31)
            for (int i = 8; i < layersProp.arraySize; i++)
            {
                if (string.IsNullOrEmpty(layersProp.GetArrayElementAtIndex(i).stringValue))
                {
                    layersProp.GetArrayElementAtIndex(i).stringValue = layerName;
                    Debug.Log($"[Editor] Слой '{layerName}' -> index {i}");
                    return true;
                }
            }

            Debug.LogError($"[Editor] Нет свободных слотов (8..31) для слоя '{layerName}'. " +
                           "Удали неиспользуемый пользовательский слой вручную в Edit → Project Settings → Tags and Layers.");
            return false;
        }

        // ─────────────────────────────────────────────────────────────────────

        private static void AssignSceneLayers()
        {
            AssignVehicleLayers();
            AssignWallLayer();
        }

        /// <summary>
        /// Назначает слой Vehicle всем машинам в активной сцене рекурсивно по иерархии.
        /// Коллайдеры, как правило, находятся на дочерних объектах — потому рекурсия обязательна.
        /// </summary>
        private static void AssignVehicleLayers()
        {
            int vehicleLayer = LayerMask.NameToLayer(Layers.Vehicle);
            if (vehicleLayer < 0)
            {
                Debug.LogError($"[Editor] Слой '{Layers.Vehicle}' не найден сразу после регистрации. " +
                               "Попробуй перезапустить Unity Editor и повторить.");
                return;
            }

            var cars = Object.FindObjectsByType<CarController>(FindObjectsSortMode.None);
            if (cars.Length == 0)
            {
                Debug.LogWarning("[Editor] CarController'ы не найдены в активной сцене. " +
                                 "Слой Vehicle не назначен. Открой сцену с машинами и повтори.");
                return;
            }

            foreach (var car in cars)
            {
                var root = car.transform.root.gameObject;
                Undo.RegisterFullObjectHierarchyUndo(root, "Assign Vehicle Layer");
                SetLayerRecursively(root, vehicleLayer);
                Debug.Log($"[Editor] Слой '{Layers.Vehicle}' назначен иерархии '{root.name}' (рекурсивно).");
            }
        }

        /// <summary>
        /// Назначает слой Wall объекту CrashTestWall в активной сцене.
        /// Имя объекта берётся из AddCrashWallToTestDrivingEditor.WallName — единственный источник.
        /// </summary>
        private static void AssignWallLayer()
        {
            int wallLayer = LayerMask.NameToLayer(Layers.Wall);
            if (wallLayer < 0)
            {
                Debug.LogError($"[Editor] Слой '{Layers.Wall}' не найден сразу после регистрации. " +
                               "Попробуй перезапустить Unity Editor и повторить.");
                return;
            }

            var wall = GameObject.Find(AddCrashWallToTestDrivingEditor.WallName);
            if (wall == null)
            {
                Debug.LogWarning($"[Editor] Объект '{AddCrashWallToTestDrivingEditor.WallName}' не найден " +
                                 "в активной сцене — пропускаем назначение слоя Wall. " +
                                 "Сначала запусти 'FlatOutLike/Setup/Add Crash Wall to Test Driving', " +
                                 "затем повтори 'Create Physics Layers'.");
                return;
            }

            Undo.RegisterFullObjectHierarchyUndo(wall, "Assign Wall Layer");
            SetLayerRecursively(wall, wallLayer);
            Debug.Log($"[Editor] Слой '{Layers.Wall}' назначен '{wall.name}' (рекурсивно).");
        }

        /// <summary>
        /// Рекурсивно назначает <paramref name="layer"/> объекту <paramref name="go"/>
        /// и всей его дочерней иерархии (важно: коллайдеры физики обычно на детях).
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
