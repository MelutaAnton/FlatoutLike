using FlatOutLike.Controllers.Car;
using FlatOutLike.Controllers.Destructible;
using FlatOutLike.Data.Configs.Destructible;
using FlatOutLike.Editor.Tools;
using UnityEditor;
using UnityEngine;

namespace FlatOutLike.Editor.Setup
{
    /// <summary>
    /// Настройка разбивающегося лобового стекла (Шаг 6, полишинг):
    ///   1. Создаёт префаб осколка Debris_GlassShard (квад + материал стекла из пака машин
    ///      ПО ССЫЛКЕ, не копированием) в Development/Prefabs/Environment/.
    ///   2. Создаёт/обновляет DestructibleWindshield_Config.asset.
    ///   3. Вешает WindshieldController на ВСЕ префабы машин и прописывает конфиг.
    /// Список машин НЕ захардкожен — определяется через
    /// <see cref="CarPrefabDiscovery.DiscoverCarPrefabPaths"/> (все .prefab в
    /// Assets/Development/Prefabs/Vehicles/ с CarController на корне). Новые машины
    /// подхватываются автоматически без правки этого файла.
    /// Идемпотентно: повторный запуск не дублирует объекты, поля перезаписываются.
    /// </summary>
    public static class SetupWindshieldShatterEditor
    {
        private const string ShardPrefabPath = "Assets/Development/Prefabs/Environment/Debris_GlassShard.prefab";
        private const string ConfigPath      = "Assets/Development/Data/Configs/Destructible/DestructibleWindshield_Config.asset";
        private const string GlassMaterialPath =
            "Assets/ThirdParty/Low Poly Destructible Cars/Material/Sport_09_Glass.mat";

        // Габарит осколка (м) и лёгкая масса, чтобы осколки не пинали машины
        private const float ShardScale = 0.12f;
        private const float ShardMass  = 0.05f;

        [MenuItem("FlatOutLike/Setup/Setup Windshield Shatter (Step 6)")]
        public static void SetupWindshieldShatter()
        {
            var shardPrefab = BuildShardPrefab();
            var config      = CreateOrUpdateConfig(shardPrefab);
            AttachControllerToCarPrefabs(config);

            AssetDatabase.SaveAssets();
            Debug.Log("[Editor] Windshield Shatter настроен: осколок + конфиг + WindshieldController на машинах.");
        }

        // ── 1. Префаб осколка ────────────────────────────────────────────────
        private static GameObject BuildShardPrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(ShardPrefabPath);
            if (existing != null)
            {
                Debug.Log("[Editor] Debris_GlassShard.prefab уже существует — пропускаем создание.");
                return existing;
            }

            // Квад со стеклянным материалом пака. MeshCollider примитива заменяем на BoxCollider —
            // non-convex MeshCollider несовместим с динамическим Rigidbody.
            var temp = GameObject.CreatePrimitive(PrimitiveType.Quad);
            temp.name = "Debris_GlassShard";
            temp.transform.localScale = Vector3.one * ShardScale;

            var meshCollider = temp.GetComponent<MeshCollider>();
            if (meshCollider != null)
                Object.DestroyImmediate(meshCollider);

            var box = temp.AddComponent<BoxCollider>();
            box.size = new Vector3(1f, 1f, 0.05f);

            var glassMaterial = AssetDatabase.LoadAssetAtPath<Material>(GlassMaterialPath);
            if (glassMaterial != null)
                temp.GetComponent<MeshRenderer>().sharedMaterial = glassMaterial;
            else
                Debug.LogWarning("[Editor] Материал стекла не найден по " + GlassMaterialPath +
                                 " — осколок останется с дефолтным материалом.");

            // Зеркальный чайлд (scale Z = -1) рендерит обратную грань квада:
            // одностороннее стекло иначе «мигает» при вращении осколка.
            // Только MeshFilter + MeshRenderer — ни коллайдера, ни физики.
            var mirror = new GameObject("Debris_GlassShard_Back");
            mirror.transform.SetParent(temp.transform, false);
            mirror.transform.localScale = new Vector3(1f, 1f, -1f);
            mirror.AddComponent<MeshFilter>().sharedMesh =
                temp.GetComponent<MeshFilter>().sharedMesh;
            var mirrorRenderer = mirror.AddComponent<MeshRenderer>();
            mirrorRenderer.sharedMaterial = temp.GetComponent<MeshRenderer>().sharedMaterial;

            var rb = temp.AddComponent<Rigidbody>();
            rb.mass = ShardMass;

            var debris = temp.AddComponent<DebrisPieceController>();
            var so = new SerializedObject(debris);
            so.FindProperty("_rigidbody").objectReferenceValue = rb;
            so.ApplyModifiedProperties();

            var prefab = PrefabUtility.SaveAsPrefabAsset(temp, ShardPrefabPath);
            Object.DestroyImmediate(temp);

            Debug.Log("[Editor] Создан " + ShardPrefabPath + ".");
            return prefab;
        }

        // ── 2. Конфиг осколков ───────────────────────────────────────────────
        private static DestructibleConfig CreateOrUpdateConfig(GameObject shardPrefab)
        {
            var config = AssetDatabase.LoadAssetAtPath<DestructibleConfig>(ConfigPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<DestructibleConfig>();
                AssetDatabase.CreateAsset(config, ConfigPath);
                Debug.Log("[Editor] Создан " + ConfigPath + ".");
            }
            else
            {
                Debug.Log("[Editor] DestructibleWindshield_Config уже существует — обновляем поля.");
            }

            var so = new SerializedObject(config);
            // Порог удара не используется: Shatter вызывается программно, не коллизией
            so.FindProperty("_breakImpulseThreshold").floatValue = 999f;
            var prefabs = so.FindProperty("_debrisPrefabs");
            prefabs.arraySize = 1;
            prefabs.GetArrayElementAtIndex(0).objectReferenceValue = shardPrefab;
            so.FindProperty("_debrisPieceCount").intValue     = 12;
            so.FindProperty("_debrisLifetime").floatValue     = 3f;
            so.FindProperty("_debrisScatterForce").floatValue = 4f;
            so.FindProperty("_debrisPoolSize").intValue       = 24;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(config);

            return config;
        }

        // ── 3. WindshieldController на префабах машин ────────────────────────
        private static void AttachControllerToCarPrefabs(DestructibleConfig config)
        {
            var carPrefabPaths = CarPrefabDiscovery.DiscoverCarPrefabPaths();
            Debug.Log("[Editor] Setup Windshield Shatter: обнаружено машин — " + carPrefabPaths.Length + ".");

            foreach (var path in carPrefabPaths)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                {
                    Debug.LogWarning("[Editor] Префаб машины не найден: " + path + " — пропускаем.");
                    continue;
                }

                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var controller = root.GetComponent<WindshieldController>();
                    if (controller == null)
                        controller = root.AddComponent<WindshieldController>();

                    var so = new SerializedObject(controller);
                    so.FindProperty("_debrisConfig").objectReferenceValue = config;
                    so.ApplyModifiedProperties();

                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    Debug.Log("[Editor] WindshieldController навешан и настроен: " + path + ".");
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
        }
    }
}
