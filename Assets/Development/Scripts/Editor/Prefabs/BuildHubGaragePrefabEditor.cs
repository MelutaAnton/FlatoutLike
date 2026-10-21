#if UNITY_EDITOR
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FlatOutLike.Editor.Prefabs
{
    /// <summary>
    /// Полиш хаба: собирает составной финальный визуал гаража
    /// Assets/Development/Prefabs/Hub/Garage/Garage_Final.prefab.
    ///
    /// Состав: ангар factory_007 (центр bounds по XZ в (0,0), низ на y=0) + декор Junkyard_1 вплотную
    /// к ангару по бокам (±X) и сзади (-Z). Перед ангаром (+Z, где зона запуска) — пусто.
    /// Масштаб РЕАЛЬНЫЙ (метры пака): весь префаб затем вписывается FitVisual'ом в VisualFootprint.
    /// Чтобы ангар после вписывания не стал мелким, габарит всего префаба по XZ ограничен
    /// ангар + 25% (по 12.5% на сторону; сзади — 25% глубины): деталь, не влезающая в «бюджет» стороны,
    /// поворачивается длинной стороной вдоль ангара, в крайнем случае слегка уменьшается (Warning).
    /// Повороты по Y — детерминированные (фиксированный seed) + лёгкий джиттер.
    /// Детали — вложенные инстансы (PrefabUtility.InstantiatePrefab, ссылки, не копии); у паков Junkyard коллайдеров нет —
    /// EnsureColliders добавляет sibling-объекты Collider_* (OBB BoxCollider) и для уже существующего префаба.
    ///
    /// Префаб создаётся, ТОЛЬКО если его нет (пользователь может двигать декор руками) — существующий
    /// не перезаписывается. Сборка идёт в preview-сцене (активная сцена не пачкается).
    ///
    /// Порядок: Build Hub Garage Prefab -> Create Building SO Set -> (BaseHub открыт) Build Base Hub Layout.
    /// </summary>
    public static class BuildHubGaragePrefabEditor
    {
        /// <summary>Путь собранного префаба — единственное место (читает Create Building SO Set).</summary>
        public const string PrefabPath = "Assets/Development/Prefabs/Hub/Garage/Garage_Final.prefab";

        private const string JunkRoot = "Assets/ThirdParty/Junkyard_1/Prefabs/";
        private const string HangarPath = JunkRoot + "factory_007.prefab";

        // Зазор между ангаром и деталью / между соседними деталями, м.
        private const float Gap = 0.3f;

        // Допуск роста габарита префаба относительно ангара: доля размера, суммарно по оси.
        private const float GrowthLimit = 0.25f;

        // Нижняя граница аварийного уменьшения детали, не влезающей в бюджет стороны.
        private const float MinShrink = 0.6f;

        // Максимальный лёгкий разворот детали по Y (±), градусы.
        private const float YawJitter = 12f;

        private const int Seed = 20261022;

        private enum Side { Left, Right, Rear }

        private readonly struct DecorDef
        {
            public readonly string Name;
            public readonly Side Side;
            // Желаемая позиция вдоль стороны: -1..1 от середины ангара (по Z для боков, по X для тыла).
            public readonly float T;

            public DecorDef(string name, Side side, float t)
            {
                Name = name;
                Side = side;
                T = t;
            }
        }

        // Порядок внутри стороны — по возрастанию T (упаковка идёт последовательно).
        private static readonly DecorDef[] Decor =
        {
            // Тыл (-Z): длинные предметы вдоль задней стены.
            new DecorDef("container_001",   Side.Rear,  -0.35f),
            new DecorDef("wreck_car_001",   Side.Rear,   0.45f),
            // Левый бок (-X).
            new DecorDef("wreck_car_003",   Side.Left,  -0.40f),
            new DecorDef("tires_001",       Side.Left,   0.25f),
            new DecorDef("barrel_001",      Side.Left,   0.65f),
            // Правый бок (+X).
            new DecorDef("tires_001",       Side.Right, -0.45f),
            new DecorDef("tire_001",        Side.Right, -0.05f),
            new DecorDef("barrel_001",      Side.Right,  0.40f),
        };

        [MenuItem("FlatOutLike/Prefabs/Build Hub Garage Prefab")]
        public static void BuildHubGaragePrefab()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
            {
                Debug.Log("[Editor] Garage_Final уже существует: " + PrefabPath +
                          " — не пересобираем (декор может быть расставлен вручную), только проверяем коллайдеры.");
                EnsureColliders();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                return;
            }

            var hangarAsset = AssetDatabase.LoadAssetAtPath<GameObject>(HangarPath);
            if (hangarAsset == null)
            {
                Debug.LogError("[Editor] Ангар не найден: " + HangarPath + " — Garage_Final не создан.");
                return;
            }

            EnsureFolderPath(Path.GetDirectoryName(PrefabPath).Replace('\\', '/'));

            var previewScene = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = new GameObject(Path.GetFileNameWithoutExtension(PrefabPath));
                SceneManager.MoveGameObjectToScene(root, previewScene);
                // Undo для объектов preview-сцены и ассетов не регистрируем (см. editor-files.md).

                // Ангар: центр по XZ в (0,0), низ на y=0.
                var hangar = (GameObject)PrefabUtility.InstantiatePrefab(hangarAsset, root.transform);
                hangar.transform.localPosition = Vector3.zero;
                hangar.transform.localRotation = Quaternion.identity;
                hangar.transform.localScale = Vector3.one;

                if (!TryGetBounds(root.transform, hangar, out Bounds hb))
                {
                    Debug.LogError("[Editor] У ангара нет подходящих Renderer'ов — Garage_Final не создан.");
                    return;
                }

                hangar.transform.localPosition = new Vector3(-hb.center.x, -hb.min.y, -hb.center.z);
                TryGetBounds(root.transform, hangar, out hb);
                Debug.Log("[Editor] Ангар factory_007: габарит " + V(hb.size) + " м, центр " + V(hb.center) + ".");

                float hx = hb.size.x * 0.5f;
                float hz = hb.size.z * 0.5f;

                // Бюджет глубины декора: на бок — по половине допуска, на тыл — весь допуск по Z; минус зазор.
                float sideBudget = GrowthLimit * hb.size.x * 0.5f - Gap;
                float rearBudget = GrowthLimit * hb.size.z - Gap;
                Debug.Log("[Editor] Бюджет декора: бок " + F2(sideBudget) + " м, тыл " + F2(rearBudget) + " м.");

                var rng = new System.Random(Seed);
                var cursors = new Dictionary<Side, float>
                {
                    { Side.Left, float.NegativeInfinity },
                    { Side.Right, float.NegativeInfinity },
                    { Side.Rear, float.NegativeInfinity },
                };

                int placed = 0;
                foreach (var def in Decor)
                {
                    if (PlaceDecor(root.transform, def, hx, hz, sideBudget, rearBudget, rng, cursors))
                        placed++;
                }

                if (TryGetBounds(root.transform, root, out Bounds total))
                {
                    float maxGrowthX = hb.size.x * (1f + GrowthLimit);
                    float maxGrowthZ = hb.size.z * (1f + GrowthLimit);
                    Debug.Log("[Editor] Garage_Final: габарит всего " + F2(total.size.x) + " x " + F2(total.size.y) +
                               " x " + F2(total.size.z) + " м; ангар " + F2(hb.size.x) + " x " + F2(hb.size.y) + " x " +
                               F2(hb.size.z) + " м (предел ангар+25%: " + F2(maxGrowthX) + " x " + F2(maxGrowthZ) +
                               "); деталей декора " + placed + "/" + Decor.Length + ".");

                    if (total.size.x > maxGrowthX + 0.05f || total.size.z > maxGrowthZ + 0.05f)
                        Debug.LogWarning("[Editor] Garage_Final: габарит по XZ превышает ангар+25% — после вписывания " +
                                         "ангар получится мельче ожидаемого.");
                }

                var saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                if (saved == null)
                {
                    Debug.LogError("[Editor] SaveAsPrefabAsset вернул null: " + PrefabPath);
                    return;
                }

                Debug.Log("[Editor] Создан префаб гаража: " + PrefabPath);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(previewScene);
            }

            // У деталей Junkyard_1 коллайдеров нет — персонаж проходил бы сквозь: добавляем свои.
            EnsureColliders();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Editor] Build Hub Garage Prefab завершён.");
        }

        private const string ColliderPrefix = "Collider_";
        private const float ChangeEpsilon = 0.001f;

        /// <summary>
        /// Идемпотентно: для каждой детали-инстанса (прямого ребёнка корня Garage_Final) гарантирует
        /// sibling-объект Collider_&lt;имя&gt;[_n] с BoxCollider по OBB детали: поворот и масштаб детали,
        /// размер — по её локальным Renderer-bounds (высота = полная высота детали). ThirdParty-инстансы
        /// не модифицируются. Существующий Collider_* обновляется (центр/размер/поворот), дублей нет;
        /// ручные сдвиги декора сохраняются (позицию деталей не трогаем, коллайдер следует за ними).
        /// Префаб сохраняется, только если что-то изменилось.
        /// </summary>
        private static void EnsureColliders()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
            {
                Debug.LogWarning("[Editor] EnsureColliders: " + PrefabPath + " не найден — коллайдеры не добавлены.");
                return;
            }

            var contents = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var rootT = contents.transform;
                var occurrences = new Dictionary<string, int>();
                var parts = new List<Transform>();
                for (int i = 0; i < rootT.childCount; i++)
                {
                    var child = rootT.GetChild(i);
                    if (child.name.StartsWith(ColliderPrefix))
                        continue;
                    if (!PrefabUtility.IsAnyPrefabInstanceRoot(child.gameObject))
                        continue;
                    parts.Add(child);
                }

                bool changed = false;
                foreach (var part in parts)
                {
                    occurrences.TryGetValue(part.name, out int n);
                    occurrences[part.name] = n + 1;
                    string colliderName = ColliderPrefix + part.name + (n == 0 ? string.Empty : "_" + (n + 1));

                    if (!TryGetBounds(part, part.gameObject, out Bounds lb))
                    {
                        Debug.LogWarning("[Editor] Collider для '" + part.name + "': нет Renderer'ов — пропущен.");
                        continue;
                    }

                    // Позиция/поворот/масштаб коллайдер-объекта = трансформ детали в осях корня; центр bounds — в его локали.
                    Vector3 pos = rootT.InverseTransformPoint(part.TransformPoint(lb.center));
                    Quaternion rot = Quaternion.Inverse(rootT.rotation) * part.rotation;
                    Vector3 scale = part.localScale;

                    var holderT = rootT.Find(colliderName);
                    bool created = holderT == null;
                    GameObject holder;
                    if (created)
                    {
                        holder = new GameObject(colliderName);
                        holder.transform.SetParent(rootT, false);
                    }
                    else
                    {
                        holder = holderT.gameObject;
                    }

                    var box = holder.GetComponent<BoxCollider>();
                    bool addedBox = box == null;
                    if (addedBox)
                        box = holder.AddComponent<BoxCollider>();

                    var ht = holder.transform;
                    bool differs = created || addedBox ||
                                   (ht.localPosition - pos).sqrMagnitude > ChangeEpsilon * ChangeEpsilon ||
                                   Quaternion.Angle(ht.localRotation, rot) > 0.01f ||
                                   (ht.localScale - scale).sqrMagnitude > ChangeEpsilon * ChangeEpsilon ||
                                   box.center != Vector3.zero ||
                                   (box.size - lb.size).sqrMagnitude > ChangeEpsilon * ChangeEpsilon ||
                                   box.isTrigger;

                    if (!differs)
                    {
                        Debug.Log("[Editor] " + colliderName + ": актуален — не трогаем.");
                        continue;
                    }

                    ht.localPosition = pos;
                    ht.localRotation = rot;
                    ht.localScale = scale;
                    box.isTrigger = false;
                    box.center = Vector3.zero;
                    box.size = lb.size;
                    changed = true;
                    Debug.Log("[Editor] " + colliderName + (created ? " создан" : " обновлён") + ": размер " + V(lb.size) +
                              " (высота " + F2(lb.size.y) + " м), центр " + V(pos) + ", yaw " + F2(rot.eulerAngles.y) + ".");
                }

                if (!changed)
                {
                    Debug.Log("[Editor] EnsureColliders: коллайдеры Garage_Final актуальны (деталей " + parts.Count + ").");
                    return;
                }

                var saved = PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
                if (saved == null)
                    Debug.LogError("[Editor] SaveAsPrefabAsset вернул null: " + PrefabPath);
                else
                    Debug.Log("[Editor] EnsureColliders: Garage_Final сохранён с коллайдерами (деталей " + parts.Count + ").");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        /// <summary>
        /// Ставит одну деталь декора на сторону ангара. Позиция вдоль стороны упаковывается последовательно
        /// (cursors хранит конец предыдущей детали), поперёк — вплотную к стене с зазором Gap.
        /// </summary>
        private static bool PlaceDecor(Transform root, DecorDef def, float hx, float hz, float sideBudget,
            float rearBudget, System.Random rng, Dictionary<Side, float> cursors)
        {
            string path = JunkRoot + def.Name + ".prefab";
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null)
            {
                Debug.LogWarning("[Editor] Деталь декора не найдена: " + path + " — пропущена.");
                return false;
            }

            var inst = (GameObject)PrefabUtility.InstantiatePrefab(asset, root);
            var t = inst.transform;
            t.localPosition = Vector3.zero;
            t.localScale = Vector3.one;

            bool isRear = def.Side == Side.Rear;
            float budget = isRear ? rearBudget : sideBudget;

            // Поворот: из {0, 90} берём тот, у которого глубина поперёк стены меньше; потом лёгкий джиттер.
            float jitter = (float)(rng.NextDouble() * 2.0 - 1.0) * YawJitter;
            float bestYaw = 0f;
            float bestDepth = float.MaxValue;
            foreach (float candidate in new[] { 0f, 90f })
            {
                t.localRotation = Quaternion.Euler(0f, candidate, 0f);
                if (!TryGetBounds(root, inst, out Bounds cb))
                {
                    Debug.LogWarning("[Editor] У детали '" + def.Name + "' нет подходящих Renderer'ов — пропущена.");
                    Object.DestroyImmediate(inst);
                    return false;
                }

                float depth = isRear ? cb.size.z : cb.size.x;
                if (depth < bestDepth - 0.001f)
                {
                    bestDepth = depth;
                    bestYaw = candidate;
                }
            }

            t.localRotation = Quaternion.Euler(0f, bestYaw + jitter, 0f);
            TryGetBounds(root, inst, out Bounds b);

            // Не влезает в бюджет стороны — аварийное уменьшение (не ниже MinShrink).
            float outDepth = isRear ? b.size.z : b.size.x;
            float scale = 1f;
            if (outDepth > budget && outDepth > 0.0001f)
            {
                scale = Mathf.Max(budget / outDepth, MinShrink);
                t.localScale = Vector3.one * scale;
                TryGetBounds(root, inst, out b);
                Debug.LogWarning("[Editor] Деталь '" + def.Name + "' глубиной " + F2(outDepth) + " м не влезает в бюджет " +
                                 F2(budget) + " м — уменьшена до scale " + F2(scale) + ".");
            }

            float alongSize = isRear ? b.size.x : b.size.z;
            float alongLimit = isRear ? hx : hz;

            // Желаемый центр вдоль стены, затем сдвиг вперёд, чтобы не наехать на предыдущую деталь.
            float desired = def.T * alongLimit;
            float minCenter = cursors[def.Side] + Gap + alongSize * 0.5f;
            float center = Mathf.Max(desired, float.IsNegativeInfinity(cursors[def.Side]) ? desired : minCenter);

            // Деталь целиком в пределах стены (тыл — по ширине ангара, бока — по глубине ангара, не заходя вперёд).
            float maxCenter = alongLimit - alongSize * 0.5f;
            float minAllowed = -alongLimit + alongSize * 0.5f;
            if (center > maxCenter)
            {
                Debug.LogWarning("[Editor] Деталь '" + def.Name + "': не хватает места вдоль стены (" + def.Side +
                                 ") — прижата к краю.");
                center = maxCenter;
            }
            center = Mathf.Max(center, minAllowed);
            cursors[def.Side] = center + alongSize * 0.5f;

            // Поперёк стены — вплотную с зазором Gap; низ на y=0.
            float x;
            float z;
            switch (def.Side)
            {
                case Side.Left:
                    x = -(hx + Gap + b.size.x * 0.5f);
                    z = center;
                    break;
                case Side.Right:
                    x = hx + Gap + b.size.x * 0.5f;
                    z = center;
                    break;
                default:
                    x = center;
                    z = -(hz + Gap + b.size.z * 0.5f);
                    break;
            }

            t.localPosition = new Vector3(x - b.center.x, -b.min.y, z - b.center.z);
            TryGetBounds(root, inst, out b);

            Debug.Log("[Editor] Декор '" + def.Name + "' (" + def.Side + "): центр (" + F2(b.center.x) + ", " +
                      F2(b.center.z) + "), размер " + V(b.size) + ", yaw " + F2(bestYaw + jitter) + ", scale " + F2(scale) + ".");
            return true;
        }

        // ── Утилиты ─────────────────────────────────────────────────────────────

        private static string F2(float value) => value.ToString("F2", CultureInfo.InvariantCulture);

        private static string V(Vector3 v) => "(" + F2(v.x) + ", " + F2(v.y) + ", " + F2(v.z) + ")";

        /// <summary>
        /// Bounds всех Renderer'ов <paramref name="target"/> в локальных осях <paramref name="space"/>
        /// (частицы и TMP-тексты не учитываются — как в FitVisual, иначе измерение разойдётся со сценой).
        /// </summary>
        private static bool TryGetBounds(Transform space, GameObject target, out Bounds bounds)
        {
            bounds = default;
            bool has = false;
            Matrix4x4 worldToSpace = space.worldToLocalMatrix;

            foreach (var r in target.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer || r.GetComponent<TMP_Text>() != null)
                    continue;

                Matrix4x4 toSpace;
                Bounds source;
                if (r is SkinnedMeshRenderer)
                {
                    toSpace = worldToSpace;
                    source = r.bounds;
                }
                else
                {
                    toSpace = worldToSpace * r.transform.localToWorldMatrix;
                    source = r.localBounds;
                }

                Vector3 c = source.center;
                Vector3 e = source.extents;
                for (int i = 0; i < 8; i++)
                {
                    var corner = c + new Vector3((i & 1) == 0 ? -e.x : e.x,
                                                 (i & 2) == 0 ? -e.y : e.y,
                                                 (i & 4) == 0 ? -e.z : e.z);
                    Vector3 p = toSpace.MultiplyPoint3x4(corner);
                    if (!has)
                    {
                        bounds = new Bounds(p, Vector3.zero);
                        has = true;
                    }
                    else
                    {
                        bounds.Encapsulate(p);
                    }
                }
            }

            return has;
        }

        /// <summary>Создаёт цепочку папок "Assets/A/B/C", если каких-то нет.</summary>
        private static void EnsureFolderPath(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
                return;

            var segments = new List<string>(folder.Split('/'));
            string current = segments[0];
            for (int i = 1; i < segments.Count; i++)
            {
                string next = current + "/" + segments[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, segments[i]);
                    Debug.Log("[Editor] Создана папка: " + next);
                }
                current = next;
            }
        }
    }
}
#endif
