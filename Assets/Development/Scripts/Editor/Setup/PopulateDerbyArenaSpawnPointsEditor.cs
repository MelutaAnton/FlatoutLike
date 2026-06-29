#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FlatOutLike.Editor.Setup
{
    /// <summary>
    /// Расставляет 7 точек спавна (SpawnPoint_0..6) по кругу в центре арены.
    ///
    /// Структура иерархии:
    ///   SpawnPoints (родитель)
    ///   ├── SpawnPoint_0   ← игрок (index 0 в CarFactory)
    ///   ├── SpawnPoint_1   ← враг 1
    ///   ├── ...
    ///   └── SpawnPoint_6   ← враг 6
    ///
    /// Каждый SpawnPoint смотрит горизонтально в центр арены (0,0,0),
    /// расположен на высоте 0.5 ед. от пола (машины спавнятся на y ≈ 1 ед.
    /// с учётом Rigidbody-гравитации при старте).
    ///
    /// Идемпотентность: если SpawnPoint_X уже существует — его позиция и поворот
    /// обновляются (переиспользование без удаления и пересоздания).
    ///
    /// Запускать ПОСЛЕ:
    ///   FlatOutLike/Scenes/Build Derby Arena
    ///
    /// Запускать ДО:
    ///   FlatOutLike/Configs/Create Default DerbyMatchConfig
    /// </summary>
    public static class PopulateDerbyArenaSpawnPointsEditor
    {
        private const string SpawnPointsParentName = "SpawnPoints";
        private const int    SpawnCount            = 7;
        private const float  SpawnRadius           = 18f;  // внутри арены 60x60 (half=30)
        private const float  SpawnHeight           = 0.5f; // над полом

        [MenuItem("FlatOutLike/Spawn/Populate Derby Arena Spawn Points")]
        public static void PopulateSpawnPoints()
        {
            Undo.SetCurrentGroupName("Populate Derby Arena Spawn Points");
            int undoGroup = Undo.GetCurrentGroup();

            // ── 1. Находим или создаём родительский объект SpawnPoints ─────────
            var parent = GetOrCreateSpawnPointsParent();

            // ── 2. Расставляем SpawnPoint_0..6 по кругу ──────────────────────
            for (int i = 0; i < SpawnCount; i++)
            {
                // Угол в радианах: 0 → 360/7 → 2*(360/7) → ...
                float angleDeg = (360f / SpawnCount) * i;
                float angleRad = angleDeg * Mathf.Deg2Rad;

                // Позиция на окружности радиуса SpawnRadius
                float x = Mathf.Sin(angleRad) * SpawnRadius;
                float z = Mathf.Cos(angleRad) * SpawnRadius;
                var   pos = new Vector3(x, SpawnHeight, z);

                // Горизонтальный поворот «лицом к центру»
                // toCenter — горизонтальный вектор к (0,0,0): просто противоположный pos
                var toCenter = new Vector3(-x, 0f, -z).normalized;
                var rot = Quaternion.LookRotation(toCenter, Vector3.up);

                SetupSpawnPoint(parent, i, pos, rot);
            }

            // ── 3. Помечаем сцену dirty и сохраняем ─────────────────────────
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());

            Undo.CollapseUndoOperations(undoGroup);

            Debug.Log("[Editor] Populate Derby Arena Spawn Points завершён: " +
                      SpawnCount + " точек, радиус " + SpawnRadius + " ед., высота " + SpawnHeight + " ед.");
        }

        // ─────────────────────────────────────────────────────────────────────

        private static GameObject GetOrCreateSpawnPointsParent()
        {
            var existing = GameObject.Find(SpawnPointsParentName);
            if (existing != null)
            {
                Debug.Log("[Editor] SpawnPoints родитель уже существует — переиспользуем.");
                return existing;
            }

            var parent = new GameObject(SpawnPointsParentName);
            Undo.RegisterCreatedObjectUndo(parent, "Create SpawnPoints Parent");
            parent.transform.position = Vector3.zero;
            Debug.Log("[Editor] Создан объект SpawnPoints.");
            return parent;
        }

        /// <summary>
        /// Находит SpawnPoint_<paramref name="index"/> среди дочерних объектов
        /// <paramref name="parent"/> — если есть, обновляет позицию и поворот;
        /// если нет, создаёт новый и регистрирует Undo.
        /// </summary>
        private static void SetupSpawnPoint(GameObject parent, int index, Vector3 pos, Quaternion rot)
        {
            string pointName = "SpawnPoint_" + index;

            // Ищем существующий SpawnPoint среди дочерних объектов родителя
            Transform existing = null;
            foreach (Transform child in parent.transform)
            {
                if (child.name == pointName)
                {
                    existing = child;
                    break;
                }
            }

            if (existing != null)
            {
                // Переиспользуем: обновляем позицию и поворот (идемпотентно)
                Undo.RecordObject(existing, "Reposition " + pointName);
                existing.position = pos;
                existing.rotation = rot;
                Debug.Log("[Editor] " + pointName + " переставлен → pos=" + pos +
                          " angleDeg=" + Quaternion.Angle(Quaternion.identity, rot).ToString("F1") + ".");
            }
            else
            {
                // Создаём новый пустой GameObject
                var go = new GameObject(pointName);
                Undo.RegisterCreatedObjectUndo(go, "Create " + pointName);
                go.transform.SetParent(parent.transform);
                go.transform.position = pos;
                go.transform.rotation = rot;
                Debug.Log("[Editor] Создан " + pointName + " pos=" + pos + ".");
            }
        }
    }
}
#endif
