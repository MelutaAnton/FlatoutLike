#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using FlatOutLike.Controllers.Destructible;
using FlatOutLike.Models;

namespace FlatOutLike.Editor.Setup
{
    /// <summary>
    /// Создаёт наши prefab'ы hazard'ов в Assets/Development/Prefabs/Environment/.
    ///
    /// Структура каждого префаба: root с физикой/логикой + дочерний инстанс ThirdParty-префаба
    /// (prefab-link сохраняется через PrefabUtility.InstantiatePrefab; ThirdParty-файлы не редактируются).
    ///
    /// КРУПНЫЕ hazard'ы (слой Hazard — AI объезжает):
    ///   Hazard_Barrel         ← barrel_001.prefab,   Rigidbody(20),   BoxCollider
    ///   Hazard_ConcreteBlock  ← barrier_001.prefab,  Rigidbody(150),  BoxCollider
    ///   Hazard_Fence          ← fence_001.prefab,    статик,          BoxCollider + DestructibleObjectController
    ///                            _config = null (свяжет FlatOutLike/Configs/Create Default DestructibleConfig)
    ///
    /// ОБЛОМКИ для пула забора (слой Default):
    ///   Debris_Plank_01  ← deadwood_001.prefab,   Rigidbody(3), BoxCollider, DebrisPieceController
    ///   Debris_Plank_02  ← deadwood_005.prefab,   Rigidbody(3), BoxCollider, DebrisPieceController
    ///   Debris_Scrap_01  ← scrap_metal_001.prefab, Rigidbody(3), BoxCollider, DebrisPieceController
    ///
    /// МЕЛКИЕ ПРОПЫ (слой Default — AI таранит насквозь, физически разлетаются):
    ///   Hazard_Tire      ← tire_001.prefab,       Rigidbody(8),  MeshCollider(convex)
    ///   Hazard_Wheel     ← car_wheel_002.prefab,  Rigidbody(10), MeshCollider(convex)
    ///   Hazard_TireStack ← tires_001.prefab,      Rigidbody(20), CapsuleCollider
    ///   Hazard_Cactus_01 ← cactus_001.prefab,     Rigidbody(10), CapsuleCollider
    ///   Hazard_Cactus_02 ← cactus_006.prefab,     Rigidbody(10), CapsuleCollider
    ///   Hazard_Cactus_03 ← cactus_011.prefab,     Rigidbody(10), CapsuleCollider
    ///
    /// Идемпотентность: если prefab уже существует по пути — пропускается (GUID не меняется).
    ///
    /// Примечание: Undo не применяется к операциям внутри prefab-asset контекста
    /// (объект не в сцене), поэтому используем только Debug.Log.
    ///
    /// Запускать ПОСЛЕ:
    ///   FlatOutLike/Setup/Create Physics Layers
    /// Запускать ДО:
    ///   FlatOutLike/Configs/Create Default DestructibleConfig
    ///   FlatOutLike/Setup/Populate Derby Arena Hazards
    /// </summary>
    public static class BuildHazardPrefabsEditor
    {
        private const string Junkyard = "Assets/ThirdParty/Junkyard_1/Prefabs/";
        private const string OutPath  = "Assets/Development/Prefabs/Environment/";

        // ─────────── Пути ThirdParty-источников ───────────────────────────────
        private const string BarrelSrc        = Junkyard + "barrel_001.prefab";
        private const string BarrierSrc       = Junkyard + "barrier_001.prefab";
        private const string FenceSrc         = Junkyard + "fence_001.prefab";
        private const string Deadwood01Src    = Junkyard + "deadwood_001.prefab";
        private const string Deadwood05Src    = Junkyard + "deadwood_005.prefab";
        private const string ScrapMetal01Src  = Junkyard + "scrap_metal_001.prefab";
        private const string TireSrc          = Junkyard + "tire_001.prefab";
        private const string WheelSrc         = Junkyard + "car_wheel_002.prefab";
        private const string TireStackSrc     = Junkyard + "tires_001.prefab";
        private const string Cactus01Src      = Junkyard + "cactus_001.prefab";
        private const string Cactus06Src      = Junkyard + "cactus_006.prefab";
        private const string Cactus11Src      = Junkyard + "cactus_011.prefab";

