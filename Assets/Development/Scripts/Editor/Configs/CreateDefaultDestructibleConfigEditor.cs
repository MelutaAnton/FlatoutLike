#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using FlatOutLike.Controllers.Destructible;
using FlatOutLike.Data.Configs.Destructible;

namespace FlatOutLike.Editor.Configs
{
    /// <summary>
    /// Создаёт или обновляет Assets/Development/Data/Configs/Destructible/DestructibleConfig.asset
    /// и привязывает его к Hazard_Fence.prefab.
    ///
    /// Шаг 1 — DestructibleConfig.asset:
    ///   _breakImpulseThreshold = 10   (импульс удара для разрушения)
    ///   _debrisPrefabs[3]      = [Debris_Plank_01, Debris_Plank_02, Debris_Scrap_01]
    ///   _debrisPieceCount      = 4    (штук обломков при разрушении)
    ///   _debrisLifetime        = 5    (секунды до возврата в пул)
    ///   _debrisScatterForce    = 6    (скорость разлёта обломков, м/с — ForceMode.VelocityChange)
    ///   _debrisPoolSize        = 8    (размер пула обломков)
    ///
    /// Шаг 2 — Hazard_Fence.prefab:
    ///   DestructibleObjectController._config = созданный DestructibleConfig.asset
    ///
    /// Идемпотентность: если ассет уже существует — поля перезаписываются, GUID сохраняется.
    ///
    /// Запускать ПОСЛЕ:
    ///   FlatOutLike/Setup/Build Hazard Prefabs  (нужны Debris_*.prefab и Hazard_Fence.prefab)
    /// Запускать ДО:
    ///   FlatOutLike/Setup/Populate Derby Arena Hazards
    /// </summary>
    public static class CreateDefaultDestructibleConfigEditor
    {
        private const string AssetPath   = "Assets/Development/Data/Configs/Destructible/DestructibleConfig.asset";
        private const string FolderRoot  = "Assets/Development/Data/Configs";
        private const string FolderDest  = "Assets/Development/Data/Configs/Destructible";

        // Пути обломков — создаются скриптом Build Hazard Prefabs
        private const string Plank01Path  = "Assets/Development/Prefabs/Environment/Debris_Plank_01.prefab";
        private const string Plank02Path  = "Assets/Development/Prefabs/Environment/Debris_Plank_02.prefab";
        private const string Scrap01Path  = "Assets/Development/Prefabs/Environment/Debris_Scrap_01.prefab";

        // Hazard_Fence.prefab — привязываем конфиг в его DestructibleObjectController._config
        private const string FencePrefabPath = "Assets/Development/Prefabs/Environment/Hazard_Fence.prefab";

