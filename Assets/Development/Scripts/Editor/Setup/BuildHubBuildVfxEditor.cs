#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using FlatOutLike.Controllers.Vfx;
using FlatOutLike.Data.Configs.Vfx;
using FlatOutLike.Installers;
using FlatOutLike.Models;

namespace FlatOutLike.Editor.Setup
{
    /// <summary>
    /// Шаг 12b: VFX стройки BaseHub.
    ///   1. Создаёт два префаба-ВАРИАНТА (Prefab Variant) поверх ThirdParty-эффектов, если их ещё нет:
    ///      VFX_BuildStageComplete (пыль / poof) и VFX_BuildingCompleted (фейерверк / падающие звёзды).
    ///      Берётся первый кандидат, у которого ParticleSystem на корне И у всех включённых
    ///      ParticleSystemRenderer есть материал (пак мог быть импортирован не целиком).
    ///   2. Настраивает вариант для пула (как SetupVfxPrefabsEditor): все ParticleSystem loop = false,
    ///      playOnAwake = false, корневой stopAction = Callback, ParticleController на корне +
    ///      _particleSystem. Все MonoBehaviour иерархии, кроме FlatOutLike.*, выключаются (enabled = false):
    ///      сторонние компоненты эффектов (CFXR_Effect и т.п.) могут сами уничтожать/выключать объект и
    ///      ломать возврат в пул. Имена выключенных компонентов пишутся в лог.
    ///   3. Дописывает в ОТДЕЛЬНЫЙ VfxConfig_Hub.asset (создаёт, если нет) записи для
    ///      VfxEffectType.BuildStageComplete (poolSize 3) и BuildingCompleted (poolSize 2), если их нет.
    ///      Из общего VfxConfig.asset записи этих двух типов удаляются (прочие не трогаются) — иначе hub-VFX
    ///      прогревались бы во всех сценах, где прошит общий конфиг.
    ///   4. Если активная сцена — BaseHub, прошивает GameLifetimeScope._vfxConfig на VfxConfig_Hub
    ///      (null не присваивается). Другая активная сцена — LogWarning и пропуск (OpenScene/диалогов нет).
    ///
    /// Существующий вариант с отсутствующим материалом удаляется и пересобирается из валидного кандидата;
    /// валидные существующие варианты не перезаписываются. Источник ThirdParty не читается и не меняется
    /// (вариант ссылается на него; правки — только на варианте).
    /// </summary>
    public static class BuildHubBuildVfxEditor
    {
        private const string VfxPrefabsFolder = "Assets/Development/Prefabs/VFX";
        private const string StageCompletePath = VfxPrefabsFolder + "/VFX_BuildStageComplete.prefab";
        private const string BuildingCompletedPath = VfxPrefabsFolder + "/VFX_BuildingCompleted.prefab";
        private const string SharedVfxConfigPath = "Assets/Development/Data/Configs/Vfx/VfxConfig.asset";

        /// <summary>Отдельный VFX-конфиг BaseHub — единственное место пути.</summary>
        public const string HubVfxConfigPath = "Assets/Development/Data/Configs/Vfx/VfxConfig_Hub.asset";

        private const string CfxrRoot = "Assets/ThirdParty/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/";

        private static readonly string[] StageCompleteCandidates =
        {
            "Assets/ThirdParty/Battle_Stadium/VFX/FX_Prefabs/FX_Dust.prefab",
            CfxrRoot + "Misc/CFXR Magic Poof.prefab",
        };

        private static readonly string[] BuildingCompletedCandidates =
        {
            CfxrRoot + "Explosions/CFXR4 Firework 1 Cyan-Purple (HDR).prefab",
            CfxrRoot + "Magic Misc/CFXR4 Falling Stars.prefab",
        };

        private const int StageCompletePoolSize = 3;
        private const int BuildingCompletedPoolSize = 2;

        private const string OwnNamespaceRoot = "FlatOutLike";

