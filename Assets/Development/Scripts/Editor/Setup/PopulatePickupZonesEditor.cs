#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using FlatOutLike.Installers;

namespace FlatOutLike.Editor.Setup
{
    /// <summary>
    /// Расставляет маркеры зон пикапов в Derby_Arena_01.unity и заполняет
    /// массив GameLifetimeScope._pickupZones.
    ///
    /// Иерархия в сцене:
    ///   PickupZones (родитель)
    ///   ├── PickupZone_0  — Repair (enumValueIndex=0), pos=(10, 0.5, 10)
    ///   └── PickupZone_1  — Nitro  (enumValueIndex=1), pos=(-10, 0.5, -10)
    ///
    /// Позиции выбраны вне радиуса спавна машин (SpawnRadius=18) — машины спавнятся
    /// на кругу, пикапы стоят в углах арены, не мешая старту матча.
    ///
    /// Идемпотентность: существующие маркеры переставляются, не пересоздаются.
    /// _pickupZones в Scope перезаписывается при каждом запуске.
    ///
    /// Запускать ПОСЛЕ:
    ///   FlatOutLike/Scenes/Build Derby Arena
    ///   FlatOutLike/Configs/Create Default PickupConfig  (не обязательно, но логично)
    /// Запускать ДО / вместе с:
    ///   FlatOutLike/Configs/Create Default DerbyMatchConfig  (проводит прочие ссылки в Scope)
    /// </summary>
    public static class PopulatePickupZonesEditor
    {
        private const string ZonesParentName = "PickupZones";

        // Позиции: на y=0.5 над полом, вне зоны спавна машин (радиус 18)
        private static readonly (int index, Vector3 pos)[] ZoneData =
        {
            (0, new Vector3( 10f, 0.5f,  10f)),  // PickupZone_0 — Repair
            (1, new Vector3(-10f, 0.5f, -10f)),  // PickupZone_1 — Nitro
        };

        [MenuItem("FlatOutLike/Spawn/Populate Pickup Zones")]
        public static void PopulatePickupZones()
        {
            Undo.SetCurrentGroupName("Populate Pickup Zones");
            int undoGroup = Undo.GetCurrentGroup();

            // ── 1. Создаём/переиспользуем родительский объект PickupZones ────
            var parent = GetOrCreateParent();

            // ── 2. Расставляем маркеры ────────────────────────────────────────
            foreach (var (idx, pos) in ZoneData)
                SetupZoneMarker(parent, idx, pos);

            // ── 3. Заполняем _pickupZones в GameLifetimeScope ─────────────────
            FillPickupZones(parent);

            // ── 4. Сохраняем сцену ───────────────────────────────────────────
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());

            Undo.CollapseUndoOperations(undoGroup);

            Debug.Log("[Editor] Populate Pickup Zones завершён: " +
                      ZoneData.Length + " зон расставлено и записано в GameLifetimeScope._pickupZones.");
        }

        // ─────────────────────────────────────────────────────────────────────

        private static GameObject GetOrCreateParent()
        {
            var existing = GameObject.Find(ZonesParentName);
            if (existing != null)
            {
                Debug.Log("[Editor] Объект PickupZones уже существует — переиспользуем.");
                return existing;
            }

            var parent = new GameObject(ZonesParentName);
            Undo.RegisterCreatedObjectUndo(parent, "Create PickupZones Parent");
            parent.transform.position = Vector3.zero;
            Debug.Log("[Editor] Создан объект PickupZones.");
            return parent;
        }

        /// <summary>
        /// Находит PickupZone_<index> среди дочерних <paramref name="parent"/>.
        /// Если существует — переставляет; если нет — создаёт и регистрирует Undo.
        /// </summary>
        private static void SetupZoneMarker(GameObject parent, int index, Vector3 pos)
        {
            string markerName = "PickupZone_" + index;

            Transform existing = null;
            foreach (Transform child in parent.transform)
            {
                if (child.name == markerName)
                {
                    existing = child;
                    break;
                }
            }

            if (existing != null)
            {
                Undo.RecordObject(existing, "Reposition " + markerName);
                existing.position = pos;
                Debug.Log("[Editor] " + markerName + " переставлен → pos=" + pos + ".");
            }
            else
            {
                var go = new GameObject(markerName);
                Undo.RegisterCreatedObjectUndo(go, "Create " + markerName);
                go.transform.SetParent(parent.transform);
                go.transform.position = pos;
                Debug.Log("[Editor] Создан " + markerName + " pos=" + pos + ".");
            }
        }

        /// <summary>
        /// Заполняет GameLifetimeScope._pickupZones:
        /// каждый элемент → { point=Transform маркера, type=enumValueIndex(i) }.
        /// Маркеры сортируются по имени: PickupZone_0=Repair(0), PickupZone_1=Nitro(1).
        /// </summary>
        private static void FillPickupZones(GameObject parent)
        {
            var scope = Object.FindAnyObjectByType<GameLifetimeScope>();
            if (scope == null)
            {
                Debug.LogWarning("[Editor] GameLifetimeScope не найден в активной сцене. " +
                                 "Открой Derby_Arena_01.unity и повтори запуск. " +
                                 "Поле _pickupZones не заполнено.");
                return;
            }

            // Собираем дочерние маркеры и сортируем по имени (PickupZone_0 < PickupZone_1)
            var markers = new List<Transform>();
            foreach (Transform child in parent.transform)
                markers.Add(child);

            markers.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.Ordinal));

            var scopeSo    = new SerializedObject(scope);
            var zonesProp  = scopeSo.FindProperty("_pickupZones");

            if (zonesProp == null)
            {
                Debug.LogWarning("[Editor] Поле '_pickupZones' не найдено в GameLifetimeScope. " +
                                 "Убедись, что code-writer добавил его в GameLifetimeScope.cs.");
                return;
            }

            zonesProp.arraySize = markers.Count;
            for (int i = 0; i < markers.Count; i++)
            {
                var element = zonesProp.GetArrayElementAtIndex(i);
                // point — Transform маркера зоны
                element.FindPropertyRelative("point").objectReferenceValue = markers[i];
                // type — PickupType enum: 0=Repair, 1=Nitro (порядок совпадает с PickupZone_0/1)
                element.FindPropertyRelative("type").enumValueIndex = i;
            }

            scopeSo.ApplyModifiedProperties();

            Debug.Log("[Editor] GameLifetimeScope._pickupZones заполнен: " +
                      markers.Count + " зон [" +
                      string.Join(", ", markers.ConvertAll(t => t.name)) + "].");
        }
    }
}
#endif
