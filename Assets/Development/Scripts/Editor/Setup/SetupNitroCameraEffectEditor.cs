#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using FlatOutLike.Controllers.Camera;
using FlatOutLike.Data.Configs.Camera;
using FlatOutLike.Installers;

namespace FlatOutLike.Editor.Setup
{
    /// <summary>
    /// Настраивает радиальный blur-эффект нитро на активной сцене:
    ///  1. Ensure NitroCameraEffectConfig_Default (ScriptableObject-ассет с дефолтами).
    ///  2. Ensure материал NitroRadialBlur.mat из шейдера "FlatOutLike/NitroRadialBlur".
    ///  3. Ensure FullScreenPassRendererFeature "NitroRadialBlur" на PC_Renderer и Mobile_Renderer
    ///     (изначально выключен — включает NitroCameraEffectController только на время нитро).
    ///  4. Ensure NitroCameraEffectController на GameObject'е с CinemachineChaseCameraBehaviour
    ///     активной сцены, прошивает _chaseCamera/_config/_blurFeatures.
    ///  5. Прошивает GameLifetimeScope._nitroCameraEffectController.
    ///
    /// Запускать на КАЖДОЙ геймплейной сцене (Derby_Arena_01, Stunts_HighJump) — шаги 1-3
    /// ассетные и идемпотентны сами по себе (при повторном запуске просто находят
    /// существующее), шаг 4-5 сценные и должны быть выполнены на каждой сцене отдельно.
    /// </summary>
    public static class SetupNitroCameraEffectEditor
    {
        private const string ConfigFolderData   = "Assets/Development/Data";
        private const string ConfigFolderCfg    = "Assets/Development/Data/Configs";
        private const string ConfigAssetPath =
            "Assets/Development/Data/Configs/Camera/NitroCameraEffectConfig_Default.asset";

        private const string MaterialPath = "Assets/Development/Art/Materials/NitroRadialBlur.mat";
        private const string ShaderName = "FlatOutLike/NitroRadialBlur";

        private const string FeatureName = "NitroRadialBlur";

        private static readonly string[] RendererDataPaths =
        {
            "Assets/Settings/PC_Renderer.asset",
            "Assets/Settings/Mobile_Renderer.asset",
        };

        [MenuItem("FlatOutLike/Setup/Setup Nitro Camera Effect")]
        public static void SetupNitroCameraEffect()
        {
            Undo.SetCurrentGroupName("Setup Nitro Camera Effect");
            int undoGroup = Undo.GetCurrentGroup();

            // ── 1. Конфиг ─────────────────────────────────────────────────────
            var config = EnsureConfig();

            // ── 2. Материал ──────────────────────────────────────────────────
            var material = EnsureMaterial();

            // ── 3. Renderer-фичи ─────────────────────────────────────────────
            var features = new List<FullScreenPassRendererFeature>();
            foreach (var rendererPath in RendererDataPaths)
            {
                var feature = EnsureRendererFeature(rendererPath, material);
                if (feature != null)
                    features.Add(feature);
            }

            // ── 4. Сценная часть ─────────────────────────────────────────────
            NitroCameraEffectController controller = null;
            var chaseBehaviour = Object.FindAnyObjectByType<CinemachineChaseCameraBehaviour>(FindObjectsInactive.Include);
            if (chaseBehaviour == null)
            {
                Debug.LogWarning("[Editor] Setup Nitro Camera Effect: на сцене '" +
                                  SceneManager.GetActiveScene().name +
                                  "' не найден CinemachineChaseCameraBehaviour — сценная часть пропущена.");
            }
            else
            {
                controller = SetupSceneController(chaseBehaviour, config, features);
            }

            // ── 5. Скоуп ─────────────────────────────────────────────────────
            var scope = Object.FindAnyObjectByType<GameLifetimeScope>(FindObjectsInactive.Include);
            if (scope == null)
            {
                Debug.LogWarning("[Editor] Setup Nitro Camera Effect: GameLifetimeScope не найден на сцене — " +
                                  "_nitroCameraEffectController не прошит.");
            }
            else if (controller != null)
            {
                var scopeSo = new SerializedObject(scope);
                var prop = scopeSo.FindProperty("_nitroCameraEffectController");
                if (prop == null)
                {
                    Debug.LogWarning("[Editor] Setup Nitro Camera Effect: у GameLifetimeScope нет поля " +
                                      "_nitroCameraEffectController — обнови рантайм-класс.");
                }
                else if (prop.objectReferenceValue != controller)
                {
                    prop.objectReferenceValue = controller;
                    scopeSo.ApplyModifiedProperties();
                    EditorUtility.SetDirty(scope);
                    Debug.Log("[Editor] Setup Nitro Camera Effect: прошит GameLifetimeScope._nitroCameraEffectController.");
                }
            }

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Undo.CollapseUndoOperations(undoGroup);

            Debug.Log("[Editor] Setup Nitro Camera Effect завершён для сцены '" +
                       SceneManager.GetActiveScene().name + "'. Renderer-фич подключено: " + features.Count +
                       "/" + RendererDataPaths.Length + ".");
        }

