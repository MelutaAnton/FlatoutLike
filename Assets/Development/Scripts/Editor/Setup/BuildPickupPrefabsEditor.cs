#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using FlatOutLike.Controllers.Pickups;
using FlatOutLike.Models;

namespace FlatOutLike.Editor.Setup
{
    /// <summary>
    /// Создаёт или обновляет префабы пикапов:
    ///   Assets/Development/Prefabs/Pickups/Pickup_Repair.prefab
    ///   Assets/Development/Prefabs/Pickups/Pickup_Nitro.prefab
    ///
    /// Структура каждого префаба:
    ///   - Слой Pickup (Layers.Pickup)
    ///   - MeshFilter/MeshRenderer со сферой и цветным URP/Lit материалом
    ///   - SphereCollider(isTrigger=true, radius=1.5) — зона подбора
    ///   - Rigidbody(isKinematic=true, useGravity=false) — КРИТИЧНО:
    ///     OnTriggerEnter не срабатывает без Rigidbody ни на одном из участников (CLAUDE.md)
    ///   - RepairPickup / NitroPickup компонент
    ///
    /// Идемпотентность: если prefab уже существует — компоненты обновляются через
    /// LoadPrefabContents; GUID ассета не меняется.
    ///
    /// Запускать ПОСЛЕ:
    ///   FlatOutLike/Setup/Create Physics Layers
    /// Запускать ДО:
    ///   FlatOutLike/Configs/Create Default PickupConfig
    /// </summary>
    public static class BuildPickupPrefabsEditor
    {
        private const string RepairPrefabPath   = "Assets/Development/Prefabs/Pickups/Pickup_Repair.prefab";
        private const string NitroPrefabPath    = "Assets/Development/Prefabs/Pickups/Pickup_Nitro.prefab";
        private const string RepairMaterialPath = "Assets/Development/Art/Materials/Pickups/Pickup_Repair.mat";
        private const string NitroMaterialPath  = "Assets/Development/Art/Materials/Pickups/Pickup_Nitro.mat";

        private const float TriggerRadius = 1.5f;

        [MenuItem("FlatOutLike/Setup/Build Pickup Prefabs")]
        public static void BuildPickupPrefabs()
        {
            Undo.SetCurrentGroupName("Build Pickup Prefabs");

            // ── Папки ────────────────────────────────────────────────────────
            // Prefabs/Pickups уже должна существовать после Create Physics Layers,
            // но страхуемся на случай если запускают впервые.
            EnsureFolder("Assets/Development/Prefabs", "Pickups");
            EnsureFolder("Assets/Development/Art/Materials", "Pickups");

            // ── Материалы ─────────────────────────────────────────────────────
            var repairMat = EnsureMaterial(RepairMaterialPath, new Color(0.15f, 0.75f, 0.15f)); // зелёный
            var nitroMat  = EnsureMaterial(NitroMaterialPath,  new Color(0.2f,  0.35f, 0.9f));  // синий

            // ── Проверяем слой Pickup ─────────────────────────────────────────
            int pickupLayer = LayerMask.NameToLayer(Layers.Pickup);
            if (pickupLayer < 0)
                Debug.LogWarning("[Editor] Слой '" + Layers.Pickup + "' не найден. " +
                                 "Запусти 'FlatOutLike/Setup/Create Physics Layers' сначала. " +
                                 "Слой не будет назначен префабам.");

            // ── Создаём или обновляем оба префаба ───────────────────────────
            BuildOrUpdatePrefab<RepairPickup>(RepairPrefabPath, "Pickup_Repair", repairMat, pickupLayer);
            BuildOrUpdatePrefab<NitroPickup>(NitroPrefabPath,  "Pickup_Nitro",  nitroMat,  pickupLayer);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[Editor] Build Pickup Prefabs завершён: Pickup_Repair.prefab и Pickup_Nitro.prefab готовы.");
        }

        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Создаёт новый или обновляет существующий pickup-префаб.
        /// Если ассет существует — использует LoadPrefabContents (GUID не меняется).
        /// Если нет — создаёт временный сцено-объект, сохраняет как ассет, уничтожает.
        /// </summary>
        private static void BuildOrUpdatePrefab<TPickup>(
            string prefabPath, string goName, Material material, int pickupLayer)
            where TPickup : MonoBehaviour
        {
            bool exists = System.IO.File.Exists(
                System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), prefabPath));