        [MenuItem("FlatOutLike/Configs/Create Default DestructibleConfig")]
        public static void CreateDefaultDestructibleConfig()
        {
            Undo.SetCurrentGroupName("Create Default DestructibleConfig");

            // ── 1. Папки ─────────────────────────────────────────────────────
            EnsureFolder("Assets/Development/Data", "Configs");
            EnsureFolder(FolderRoot, "Destructible");

            // ── 2. Создаём или загружаем ассет (идемпотентно по пути) ─────────
            var config = AssetDatabase.LoadAssetAtPath<DestructibleConfig>(AssetPath);
            bool isNew = config == null;

            if (isNew)
            {
                config = ScriptableObject.CreateInstance<DestructibleConfig>();
                AssetDatabase.CreateAsset(config, AssetPath);
                AssetDatabase.SaveAssets();
                Undo.RegisterCreatedObjectUndo(config, "Create DestructibleConfig");
                Debug.Log("[Editor] Создан DestructibleConfig.asset: " + AssetPath);
            }
            else
            {
                Debug.Log("[Editor] DestructibleConfig.asset уже существует — обновляем поля.");
            }

            // ── 3. Загружаем ссылки на debris-префабы ─────────────────────────
            var plank01 = AssetDatabase.LoadAssetAtPath<GameObject>(Plank01Path);
            if (plank01 == null)
                Debug.LogWarning("[Editor] Debris_Plank_01.prefab не найден по " + Plank01Path +
                                 ". Запусти 'FlatOutLike/Setup/Build Hazard Prefabs' сначала.");

            var plank02 = AssetDatabase.LoadAssetAtPath<GameObject>(Plank02Path);
            if (plank02 == null)
                Debug.LogWarning("[Editor] Debris_Plank_02.prefab не найден по " + Plank02Path +
                                 ". Запусти 'FlatOutLike/Setup/Build Hazard Prefabs' сначала.");

            var scrap01 = AssetDatabase.LoadAssetAtPath<GameObject>(Scrap01Path);
            if (scrap01 == null)
                Debug.LogWarning("[Editor] Debris_Scrap_01.prefab не найден по " + Scrap01Path +
                                 ". Запусти 'FlatOutLike/Setup/Build Hazard Prefabs' сначала.");

            // ── 4. Заполняем поля через SerializedObject ──────────────────────
            var so = new SerializedObject(config);

            // Числовые параметры разрушения
            so.FindProperty("_breakImpulseThreshold").floatValue = 10f;
            so.FindProperty("_debrisPieceCount").intValue        = 4;
            so.FindProperty("_debrisLifetime").floatValue        = 5f;
            so.FindProperty("_debrisScatterForce").floatValue    = 6f; // м/с, ForceMode.VelocityChange
            so.FindProperty("_debrisPoolSize").intValue          = 8;

            // Массив debris-префабов: Plank_01, Plank_02, Scrap_01
            var debrisProp = so.FindProperty("_debrisPrefabs");
            debrisProp.arraySize = 3;
            debrisProp.GetArrayElementAtIndex(0).objectReferenceValue = plank01;
            debrisProp.GetArrayElementAtIndex(1).objectReferenceValue = plank02;
            debrisProp.GetArrayElementAtIndex(2).objectReferenceValue = scrap01;

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();

            Debug.Log("[Editor] DestructibleConfig заполнен: " +
                      "_breakImpulseThreshold=10, _debrisPieceCount=4, _debrisLifetime=5, " +
                      "_debrisScatterForce=6, _debrisPoolSize=8, " +
                      "_debrisPrefabs[3]: " +
                      (plank01 != null ? plank01.name : "NULL") + " / " +
                      (plank02 != null ? plank02.name : "NULL") + " / " +
                      (scrap01 != null ? scrap01.name : "NULL") + ".");

            // ── 5. Привязываем конфиг к Hazard_Fence.prefab ───────────────────
            // Открываем prefab-contents в изолированной preview-сцене, находим
            // DestructibleObjectController и выставляем _config → наш ассет.
            WireConfigToFencePrefab(config);
        }

        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Загружает Hazard_Fence.prefab через LoadPrefabContents, находит
        /// DestructibleObjectController и присваивает его _config = config.
        /// Паттерн: LoadPrefabContents → изменения → SaveAsPrefabAsset → UnloadPrefabContents.
        /// </summary>
        private static void WireConfigToFencePrefab(DestructibleConfig config)
        {
            bool fenceExists = System.IO.File.Exists(
                System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), FencePrefabPath));

            if (!fenceExists)
            {
                Debug.LogWarning("[Editor] Hazard_Fence.prefab не найден по " + FencePrefabPath +
                                 ". Запусти 'FlatOutLike/Setup/Build Hazard Prefabs' сначала — " +
                                 "DestructibleConfig._config не будет привязан.");
                return;
            }

            var fenceRoot = PrefabUtility.LoadPrefabContents(FencePrefabPath);
            if (fenceRoot == null)
            {
                Debug.LogError("[Editor] Не удалось загрузить содержимое " + FencePrefabPath);
                return;
            }

            try
            {
                var destructible = fenceRoot.GetComponent<DestructibleObjectController>();
                if (destructible == null)
                {
                    Debug.LogWarning("[Editor] DestructibleObjectController не найден на Hazard_Fence.prefab. " +
                                     "Возможно, рантайм-скрипт ещё не добавлен или Build Hazard Prefabs не запущен.");
                }
                else
                {
                    // Выставляем _config через SerializedObject — тот же паттерн что в FillGameLifetimeScopeRefsEditor
                    var fenceSo = new SerializedObject(destructible);
                    fenceSo.FindProperty("_config").objectReferenceValue = config;
                    fenceSo.ApplyModifiedProperties();

                    Debug.Log("[Editor] Hazard_Fence.prefab: " +
                              "DestructibleObjectController._config → DestructibleConfig привязан.");
                }

                PrefabUtility.SaveAsPrefabAsset(fenceRoot, FencePrefabPath);
                Debug.Log("[Editor] Hazard_Fence.prefab сохранён с обновлённым _config.");
            }
            finally
            {
                // UnloadPrefabContents ОБЯЗАТЕЛЕН в finally — уничтожает preview-сцену
                PrefabUtility.UnloadPrefabContents(fenceRoot);
            }
        }

        // ─────────────────────────────────────────────────────────────────────

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