        // ── Шаг 1: конфиг ───────────────────────────────────────────────────────
        private static NitroCameraEffectConfig EnsureConfig()
        {
            var existing = AssetDatabase.LoadAssetAtPath<NitroCameraEffectConfig>(ConfigAssetPath);
            if (existing != null)
                return existing;

            EnsureFolder("Assets/Development", "Data");
            EnsureFolder(ConfigFolderData, "Configs");
            EnsureFolder(ConfigFolderCfg, "Camera");

            var config = ScriptableObject.CreateInstance<NitroCameraEffectConfig>();
            AssetDatabase.CreateAsset(config, ConfigAssetPath);
            AssetDatabase.SaveAssets();

            Debug.Log("[Editor] Setup Nitro Camera Effect: создан " + ConfigAssetPath + ".");
            return config;
        }

        // ── Шаг 2: материал ─────────────────────────────────────────────────────
        private static Material EnsureMaterial()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (existing != null)
                return existing;

            var shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                Debug.LogError("[Editor] Setup Nitro Camera Effect: шейдер '" + ShaderName +
                                "' не найден — материал не создан, шаг с renderer-фичами будет пропущен там, где фичи ещё не существуют.");
                return null;
            }

            var material = new Material(shader) { name = "NitroRadialBlur" };
            AssetDatabase.CreateAsset(material, MaterialPath);
            AssetDatabase.SaveAssets();