        [MenuItem("FlatOutLike/Setup/Build Hazard Prefabs")]
        public static void BuildHazardPrefabs()
        {
            EnsureFolder("Assets/Development/Prefabs", "Environment");

            int hazardLayer  = GetLayer(Layers.Hazard);
            int defaultLayer = 0; // Default — стандартный слой Unity id=0

            // ── Крупные hazard'ы (слой Hazard) ────────────────────────────────
            BuildPhysicsHazardPrefab(
                "Hazard_Barrel",        BarrelSrc,
                mass: 20f,  ColliderKind.Box,  hazardLayer, hasRigidbody: true);

            BuildPhysicsHazardPrefab(
                "Hazard_ConcreteBlock", BarrierSrc,
                mass: 150f, ColliderKind.Box,  hazardLayer, hasRigidbody: true);

            BuildFencePrefab(hazardLayer);

            // ── Обломки для пула забора (слой Default) ─────────────────────────
            BuildDebrisPrefab("Debris_Plank_01", Deadwood01Src,   defaultLayer);
            BuildDebrisPrefab("Debris_Plank_02", Deadwood05Src,   defaultLayer);
            BuildDebrisPrefab("Debris_Scrap_01", ScrapMetal01Src, defaultLayer);

            // ── Мелкие пропы (слой Default) ────────────────────────────────────
            BuildPhysicsHazardPrefab(
                "Hazard_Tire",      TireSrc,
                mass: 8f,  ColliderKind.ConvexMesh, defaultLayer, hasRigidbody: true);

            BuildPhysicsHazardPrefab(
                "Hazard_Wheel",     WheelSrc,
                mass: 10f, ColliderKind.ConvexMesh, defaultLayer, hasRigidbody: true);

            BuildPhysicsHazardPrefab(
                "Hazard_TireStack", TireStackSrc,
                mass: 20f, ColliderKind.Capsule,    defaultLayer, hasRigidbody: true);

            BuildCactusPrefab("Hazard_Cactus_01", Cactus01Src, defaultLayer);
            BuildCactusPrefab("Hazard_Cactus_02", Cactus06Src, defaultLayer);
            BuildCactusPrefab("Hazard_Cactus_03", Cactus11Src, defaultLayer);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[Editor] Build Hazard Prefabs завершён: 12 префабов обработано. " +
                      "Следующий шаг: FlatOutLike/Configs/Create Default DestructibleConfig.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // Строители отдельных типов
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Создаёт простой физический hazard-префаб: root + ThirdParty visual child +
        /// опциональный Rigidbody + коллайдер указанного типа.
        /// Если prefab уже существует — пропускает (идемпотентно).
        /// </summary>
        private static void BuildPhysicsHazardPrefab(
            string prefabName, string thirdPartyPath,
            float mass, ColliderKind colliderKind,
            int layer, bool hasRigidbody)
        {
            string outPath = OutPath + prefabName + ".prefab";
            if (PrefabFileExists(outPath))
            {
                Debug.Log("[Editor] " + prefabName + ".prefab уже существует — пропускаем.");
                return;
            }

            var source = LoadThirdParty(thirdPartyPath, prefabName);
            if (source == null) return;

            var root = new GameObject(prefabName);
            try
            {
                // Вкладываем ThirdParty-префаб как child, сохраняем prefab-link
                var visual = InstantiateVisualChild(source, root);

                // Rigidbody на root (если нужен)
                if (hasRigidbody)
                {
                    var rb = root.AddComponent<Rigidbody>();
                    rb.mass = mass;
                }

                // Коллайдер на root
                AddCollider(root, visual, colliderKind);

                // Слой рекурсивно
                if (layer >= 0)
                    SetLayerRecursively(root, layer);

                PrefabUtility.SaveAsPrefabAsset(root, outPath);
                Debug.Log("[Editor] Создан " + prefabName + ".prefab" +
                          (hasRigidbody ? " (Rigidbody mass=" + mass + ")" : " (static)") +
                          ", слой=" + layer + " → " + outPath);
            }
            finally
            {
                // Уничтожаем временный сцено-объект
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// Создаёт Hazard_Fence: статик (без Rigidbody), BoxCollider, DestructibleObjectController.
        /// _config оставляется null — заполнит скрипт Create Default DestructibleConfig.
        /// _intactVisual = Visual child, _collider = BoxCollider на root.
        /// </summary>
        private static void BuildFencePrefab(int hazardLayer)
        {
            const string PrefabName = "Hazard_Fence";
            string outPath          = OutPath + PrefabName + ".prefab";

            if (PrefabFileExists(outPath))
            {
                Debug.Log("[Editor] Hazard_Fence.prefab уже существует — пропускаем.");
                return;
            }

            var source = LoadThirdParty(FenceSrc, PrefabName);
            if (source == null) return;

            var root = new GameObject(PrefabName);
            try
            {
                // Visual child (prefab-link сохраняется)
                var visual = InstantiateVisualChild(source, root);

                // BoxCollider по bounds визуала — статик, Rigidbody отсутствует
                // OnCollisionEnter сработает от Rigidbody машины при ударе
                var bounds = GetCombinedBounds(visual);
                var boxCol = root.AddComponent<BoxCollider>();
                boxCol.center = bounds.center - root.transform.position;
                boxCol.size   = bounds.size;

                // DestructibleObjectController — логика разрушения
                var destructible = root.AddComponent<DestructibleObjectController>();

                // Заполняем ссылки через SerializedObject
                // _config оставляем null — задаёт следующий скрипт (Create Default DestructibleConfig)
                var so = new SerializedObject(destructible);
                so.FindProperty("_intactVisual").objectReferenceValue = visual;
                so.FindProperty("_collider").objectReferenceValue     = boxCol;
                so.ApplyModifiedProperties();

                // Слой рекурсивно
                if (hazardLayer >= 0)
                    SetLayerRecursively(root, hazardLayer);

                PrefabUtility.SaveAsPrefabAsset(root, outPath);
                Debug.Log("[Editor] Создан Hazard_Fence.prefab " +
                          "(static, BoxCollider, DestructibleObjectController). " +
                          "_config = null → привяжет 'FlatOutLike/Configs/Create Default DestructibleConfig'.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// Создаёт debris-префаб: Rigidbody(3) + BoxCollider + DebrisPieceController.
        /// _rigidbody выставляется сразу через SerializedObject (не null при старте пула).
        /// </summary>
        private static void BuildDebrisPrefab(string prefabName, string thirdPartyPath, int layer)
        {
            string outPath = OutPath + prefabName + ".prefab";
            if (PrefabFileExists(outPath))
            {
                Debug.Log("[Editor] " + prefabName + ".prefab уже существует — пропускаем.");
                return;
            }

            var source = LoadThirdParty(thirdPartyPath, prefabName);
            if (source == null) return;

            var root = new GameObject(prefabName);
            try
            {
                var visual = InstantiateVisualChild(source, root);

                // Rigidbody — обломок физически симулируется в полёте
                var rb = root.AddComponent<Rigidbody>();
                rb.mass = 3f;

                // BoxCollider по bounds визуала
                var bounds = GetCombinedBounds(visual);
                var boxCol = root.AddComponent<BoxCollider>();
                boxCol.center = bounds.center - root.transform.position;
                boxCol.size   = bounds.size;

                // DebrisPieceController — контроллер поведения обломка в пуле
                var debrisPiece = root.AddComponent<DebrisPieceController>();

                // Сразу выставляем _rigidbody, чтобы контроллер не делал GetComponent в рантайме
                var so = new SerializedObject(debrisPiece);
                so.FindProperty("_rigidbody").objectReferenceValue = rb;
                so.ApplyModifiedProperties();

                if (layer >= 0)
                    SetLayerRecursively(root, layer);

                PrefabUtility.SaveAsPrefabAsset(root, outPath);
                Debug.Log("[Editor] Создан " + prefabName +
                          ".prefab (Rigidbody mass=3, BoxCollider, DebrisPieceController).");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// Создаёт кактус-проп: Rigidbody(10) + CapsuleCollider по Y-оси (высокий).
        /// </summary>
        private static void BuildCactusPrefab(string prefabName, string thirdPartyPath, int layer)
        {
            string outPath = OutPath + prefabName + ".prefab";
            if (PrefabFileExists(outPath))
            {
                Debug.Log("[Editor] " + prefabName + ".prefab уже существует — пропускаем.");
                return;
            }

            var source = LoadThirdParty(thirdPartyPath, prefabName);
            if (source == null) return;

            var root = new GameObject(prefabName);
            try
            {
                var visual = InstantiateVisualChild(source, root);

                var rb  = root.AddComponent<Rigidbody>();
                rb.mass = 10f;

                // CapsuleCollider по Y-оси — кактус высокий, вертикальный
                var bounds = GetCombinedBounds(visual);
                var cap    = root.AddComponent<CapsuleCollider>();
                cap.direction = 1; // Y-axis
                cap.height    = bounds.size.y;
                cap.radius    = Mathf.Max(bounds.extents.x, bounds.extents.z) * 0.5f;
                cap.center    = new Vector3(0f, bounds.extents.y, 0f);

                if (layer >= 0)
                    SetLayerRecursively(root, layer);

                PrefabUtility.SaveAsPrefabAsset(root, outPath);
                Debug.Log("[Editor] Создан " + prefabName +
                          ".prefab (Rigidbody mass=10, CapsuleCollider Y-axis).");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Вспомогательные методы
        // ─────────────────────────────────────────────────────────────────────

        private enum ColliderKind { Box, ConvexMesh, Capsule }

        /// <summary>
        /// Добавляет коллайдер указанного типа на root, используя данные из visual-child.
        /// При отсутствии MeshFilter для ConvexMesh падает обратно на Box.
        /// </summary>
        private static void AddCollider(GameObject root, GameObject visual, ColliderKind kind)
        {
            switch (kind)
            {
                case ColliderKind.Box:
                {
                    var b   = GetCombinedBounds(visual);
                    var col = root.AddComponent<BoxCollider>();
                    col.center = b.center - root.transform.position;
                    col.size   = b.size;
                    break;
                }

                case ColliderKind.ConvexMesh:
                {
                    // Convex MeshCollider на root ссылается на первый меш из visual-child
                    var mf = visual.GetComponentInChildren<MeshFilter>(true);
                    if (mf != null && mf.sharedMesh != null)
                    {
                        var mc     = root.AddComponent<MeshCollider>();
                        mc.sharedMesh = mf.sharedMesh;
                        mc.convex  = true;
                    }
                    else
                    {
                        // Fallback: BoxCollider по bounds (MeshFilter не найден)
                        Debug.LogWarning("[Editor] MeshFilter не найден в '" + visual.name +
                                         "' — ставим BoxCollider вместо convex MeshCollider.");
                        var b   = GetCombinedBounds(visual);
                        var col = root.AddComponent<BoxCollider>();
                        col.center = b.center - root.transform.position;
                        col.size   = b.size;
                    }
                    break;
                }

                case ColliderKind.Capsule:
                {
                    var b   = GetCombinedBounds(visual);
                    var col = root.AddComponent<CapsuleCollider>();
                    col.direction = 1; // Y-axis
                    col.height    = b.size.y;
                    col.radius    = Mathf.Max(b.extents.x, b.extents.z) * 0.5f;
                    col.center    = new Vector3(0f, b.extents.y, 0f);
                    break;
                }
            }
        }

        /// <summary>
        /// Инстанциирует ThirdParty-префаб как дочерний объект root, сохраняя prefab-link.
        /// Называет child "Visual" для консистентности иерархии.
        /// </summary>
        private static GameObject InstantiateVisualChild(GameObject sourcePrefab, GameObject root)
        {
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(sourcePrefab, root.transform);
            visual.name = "Visual";
            return visual;
        }

        /// <summary>
        /// Вычисляет объединённые AABB всех Renderer'ов в иерархии go (в world-space).
        /// Fallback — единичный куб в начале координат.
        /// </summary>
        private static Bounds GetCombinedBounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                Debug.LogWarning("[Editor] Renderer'ы не найдены в '" + go.name +
                                 "' — используем Bounds(zero, one) как fallback для коллайдера.");
                return new Bounds(Vector3.zero, Vector3.one);
            }

            var b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                b.Encapsulate(renderers[i].bounds);
            return b;
        }

        /// <summary>
        /// Загружает ThirdParty-префаб. Логирует предупреждение и возвращает null если не найден.
        /// </summary>
        private static GameObject LoadThirdParty(string path, string prefabName)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (source == null)
                Debug.LogWarning("[Editor] ThirdParty-ассет не найден: " + path +
                                 " — пропускаем создание " + prefabName + ".prefab. " +
                                 "Проверь импорт пака Junkyard_1.");
            return source;
        }

        /// <summary>
        /// Возвращает индекс слоя; логирует предупреждение если слой не зарегистрирован.
        /// </summary>
        private static int GetLayer(string layerName)
        {
            int id = LayerMask.NameToLayer(layerName);
            if (id < 0)
                Debug.LogWarning("[Editor] Слой '" + layerName + "' не зарегистрирован. " +
                                 "Запусти 'FlatOutLike/Setup/Create Physics Layers' сначала — " +
                                 "без него AI не будет видеть hazard'ы как препятствия.");
            return id;
        }

        private static bool PrefabFileExists(string assetPath) =>
            System.IO.File.Exists(
                System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), assetPath));

        private static void SetLayerRecursively(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform)
                SetLayerRecursively(child.gameObject, layer);
        }

        private static void EnsureFolder(string parent, string folderName)
        {
            string full = parent + "/" + folderName;
            if (!AssetDatabase.IsValidFolder(full))
            {
                AssetDatabase.CreateFolder(parent, folderName);
                Debug.Log("[Editor] Создана папка: " + full);
            }
        }
    }
}
#endif