        [MenuItem("FlatOutLike/Setup/Build Hub Build VFX")]
        public static void BuildHubBuildVfx()
        {
            var stageComplete = EnsureVariant(StageCompletePath, StageCompleteCandidates);
            var buildingCompleted = EnsureVariant(BuildingCompletedPath, BuildingCompletedCandidates);

            EnsureHubConfigEntries(stageComplete, buildingCompleted);
            RemoveHubEntriesFromSharedConfig();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            WireVfxConfigInActiveScene();

            Debug.Log("[Editor] Build Hub Build VFX завершён.");
        }

        // ── Варианты префабов ───────────────────────────────────────────────────

        /// <summary>
        /// Возвращает префаб-вариант: существующий валидный как есть, иначе (нет / битый материал) создаёт
        /// из первого пригодного кандидата.
        /// </summary>
        private static GameObject EnsureVariant(string variantPath, string[] candidates)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(variantPath);
            if (existing != null)
            {
                if (HasValidMaterials(existing))
                {
                    Debug.Log("[Editor] Вариант VFX уже существует: " + variantPath + " — не трогаем.");
                    return existing;
                }

                // Битый материал (пак импортирован не целиком) — вариант бесполезен, пересобираем.
                Debug.LogWarning("[Editor] Вариант VFX " + variantPath +
                                 " с отсутствующим материалом — удаляем и пересобираем.");
                if (!AssetDatabase.DeleteAsset(variantPath))
                {
                    Debug.LogError("[Editor] Не удалось удалить битый вариант: " + variantPath);
                    return null;
                }

                Debug.Log("[Editor] Удалён битый вариант VFX: " + variantPath);
            }

            GameObject source = null;
            string sourcePath = null;
            foreach (var candidate in candidates)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(candidate);
                if (prefab == null)
                {
                    Debug.LogWarning("[Editor] Кандидат VFX не найден: " + candidate);
                    continue;
                }

                if (prefab.GetComponent<ParticleSystem>() == null)
                {
                    Debug.LogWarning("[Editor] Кандидат VFX без ParticleSystem на корне: " + candidate);
                    continue;
                }

                if (!HasValidMaterials(prefab))
                {
                    Debug.LogWarning("[Editor] Кандидат VFX с отсутствующим материалом (пак импортирован не целиком): " +
                                     candidate + " — следующий кандидат.");
                    continue;
                }

                source = prefab;
                sourcePath = candidate;
                break;
            }

            if (source == null)
            {
                Debug.LogWarning("[Editor] Для " + variantPath + " не нашлось пригодного кандидата — вариант не создан.");
                return null;
            }