            Debug.Log("[Editor] Setup Nitro Camera Effect: создан материал " + MaterialPath + ".");
            return material;
        }

        // ── Шаг 3: renderer-фича ────────────────────────────────────────────────
        private static FullScreenPassRendererFeature EnsureRendererFeature(string rendererDataPath, Material material)
        {
            var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererDataPath);
            if (rendererData == null)
            {
                Debug.LogWarning("[Editor] Setup Nitro Camera Effect: renderer-ассет не найден — " + rendererDataPath +
                                  " — пропускаю.");
                return null;
            }

            foreach (var existingFeature in rendererData.rendererFeatures)
            {
                if (existingFeature is FullScreenPassRendererFeature existingFullScreen && existingFeature.name == FeatureName)
                {
                    Debug.Log("[Editor] Setup Nitro Camera Effect: фича '" + FeatureName + "' уже есть на " +
                              rendererDataPath + ".");
                    return existingFullScreen;
                }
            }

            if (material == null)
            {
                Debug.LogWarning("[Editor] Setup Nitro Camera Effect: материал не создан — не могу добавить фичу '" +
                                  FeatureName + "' на " + rendererDataPath + ".");
                return null;
            }

            var feature = ScriptableObject.CreateInstance<FullScreenPassRendererFeature>();
            feature.name = FeatureName;
            feature.passMaterial = material;
            feature.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.AfterRenderingPostProcessing;
            feature.fetchColorBuffer = true;
            feature.SetActive(false); // включает контроллер только на время нитро

            AssetDatabase.AddObjectToAsset(feature, rendererData);

            // Сохраняем ДО чтения localId: у только что добавленного суб-ассета
            // localFileIdentifier стабилизируется лишь после сериализации — иначе
            // в m_RendererFeatureMap рискуем записать 0 и URP «потеряет» фичу.
            AssetDatabase.SaveAssets();

            var so = new SerializedObject(rendererData);
            var featuresProp = so.FindProperty("m_RendererFeatures");
            var mapProp = so.FindProperty("m_RendererFeatureMap");

            int idx = featuresProp.arraySize;
            featuresProp.arraySize++;
            mapProp.arraySize++;

            featuresProp.GetArrayElementAtIndex(idx).objectReferenceValue = feature;

            if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out string _, out long localId) && localId != 0)
                mapProp.GetArrayElementAtIndex(idx).longValue = localId;
            else
                Debug.LogWarning("[Editor] Setup Nitro Camera Effect: не удалось получить localId фичи '" +
                                  FeatureName + "' на " + rendererDataPath +
                                  " — проверь m_RendererFeatureMap в ассете вручную.");

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(rendererData);

            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(rendererDataPath, ImportAssetOptions.ForceUpdate);

            Debug.Log("[Editor] Setup Nitro Camera Effect: добавлена фича '" + FeatureName + "' на " + rendererDataPath + ".");
            return feature;
        }

        // ── Шаг 4: сценная часть ────────────────────────────────────────────────
        private static NitroCameraEffectController SetupSceneController(
            CinemachineChaseCameraBehaviour chaseBehaviour, NitroCameraEffectConfig config,
            List<FullScreenPassRendererFeature> features)
        {
            var go = chaseBehaviour.gameObject;

            var controller = go.GetComponent<NitroCameraEffectController>();
            if (controller == null)
            {
                controller = Undo.AddComponent<NitroCameraEffectController>(go);
                Debug.Log("[Editor] Setup Nitro Camera Effect: добавлен NitroCameraEffectController на '" + go.name + "'.");
            }

            // Читаем _camera с chase-адаптера, а не гадаем — единственный источник правды.
            var chaseSo = new SerializedObject(chaseBehaviour);
            var chaseCameraProp = chaseSo.FindProperty("_camera");
            var chaseCamera = chaseCameraProp != null ? chaseCameraProp.objectReferenceValue : null;

            var controllerSo = new SerializedObject(controller);
            bool changed = false;

            var chaseCameraProp2 = controllerSo.FindProperty("_chaseCamera");
            if (chaseCameraProp2 != null)
            {
                if (chaseCamera != null)
                {
                    if (chaseCameraProp2.objectReferenceValue != chaseCamera)
                    {
                        chaseCameraProp2.objectReferenceValue = chaseCamera;
                        changed = true;
                    }
                }
                else
                {
                    Debug.LogWarning("[Editor] Setup Nitro Camera Effect: у CinemachineChaseCameraBehaviour._camera не задан — " +
                                      "_chaseCamera не прошит, оставляю как есть.");
                }
            }

            var configProp = controllerSo.FindProperty("_config");
            if (configProp != null)
            {
                if (config != null)
                {
                    if (configProp.objectReferenceValue != config)
                    {
                        configProp.objectReferenceValue = config;
                        changed = true;
                    }
                }
                else
                {
                    Debug.LogWarning("[Editor] Setup Nitro Camera Effect: NitroCameraEffectConfig отсутствует — " +
                                      "_config не прошит, оставляю как есть.");
                }
            }

            var featuresProp = controllerSo.FindProperty("_blurFeatures");
            if (featuresProp != null)
            {
                if (features.Count > 0)
                {
                    if (featuresProp.arraySize != features.Count)
                    {
                        featuresProp.arraySize = features.Count;
                        changed = true;
                    }

                    for (int i = 0; i < features.Count; i++)
                    {
                        var element = featuresProp.GetArrayElementAtIndex(i);
                        if (element.objectReferenceValue != features[i])
                        {
                            element.objectReferenceValue = features[i];
                            changed = true;
                        }
                    }
                }
                else
                {
                    Debug.LogWarning("[Editor] Setup Nitro Camera Effect: ни одна renderer-фича не найдена/создана — " +
                                      "_blurFeatures не прошит, оставляю как есть.");
                }
            }

            if (changed)
            {
                controllerSo.ApplyModifiedProperties();
                EditorUtility.SetDirty(controller);
                Debug.Log("[Editor] Setup Nitro Camera Effect: прошиты ссылки NitroCameraEffectController на '" + go.name + "'.");
            }

            return controller;
        }

        // ── Утилиты ──────────────────────────────────────────────────────────────
        private static void EnsureFolder(string parent, string folderName)
        {
            string full = parent + "/" + folderName;
            if (!AssetDatabase.IsValidFolder(full))
            {
                AssetDatabase.CreateFolder(parent, folderName);
                Debug.Log("[Editor] Created folder " + full);
            }
        }
    }
}
#endif
