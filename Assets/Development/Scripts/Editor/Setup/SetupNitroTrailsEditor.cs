#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using FlatOutLike.Controllers.Car;

namespace FlatOutLike.Editor.Setup
{
    /// <summary>
    /// Донастраивает КАЖДЫЙ prefab машины под Assets/Development/Prefabs/Vehicles (у корня
    /// которого есть CarController): добавляет под корнем "NitroTrails" с двумя анкорами
    /// (левый/правый задний бампер), вешает на каждый анкор nested-инстанс огненного трейла
    /// из ThirdParty-пака "Real Fire &amp; Smoke" (ссылка на ассет, без копирования содержимого)
    /// и прошивает NitroTrailsController._car / ._trailEmitters.
    ///
    /// Позиции анкоров считаются из объединённых bounds всех НЕ-триггерных Collider'ов
    /// префаба (в локальных координатах корня) — так скрипт работает на любой машине пака
    /// без ручной подгонки координат под каждую модель.
    ///
    /// Идемпотентность: повторный запуск на уже настроенном префабе не создаёт дублей —
    /// анкоры и nested-инстансы переиспользуются, позиции анкоров пересчитываются заново
    /// (детерминированная функция от bounds — на неизменной модели даёт тот же результат).
    /// </summary>
    public static class SetupNitroTrailsEditor
    {
        private const string VehiclesFolder = "Assets/Development/Prefabs/Vehicles";
        private const string FireTrailAssetPath =
            "Assets/ThirdParty/Real Fire & Smoke/Prefabs/Fire Alternate/Trails/FireTrailALT.prefab";

        private const string NitroTrailsName = "NitroTrails";
        private const string AnchorLeftName = "NitroTrail_L";
        private const string AnchorRightName = "NitroTrail_R";

        [MenuItem("FlatOutLike/Setup/Setup Nitro Trails")]
        public static void SetupNitroTrails()
        {
            var fireTrailAsset = AssetDatabase.LoadAssetAtPath<GameObject>(FireTrailAssetPath);
            if (fireTrailAsset == null)
            {
                Debug.LogError("[Editor] Setup Nitro Trails: не найден ассет " + FireTrailAssetPath +
                                " — прерываю (нечем заполнять анкоры).");
                return;
            }

            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { VehiclesFolder });
            if (guids == null || guids.Length == 0)
            {
                Debug.LogWarning("[Editor] Setup Nitro Trails: в " + VehiclesFolder + " не найдено ни одного префаба.");
                return;
            }

            int processed = 0;
            int changedCount = 0;
            int skippedNoCarController = 0;

            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                    continue;

                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var car = root.GetComponent<CarController>();
                    if (car == null)
                    {
                        skippedNoCarController++;
                        continue;
                    }

                    processed++;
                    bool changed = ProcessCarPrefab(root, car, fireTrailAsset);

                    if (changed)
                    {
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                        changedCount++;
                        Debug.Log("[Editor] Setup Nitro Trails: сохранён префаб " + path + ".");
                    }
                    else
                    {
                        Debug.Log("[Editor] Setup Nitro Trails: без изменений — " + path + ".");
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[Editor] Setup Nitro Trails завершён. Обработано машин: " + processed +
                       ", изменено: " + changedCount +
                       ", пропущено (нет CarController): " + skippedNoCarController + ".");
        }