            if (exists)
            {
                // ── Обновляем существующий через LoadPrefabContents ────────
                var prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
                if (prefabRoot == null)
                {
                    Debug.LogError("[Editor] Не удалось загрузить " + prefabPath);
                    return;
                }
                try
                {
                    ConfigurePickupRoot<TPickup>(prefabRoot, material, pickupLayer);
                    PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
                    Debug.Log("[Editor] Обновлён префаб: " + prefabPath);
                }
                finally
                {
                    // UnloadPrefabContents ОБЯЗАТЕЛЕН в finally — уничтожает preview-сцену
                    PrefabUtility.UnloadPrefabContents(prefabRoot);
                }
            }
            else
            {
                // ── Создаём новый через временный сцено-объект ────────────
                // CreatePrimitive(Sphere) даёт MeshFilter + MeshRenderer + SphereCollider
                var tempGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                tempGo.name = goName;
                try
                {
                    ConfigurePickupRoot<TPickup>(tempGo, material, pickupLayer);
                    PrefabUtility.SaveAsPrefabAsset(tempGo, prefabPath);
                    Debug.Log("[Editor] Создан префаб: " + prefabPath);
                }
                finally
                {
                    // DestroyImmediate: убираем временный объект из сцены
                    Object.DestroyImmediate(tempGo);
                }
            }
        }

        /// <summary>
        /// Настраивает GameObject как пикап: слой, материал, коллайдер, Rigidbody, компонент.
        /// Идемпотентно — каждый компонент добавляется только если его нет.
        /// </summary>
        private static void ConfigurePickupRoot<TPickup>(
            GameObject go, Material material, int pickupLayer)
            where TPickup : MonoBehaviour
        {
            // Слой Pickup
            if (pickupLayer >= 0)
                go.layer = pickupLayer;

            // MeshRenderer: назначаем материал (у сферы-примитива уже есть)
            var rend = go.GetComponent<MeshRenderer>();
            if (rend != null && material != null)
                rend.sharedMaterial = material;

            // SphereCollider: isTrigger=true, radius=1.5
            // CreatePrimitive добавляет SphereCollider(radius=0.5) — перенастраиваем
            var col = go.GetComponent<SphereCollider>();
            if (col == null)
                col = go.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius    = TriggerRadius;

            // Rigidbody: kinematic — КРИТИЧНО для OnTriggerEnter/Exit (CLAUDE.md §Physics)
            var rb = go.GetComponent<Rigidbody>();
            if (rb == null)
                rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity  = false;

            // Компонент пикапа (RepairPickup / NitroPickup)
            if (go.GetComponent<TPickup>() == null)
            {
                go.AddComponent<TPickup>();
                Debug.Log("[Editor] Добавлен компонент " + typeof(TPickup).Name + " на " + go.name + ".");
            }
        }

        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Загружает существующий URP/Lit материал или создаёт новый с указанным цветом.
        /// </summary>
        private static Material EnsureMaterial(string path, Color color)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                Debug.Log("[Editor] Материал уже существует: " + path);
                return existing;
            }

            // Пытаемся найти URP/Lit, fallback на Standard
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogWarning("[Editor] Шейдер 'Universal Render Pipeline/Lit' не найден — " +
                                 "используем Standard. Переназначь шейдер вручную если нужен URP.");
                shader = Shader.Find("Standard");
            }

            var mat = new Material(shader) { name = System.IO.Path.GetFileNameWithoutExtension(path) };

            // _BaseColor — основной цвет URP/Lit; mat.color — fallback для Standard
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", color);
            else
                mat.color = color;

            AssetDatabase.CreateAsset(mat, path);
            Debug.Log("[Editor] Создан материал: " + path + " цвет=" + color);
            return mat;
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