            // Шаг 1: сохраняем чистый вариант (инстанс источника в preview-сцене -> SaveAsPrefabAsset = Variant).
            var previewScene = EditorSceneManager.NewPreviewScene();
            try
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(source, previewScene);
                instance.name = System.IO.Path.GetFileNameWithoutExtension(variantPath);
                var saved = PrefabUtility.SaveAsPrefabAsset(instance, variantPath);
                if (saved == null)
                {
                    Debug.LogError("[Editor] SaveAsPrefabAsset вернул null: " + variantPath);
                    return null;
                }
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(previewScene);
            }

            Debug.Log("[Editor] Создан вариант " + variantPath + " <- " + sourcePath);

            // Шаг 2: настройка под пул — через LoadPrefabContents (стандартный путь правки префаб-ассета/варианта).
            if (!ConfigureForPool(variantPath))
                return null;

            return AssetDatabase.LoadAssetAtPath<GameObject>(variantPath);
        }

        /// <summary>
        /// true, если у каждого включённого ParticleSystemRenderer иерархии есть материал (и материал трейлов,
        /// когда модуль trails включён). Отсутствующий материал = розовые/невидимые частицы.
        /// </summary>
        private static bool HasValidMaterials(GameObject prefab)
        {
            foreach (var r in prefab.GetComponentsInChildren<ParticleSystemRenderer>(true))
            {
                if (!r.enabled)
                    continue;

                if (r.sharedMaterial == null)
                {
                    Debug.LogWarning("[Editor] '" + prefab.name + "': у ParticleSystemRenderer на '" + r.name +
                                     "' нет материала.");
                    return false;
                }

                var ps = r.GetComponent<ParticleSystem>();
                if (ps != null && ps.trails.enabled && r.trailMaterial == null)
                {
                    Debug.LogWarning("[Editor] '" + prefab.name + "': у трейлов на '" + r.name + "' нет материала.");
                    return false;
                }
            }

            return true;
        }

        private static bool ConfigureForPool(string variantPath)
        {
            var root = PrefabUtility.LoadPrefabContents(variantPath);
            try
            {
                var rootPs = root.GetComponent<ParticleSystem>();
                if (rootPs == null)
                {
                    Debug.LogWarning("[Editor] " + variantPath + ": ParticleSystem на корне не найден — настройка пропущена.");
                    return false;
                }

                // Все системы: loop = false, playOnAwake = false. Дочерние stopAction Destroy/Disable -> None
                // (иначе ребёнок сам выключит/уничтожит объект из пула).
                var systems = root.GetComponentsInChildren<ParticleSystem>(true);
                foreach (var ps in systems)
                {
                    var main = ps.main;
                    main.loop = false;
                    main.playOnAwake = false;
                    if (ps != rootPs &&
                        (main.stopAction == ParticleSystemStopAction.Destroy ||
                         main.stopAction == ParticleSystemStopAction.Disable))
                    {
                        Debug.Log("[Editor] '" + ps.name + "': stopAction " + main.stopAction + " -> None (пул).");
                        main.stopAction = ParticleSystemStopAction.None;
                    }
                }

                var rootMain = rootPs.main;
                rootMain.stopAction = ParticleSystemStopAction.Callback;
                Debug.Log("[Editor] " + root.name + ": ParticleSystem в иерархии — " + systems.Length +
                          ", loop = false, playOnAwake = false, корневой stopAction = Callback.");

                // ParticleController на корне + _particleSystem.
                var controller = root.GetComponent<ParticleController>();
                if (controller == null)
                {
                    controller = root.AddComponent<ParticleController>();
                    Debug.Log("[Editor] " + root.name + ": добавлен ParticleController.");
                }

                var controllerSo = new SerializedObject(controller);
                var psProp = controllerSo.FindProperty("_particleSystem");
                if (psProp == null)
                {
                    Debug.LogWarning("[Editor] ParticleController._particleSystem не найдено (рантайм изменился?).");
                }
                else
                {
                    psProp.objectReferenceValue = rootPs;
                    controllerSo.ApplyModifiedProperties();
                }

                // Сторонние MonoBehaviour выключаем: они могут сами Destroy/SetActive(false) и ломать возврат в пул.
                DisableForeignBehaviours(root);

                PrefabUtility.SaveAsPrefabAsset(root, variantPath);
                Debug.Log("[Editor] Вариант настроен и сохранён: " + variantPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            return VerifyVariant(variantPath);
        }

        private static void DisableForeignBehaviours(GameObject root)
        {
            int disabled = 0;
            foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour == null)
                {
                    Debug.LogWarning("[Editor] " + root.name + ": компонент с потерянным скриптом пропущен.");
                    continue;
                }

                string ns = behaviour.GetType().Namespace;
                bool own = ns != null && (ns == OwnNamespaceRoot || ns.StartsWith(OwnNamespaceRoot + "."));
                if (own)
                    continue;

                if (!behaviour.enabled)
                {
                    Debug.Log("[Editor] " + root.name + ": '" + behaviour.GetType().Name + "' на '" + behaviour.name +
                              "' уже выключен.");
                    continue;
                }

                behaviour.enabled = false;
                disabled++;
                Debug.Log("[Editor] " + root.name + ": выключен сторонний компонент '" + behaviour.GetType().FullName +
                          "' на '" + behaviour.name + "' (защита пула).");
            }

            Debug.Log("[Editor] " + root.name + ": сторонних компонентов выключено — " + disabled + ".");
        }

        /// <summary>
        /// Контрольная проверка сохранённого варианта: настройки действительно дошли до ассета и материалы на месте.
        /// Вариант без материалов удаляется (в конфиг не попадёт).
        /// </summary>
        private static bool VerifyVariant(string variantPath)
        {
            var saved = AssetDatabase.LoadAssetAtPath<GameObject>(variantPath);
            if (saved == null)
            {
                Debug.LogWarning("[Editor] Проверка: " + variantPath + " не загружается.");
                return false;
            }

            if (!HasValidMaterials(saved))
            {
                Debug.LogError("[Editor] Проверка " + variantPath +
                               ": итоговый вариант с отсутствующим материалом — удалён.");
                AssetDatabase.DeleteAsset(variantPath);
                return false;
            }

            var ps = saved.GetComponent<ParticleSystem>();
            var pc = saved.GetComponent<ParticleController>();
            bool ok = ps != null && pc != null &&
                      ps.main.stopAction == ParticleSystemStopAction.Callback &&
                      !ps.main.loop && !ps.main.playOnAwake;
            string type = PrefabUtility.GetPrefabAssetType(saved).ToString();

            if (ok)
                Debug.Log("[Editor] Проверка " + variantPath + ": OK (" + type +
                          ", Callback, loop = false, ParticleController есть, материалы на месте).");
            else
                Debug.LogWarning("[Editor] Проверка " + variantPath + ": настройки пула НЕ подтвердились (" + type +
                                 ") — проверь префаб вручную в Inspector.");

            // Настройки пула — предупреждение, но сам вариант с материалами оставляем.
            return true;
        }

        // ── VfxConfig ───────────────────────────────────────────────────────────

        /// <summary>Возвращает VfxConfig_Hub; создаёт ассет, если его ещё нет.</summary>
        private static VfxConfig EnsureHubConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<VfxConfig>(HubVfxConfigPath);
            if (config != null)
                return config;

            config = ScriptableObject.CreateInstance<VfxConfig>();
            AssetDatabase.CreateAsset(config, HubVfxConfigPath);
            AssetDatabase.SaveAssets();
            // Создание ассета в Undo не регистрируем (editor-files.md).
            Debug.Log("[Editor] Создан VfxConfig хаба: " + HubVfxConfigPath);
            return AssetDatabase.LoadAssetAtPath<VfxConfig>(HubVfxConfigPath);
        }

        private static void EnsureHubConfigEntries(GameObject stageComplete, GameObject buildingCompleted)
        {
            var config = EnsureHubConfig();
            if (config == null)
            {
                Debug.LogWarning("[Editor] Не удалось получить VfxConfig хаба: " + HubVfxConfigPath +
                                 " — записи не добавлены.");
                return;
            }

            var so = new SerializedObject(config);
            var entries = so.FindProperty("_entries");
            if (entries == null)
            {
                Debug.LogWarning("[Editor] VfxConfig._entries не найдено (рантайм изменился?).");
                return;
            }

            bool changed = false;
            changed |= EnsureEntry(entries, VfxEffectType.BuildStageComplete, stageComplete, StageCompletePoolSize);
            changed |= EnsureEntry(entries, VfxEffectType.BuildingCompleted, buildingCompleted, BuildingCompletedPoolSize);

            if (changed)
            {
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(config);
                Debug.Log("[Editor] VfxConfig хаба обновлён.");
            }
        }

        /// <summary>Убирает записи BuildStageComplete / BuildingCompleted из общего VfxConfig (остальные не трогает).</summary>
        private static void RemoveHubEntriesFromSharedConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<VfxConfig>(SharedVfxConfigPath);
            if (config == null)
            {
                Debug.Log("[Editor] Общий VfxConfig не найден (" + SharedVfxConfigPath + ") — чистить нечего.");
                return;
            }

            var so = new SerializedObject(config);
            var entries = so.FindProperty("_entries");
            if (entries == null)
            {
                Debug.LogWarning("[Editor] VfxConfig._entries не найдено (рантайм изменился?).");
                return;
            }

            bool changed = false;
            for (int i = entries.arraySize - 1; i >= 0; i--)
            {
                var typeProp = entries.GetArrayElementAtIndex(i).FindPropertyRelative("type");
                if (typeProp == null)
                    continue;

                int value = typeProp.intValue;
                if (value != (int)VfxEffectType.BuildStageComplete && value != (int)VfxEffectType.BuildingCompleted)
                    continue;

                entries.DeleteArrayElementAtIndex(i);
                changed = true;
                Debug.Log("[Editor] Из общего VfxConfig удалена hub-запись " + (VfxEffectType)value + ".");
            }

            if (changed)
            {
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(config);
                Debug.Log("[Editor] Общий VfxConfig очищен от hub-записей.");
            }
            else
            {
                Debug.Log("[Editor] Общий VfxConfig: hub-записей нет.");
            }
        }

        private static bool EnsureEntry(SerializedProperty entries, VfxEffectType type, GameObject prefab, int poolSize)
        {
            for (int i = 0; i < entries.arraySize; i++)
            {
                var existing = entries.GetArrayElementAtIndex(i);
                var typeProp = existing.FindPropertyRelative("type");
                if (typeProp == null || typeProp.intValue != (int)type)
                    continue;

                // Запись есть: poolSize и прочее не трогаем; пустой prefab — дозаполняем, если он у нас есть.
                var existingPrefab = existing.FindPropertyRelative("prefab");
                if (existingPrefab != null && existingPrefab.objectReferenceValue == null && prefab != null)
                {
                    existingPrefab.objectReferenceValue = prefab;
                    Debug.Log("[Editor] VfxConfig: " + type + " — дозаполнен пустой prefab.");
                    return true;
                }

                Debug.Log("[Editor] VfxConfig: запись " + type + " уже есть — не трогаем.");
                return false;
            }

            if (prefab == null)
            {
                Debug.LogWarning("[Editor] VfxConfig: для " + type + " нет префаба — запись не добавлена.");
                return false;
            }

            int index = entries.arraySize;
            entries.arraySize = index + 1;
            var entry = entries.GetArrayElementAtIndex(index);
            // arraySize++ зануляет инициализаторы — заполняем КАЖДОЕ поле элемента явно.
            var newType = entry.FindPropertyRelative("type");
            var newPrefab = entry.FindPropertyRelative("prefab");
            var newPool = entry.FindPropertyRelative("poolSize");
            if (newType == null || newPrefab == null || newPool == null)
            {
                Debug.LogWarning("[Editor] У VfxEntry нет полей type/prefab/poolSize (рантайм изменился?) — запись " + type + " не заполнена.");
                entries.arraySize = index;
                return false;
            }

            newType.intValue = (int)type;
            newPrefab.objectReferenceValue = prefab;
            newPool.intValue = poolSize;
            Debug.Log("[Editor] VfxConfig: добавлена запись " + type + " (" + prefab.name + ", pool " + poolSize + ").");
            return true;
        }

        // ── Прошивка сцены ──────────────────────────────────────────────────────

        private static void WireVfxConfigInActiveScene()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.name != SceneKeys.BaseHub)
            {
                Debug.LogWarning("[Editor] Активная сцена '" + scene.name + "' — не " + SceneKeys.BaseHub +
                                 ": GameLifetimeScope._vfxConfig не прошит. Открой BaseHub и перезапусти пункт " +
                                 "(OpenScene из скрипта не делаем).");
                return;
            }

            var scope = Object.FindAnyObjectByType<GameLifetimeScope>(FindObjectsInactive.Include);
            if (scope == null)
            {
                Debug.LogWarning("[Editor] В сцене " + scene.name + " нет GameLifetimeScope — прошивка пропущена.");
                return;
            }

            // Конфиг — свежей ссылкой, уже после получения сцены.
            var config = AssetDatabase.LoadAssetAtPath<VfxConfig>(HubVfxConfigPath);
            if (config == null)
            {
                Debug.LogWarning("[Editor] VfxConfig хаба не найден — GameLifetimeScope._vfxConfig не тронут.");
                return;
            }

            var so = new SerializedObject(scope);
            var prop = so.FindProperty("_vfxConfig");
            if (prop == null)
            {
                Debug.LogWarning("[Editor] GameLifetimeScope._vfxConfig не найдено (рантайм изменился?).");
                return;
            }

            if (prop.objectReferenceValue == config)
            {
                Debug.Log("[Editor] GameLifetimeScope._vfxConfig уже прошит в " + scene.name + ".");
                return;
            }

            prop.objectReferenceValue = config;
            so.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Editor] GameLifetimeScope._vfxConfig прошит на VfxConfig_Hub в " + scene.name + ", сцена сохранена.");
        }
    }
}
#endif