        /// <summary>Настраивает один загруженный prefab-контент. Возвращает true, если что-то изменилось.</summary>
        private static bool ProcessCarPrefab(GameObject root, CarController car, GameObject fireTrailAsset)
        {
            bool changed = false;

            var colliders = root.GetComponentsInChildren<Collider>(true);
            if (!TryComputeLocalBounds(root.transform, colliders, out Vector3 center, out Vector3 extents))
            {
                Debug.LogWarning("[Editor] Setup Nitro Trails: у '" + root.name +
                                  "' нет не-триггерных Collider'ов — не могу вычислить позиции анкоров, пропускаю.");
                return false;
            }

            // ── 1. Ensure "NitroTrails" ──────────────────────────────────────────
            var nitroTrails = EnsureChild(root.transform, NitroTrailsName, out bool createdRoot);
            if (createdRoot) changed = true;
            if (SetLocalPositionIfChanged(nitroTrails, Vector3.zero)) changed = true;

            // ── 2. Позиции анкоров из bounds ─────────────────────────────────────
            // z: смещение к заднему бамперу (-Z — зад машины) + небольшой отступ наружу.
            float anchorZ = -extents.z + 0.1f;
            // y: высота заднего бампера — чуть ниже центра bounds.
            float anchorY = center.y - extents.y * 0.5f;
            // x: 35% от полуширины машины от центра, симметрично влево/вправо.
            float anchorXOffset = extents.x * 0.35f;

            var leftPos = new Vector3(-anchorXOffset, anchorY, anchorZ);
            var rightPos = new Vector3(anchorXOffset, anchorY, anchorZ);
            var anchorRotation = Quaternion.LookRotation(Vector3.back);

            if (SetupAnchor(nitroTrails, AnchorLeftName, leftPos, anchorRotation, fireTrailAsset))
                changed = true;
            if (SetupAnchor(nitroTrails, AnchorRightName, rightPos, anchorRotation, fireTrailAsset))
                changed = true;

            var anchorL = nitroTrails.Find(AnchorLeftName);
            var anchorR = nitroTrails.Find(AnchorRightName);

            // ── 3. NitroTrailsController ─────────────────────────────────────────
            var controller = root.GetComponent<NitroTrailsController>();
            if (controller == null)
            {
                // Без Undo: работаем внутри LoadPrefabContents (временная preview-сцена),
                // Undo-записи там бессмысленны — «откат» = не сохранять SaveAsPrefabAsset.
                controller = root.AddComponent<NitroTrailsController>();
                Debug.Log("[Editor] Setup Nitro Trails: добавлен NitroTrailsController на '" + root.name + "'.");
                changed = true;
            }

            var so = new SerializedObject(controller);

            var carProp = so.FindProperty("_car");
            if (carProp != null && carProp.objectReferenceValue != car)
            {
                carProp.objectReferenceValue = car;
                changed = true;
            }

            var emittersProp = so.FindProperty("_trailEmitters");
            if (emittersProp != null)
            {
                if (emittersProp.arraySize != 2)
                {
                    emittersProp.arraySize = 2;
                    changed = true;
                }

                var element0 = emittersProp.GetArrayElementAtIndex(0);
                var element1 = emittersProp.GetArrayElementAtIndex(1);

                if (anchorL != null && element0.objectReferenceValue != anchorL.gameObject)
                {
                    element0.objectReferenceValue = anchorL.gameObject;
                    changed = true;
                }

                if (anchorR != null && element1.objectReferenceValue != anchorR.gameObject)
                {
                    element1.objectReferenceValue = anchorR.gameObject;
                    changed = true;
                }
            }

            so.ApplyModifiedProperties();

            return changed;
        }

        /// <summary>
        /// Ensure-анкор под nitroTrails: позиция/поворот, SetActive(false), и вложенный
        /// prefab-инстанс трейла (если ещё не создан). Возвращает true, если что-то изменилось.
        /// </summary>
        private static bool SetupAnchor(Transform nitroTrails, string anchorName, Vector3 localPos,
            Quaternion localRot, GameObject fireTrailAsset)
        {
            bool changed = false;

            var anchor = EnsureChild(nitroTrails, anchorName, out bool created);
            if (created) changed = true;

            if (SetLocalPositionIfChanged(anchor, localPos)) changed = true;
            if (SetLocalRotationIfChanged(anchor, localRot)) changed = true;

            // Контроллер в рантайме включает анкор явно — по умолчанию выключен.
            if (anchor.gameObject.activeSelf)
            {
                anchor.gameObject.SetActive(false);
                changed = true;
            }

            // Nested-инстанс трейла — создаём только если анкор ещё пуст.
            if (anchor.childCount == 0)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(fireTrailAsset, anchor);
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;
                changed = true;
                Debug.Log("[Editor] Setup Nitro Trails: создан nested-инстанс FireTrailALT под '" + anchorName + "'.");
            }

            return changed;
        }

        /// <summary>Ensure-ребёнок с данным именем. created=true, если объект только что создан.</summary>
        private static Transform EnsureChild(Transform parent, string name, out bool created)
        {
            var existing = parent.Find(name);
            if (existing != null)
            {
                created = false;
                return existing;
            }

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            created = true;
            return go.transform;
        }

        private static bool SetLocalPositionIfChanged(Transform t, Vector3 pos)
        {
            if (t.localPosition == pos) return false;
            t.localPosition = pos;
            return true;
        }

        private static bool SetLocalRotationIfChanged(Transform t, Quaternion rot)
        {
            if (t.localRotation == rot) return false;
            t.localRotation = rot;
            return true;
        }

        /// <summary>
        /// Считает bounds всех НЕ-триггерных Collider'ов в локальных координатах root
        /// (устойчиво к любому transform самого root — не полагается на identity).
        /// Возвращает false, если подходящих Collider'ов не нашлось.
        /// </summary>
        private static bool TryComputeLocalBounds(Transform root, Collider[] colliders, out Vector3 center, out Vector3 extents)
        {
            bool hasBounds = false;
            var localBounds = new Bounds();

            foreach (var col in colliders)
            {
                if (col == null || col.isTrigger) continue;

                var worldBounds = col.bounds;
                var wCenter = worldBounds.center;
                var wExtents = worldBounds.extents;

                for (int xi = -1; xi <= 1; xi += 2)
                for (int yi = -1; yi <= 1; yi += 2)
                for (int zi = -1; zi <= 1; zi += 2)
                {
                    var corner = wCenter + Vector3.Scale(wExtents, new Vector3(xi, yi, zi));
                    var localCorner = root.InverseTransformPoint(corner);

                    if (!hasBounds)
                    {
                        localBounds = new Bounds(localCorner, Vector3.zero);
                        hasBounds = true;
                    }
                    else
                    {
                        localBounds.Encapsulate(localCorner);
                    }
                }
            }

            center = localBounds.center;
            extents = localBounds.extents;
            return hasBounds;
        }
    }
}
#endif
