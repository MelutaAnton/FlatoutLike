#if UNITY_EDITOR
using System.IO;
using TMPro;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using FlatOutLike.Controllers.Camera;
using FlatOutLike.Controllers.Hub;
using FlatOutLike.Controllers.Mobile;
using FlatOutLike.Data.Configs.Camera;
using FlatOutLike.Data.Configs.Hub;
using FlatOutLike.Data.Configs.Input;
using FlatOutLike.Data.Configs.Progression;
using FlatOutLike.Editor.Configs;
using FlatOutLike.Editor.Scenes;
using FlatOutLike.Editor.Setup;
using FlatOutLike.Editor.UI;
using FlatOutLike.Installers;
using FlatOutLike.Views.UI.Overlay;
using Object = UnityEngine.Object;

namespace FlatOutLike.Editor.Hub
{
    /// <summary>
    /// Шаг 11a: собирает сцену BaseHub (Assets/Development/Scenes/BaseHub.unity).
    ///
    /// Состав сцены:
    ///   • Directional Light, Environment/Ground (Plane 100x100, материал Grass из Arcade Idle Engine);
    ///   • Buildings/&lt;BuildingId&gt; — корень на Position/YRotation из BuildingCatalogConfig, внутри
    ///     инстанс VisualPrefab; для Enabled=false — world-space TextMeshPro «Coming soon»;
    ///   • HubCharacter (инстанс префаба в точке спавна из каталога);
    ///   • Main Camera (CinemachineBrain + единственный AudioListener) и CM_HubCamera
    ///     (CinemachineCamera + Follow + RotationComposer + CinemachineHubCameraBehaviour, Priority=Active);
    ///   • GameLifetimeScope + GameBootstrap, UICanvas + EventSystem (HUD хаба — шаг 11b);
    ///   • connected-инстансы префабов MobileInputCanvas / PauseOverlayCanvas / AudioPlayer;
    ///   • прошивка полей GameLifetimeScope; Build Settings (Boot первой, BaseHub второй).
    ///
    /// Идемпотентность: повторный прогон = миграция без дублей (поиск по компонентам и именам,
    /// устаревший визуал здания заменяется при смене префаба в каталоге). Все ассеты читаются
    /// ПОСЛЕ OpenScene (OpenScene выгружает ассеты — managed-ссылка становится fake-null).
    ///
    /// Запускать ПОСЛЕ:
    ///   FlatOutLike/UI/Build Mobile Input Canvas  (в любой игровой сцене — чтобы в префабе был HubGroup/HubStick)
    /// Недостающие SO-ассеты и префаб персонажа скрипт доделывает сам
    /// (Create Building SO Set / Build Hub Character Prefab — только если их ещё нет).
    /// </summary>
    public static class BuildBaseHubLayoutEditor
    {
        private const string ScenePath         = "Assets/Development/Scenes/BaseHub.unity";
        private const string GrassMaterialPath = "Assets/ThirdParty/Arcade Idle Engine/Materials/Grass.mat";

        private const string LightName       = "Directional Light";
        private const string EnvironmentName = "Environment";
        private const string GroundName      = "Ground";
        private const string BuildingsName   = "Buildings";
        private const string LabelName       = "ComingSoonLabel";
        private const string HubCameraName   = "CM_HubCamera";

        // Plane = 10x10 м, масштаб 10 → земля 100x100 м.
        private const float GroundScale = 10f;

        // Метка «Coming soon»: над площадкой, наклон = наклону камеры хаба (камера смотрит
        // вдоль +Z сверху вниз), чтобы текст читался как плоскость, обращённая к камере.
        private const float LabelHeight   = 5f;
        private const float LabelPitchDeg = 50f;
        private const float LabelFontSize = 14f;

        [MenuItem("FlatOutLike/Hub/Build Base Hub Layout")]
        public static void BuildBaseHubLayout()
        {
            // Даём сохранить/выбросить правки текущей сцены ДО OpenScene(Single); Cancel = выход.
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log("[Editor] Build Base Hub Layout отменён пользователем (несохранённая сцена).");
                return;
            }

            EnsurePrerequisiteAssets();

            var scene = OpenOrCreateScene();

            // ── Ассеты — ТОЛЬКО после OpenScene ─────────────────────────────────
            var catalog = AssetDatabase.LoadAssetAtPath<BuildingCatalogConfig>(CreateBuildingSoSetEditor.CatalogPath);
            if (catalog == null)
            {
                Debug.LogError("[Editor] BuildingCatalogConfig_Default не найден: " +
                               CreateBuildingSoSetEditor.CatalogPath + " — сцена не собрана.");
                return;
            }

            Undo.SetCurrentGroupName("Build Base Hub Layout");
            int undoGroup = Undo.GetCurrentGroup();

            EnsureDirectionalLight();
            EnsureGround(scene);
            EnsureBuildings(scene, catalog);

            var hubCharacter = EnsureHubCharacter(scene, catalog);

            SetupCinemachineCameraRigEditor.EnsureBrain();
            EnsureHubCamera(hubCharacter);

            // Composition Root, UI-каркас.
            BuildMetaScenesEditor.CreateGameLifetimeScope();
            BuildMetaScenesEditor.CreateGameBootstrap();
            UIBuildHelpers.EnsureCanvasAndEventSystem();

            var mobileCanvas = EnsureMobileInputCanvas(scene);
            var pauseView    = EnsurePauseOverlay(scene);

            WireScope(scene, hubCharacter, mobileCanvas, pauseView);

            BuildMetaScenesEditor.RegisterAllScenesInBuildSettings();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Undo.CollapseUndoOperations(undoGroup);

            Debug.Log("[Editor] Build Base Hub Layout завершён, сцена сохранена: " + ScenePath);
        }

        // ── Подготовка ──────────────────────────────────────────────────────────

        /// <summary>Недостающие ассеты доделываем существующими меню (они ничего не перезаписывают).</summary>
        private static void EnsurePrerequisiteAssets()
        {
            bool setMissing =
                AssetDatabase.LoadAssetAtPath<BuildingCatalogConfig>(CreateBuildingSoSetEditor.CatalogPath) == null ||
                AssetDatabase.LoadAssetAtPath<HubCharacterConfig>(CreateBuildingSoSetEditor.CharacterConfigPath) == null ||
                AssetDatabase.LoadAssetAtPath<HubCameraConfig>(CreateBuildingSoSetEditor.CameraConfigPath) == null;
            if (setMissing)
            {
                Debug.Log("[Editor] Не все SO хаба существуют — запускаем Create Building SO Set.");
                CreateBuildingSoSetEditor.CreateBuildingSoSet();
            }

            if (AssetDatabase.LoadAssetAtPath<GameObject>(BuildHubCharacterPrefabEditor.PrefabPath) == null)
            {
                Debug.Log("[Editor] HubCharacter.prefab отсутствует — запускаем Build Hub Character Prefab.");
                BuildHubCharacterPrefabEditor.BuildHubCharacterPrefab();
            }
        }

        private static Scene OpenOrCreateScene()
        {
            if (File.Exists(Path.Combine(Directory.GetCurrentDirectory(), ScenePath)))
            {
                var existing = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                Debug.Log("[Editor] Открыта сцена " + ScenePath + ", дособираем недостающее.");
                return existing;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("[Editor] Создана новая сцена " + ScenePath + ".");
            return scene;
        }

        // ── Свет и земля ────────────────────────────────────────────────────────

        private static void EnsureDirectionalLight()
        {
            // Идемпотентность по КОМПОНЕНТУ: любой directional Light в сцене подходит.
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (l.type == LightType.Directional)
                {
                    Debug.Log("[Editor] Directional Light уже есть (" + l.name + ") — пропускаем.");
                    return;
                }
            }

            var go = new GameObject(LightName);
            Undo.RegisterCreatedObjectUndo(go, "Create Directional Light");
            var light = Undo.AddComponent<Light>(go);
            light.type = LightType.Directional;
            light.intensity = 1f;
            light.shadows = LightShadows.Soft;
            go.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            Debug.Log("[Editor] Создан Directional Light.");
        }

        private static void EnsureGround(Scene scene)
        {
            var env = GetOrCreateRoot(scene, EnvironmentName);

            GameObject ground = null;
            var groundT = env.transform.Find(GroundName);
            if (groundT != null && groundT.GetComponent<MeshFilter>() != null)
                ground = groundT.gameObject;

            if (ground == null)
            {
                ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
                ground.name = GroundName;
                Undo.RegisterCreatedObjectUndo(ground, "Create Ground");
                ground.transform.SetParent(env.transform, false);
                Debug.Log("[Editor] Создан Environment/Ground (Plane).");
            }
            else
            {
                Debug.Log("[Editor] Environment/Ground уже есть — обновляем масштаб/материал.");
            }

            Undo.RecordObject(ground.transform, "Setup Ground");
            ground.transform.localPosition = Vector3.zero;
            ground.transform.localRotation = Quaternion.identity;
            ground.transform.localScale = new Vector3(GroundScale, 1f, GroundScale);

            var grass = AssetDatabase.LoadAssetAtPath<Material>(GrassMaterialPath);
            var meshRenderer = ground.GetComponent<MeshRenderer>();
            if (grass == null)
            {
                Debug.LogWarning("[Editor] Материал Grass не найден: " + GrassMaterialPath + " — материал земли не тронут.");
            }
            else if (meshRenderer != null)
            {
                Undo.RecordObject(meshRenderer, "Assign Grass");
                meshRenderer.sharedMaterial = grass;
                Debug.Log("[Editor] Ground: материал Grass назначен (ссылкой).");
            }
        }

        // ── Здания ──────────────────────────────────────────────────────────────

        private static void EnsureBuildings(Scene scene, BuildingCatalogConfig catalog)
        {
            var root = GetOrCreateRoot(scene, BuildingsName);
            int processed = 0;

            foreach (var entry in catalog.Entries)
            {
                processed++;
                var go = FindOrCreateChild(root.transform, entry.Id.ToString());

                Undo.RecordObject(go.transform, "Place Building");
                go.transform.localPosition = entry.Position;
                go.transform.localRotation = Quaternion.Euler(0f, entry.YRotation, 0f);
                go.transform.localScale = Vector3.one;

                SyncVisual(go, entry);
                SyncComingSoonLabel(go, entry, catalog.ComingSoonLabel);
            }

            Debug.Log("[Editor] Buildings: обработано зданий — " + processed + ".");
        }

        /// <summary>
        /// Гарантирует ровно один инстанс entry.VisualPrefab внутри здания; инстансы другого
        /// префаба (после смены визуала в каталоге) заменяются.
        /// </summary>
        private static void SyncVisual(GameObject building, BuildingCatalogConfig.BuildingEntry entry)
        {
            if (entry.VisualPrefab == null)
            {
                Debug.Log("[Editor] " + entry.Id + ": VisualPrefab в каталоге не задан — визуал не создаётся.");
                return;
            }

            bool found = false;
            for (int i = building.transform.childCount - 1; i >= 0; i--)
            {
                var child = building.transform.GetChild(i).gameObject;
                if (child.name == LabelName || !PrefabUtility.IsAnyPrefabInstanceRoot(child))
                    continue;

                var source = PrefabUtility.GetCorrespondingObjectFromSource(child);
                if (!found && source == entry.VisualPrefab)
                {
                    found = true;
                    continue;
                }

                Debug.Log("[Editor] " + entry.Id + ": убран устаревший визуал '" + child.name + "'.");
                Undo.DestroyObjectImmediate(child);
            }

            if (found)
                return;

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(entry.VisualPrefab, building.transform);
            Undo.RegisterCreatedObjectUndo(instance, "Create Building Visual");
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            Debug.Log("[Editor] " + entry.Id + ": создан визуал '" + entry.VisualPrefab.name + "'.");
        }

        /// <summary>
        /// Для Enabled=false — world-space TextMeshPro (3D, не UGUI) над площадкой; для
        /// Enabled=true — метка снимается (миграция при включении здания в каталоге).
        /// </summary>
        private static void SyncComingSoonLabel(GameObject building, BuildingCatalogConfig.BuildingEntry entry, string text)
        {
            var labelT = building.transform.Find(LabelName);

            if (entry.Enabled)
            {
                if (labelT != null)
                {
                    Undo.DestroyObjectImmediate(labelT.gameObject);
                    Debug.Log("[Editor] " + entry.Id + ": здание включено — метка Coming soon убрана.");
                }
                return;
            }

            GameObject labelGo;
            if (labelT == null)
            {
                labelGo = new GameObject(LabelName);
                Undo.RegisterCreatedObjectUndo(labelGo, "Create Coming Soon Label");
                labelGo.transform.SetParent(building.transform, false);
                Debug.Log("[Editor] " + entry.Id + ": создана метка Coming soon.");
            }
            else
            {
                labelGo = labelT.gameObject;
            }

            Undo.RecordObject(labelGo.transform, "Place Coming Soon Label");
            labelGo.transform.localPosition = new Vector3(0f, LabelHeight, 0f);
            // Мировой поворот — не зависит от yaw здания: метка всегда обращена к камере хаба.
            labelGo.transform.rotation = Quaternion.Euler(LabelPitchDeg, 0f, 0f);

            var tmp = labelGo.GetComponent<TextMeshPro>();
            if (tmp == null)
                tmp = Undo.AddComponent<TextMeshPro>(labelGo);
            else
                Undo.RecordObject(tmp, "Update Coming Soon Label");

            if (string.IsNullOrEmpty(text))
                Debug.LogWarning("[Editor] ComingSoonLabel в каталоге пуст — метка " + entry.Id + " без текста.");

            tmp.text = text;
            tmp.fontSize = LabelFontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.raycastTarget = false; // рейкаст для world-space метки не нужен
            tmp.maskable = false;

            var font = UIBuildHelpers.LoadFont(UIBuildHelpers.DisplayFontPath);
            if (font != null)
                tmp.font = font;
        }

        // ── Персонаж и камера ───────────────────────────────────────────────────

        private static HubCharacterController EnsureHubCharacter(Scene scene, BuildingCatalogConfig catalog)
        {
            var existing = Object.FindAnyObjectByType<HubCharacterController>(FindObjectsInactive.Include);
            if (existing != null)
            {
                Debug.Log("[Editor] HubCharacter уже в сцене — переиспользуем (позицию не трогаем).");
                return existing;
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BuildHubCharacterPrefabEditor.PrefabPath);
            if (prefab == null)
            {
                Debug.LogError("[Editor] HubCharacter.prefab не найден: " + BuildHubCharacterPrefabEditor.PrefabPath);
                return null;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            Undo.RegisterCreatedObjectUndo(instance, "Create HubCharacter");
            instance.transform.position = catalog.CharacterSpawnPosition;
            instance.transform.rotation = Quaternion.Euler(0f, catalog.CharacterSpawnYRotation, 0f);
            Debug.Log("[Editor] Создан HubCharacter в точке спавна " + catalog.CharacterSpawnPosition + ".");

            var controller = instance.GetComponent<HubCharacterController>();
            if (controller == null)
                Debug.LogWarning("[Editor] На HubCharacter нет HubCharacterController — пересобери префаб.");
            return controller;
        }

        private static void EnsureHubCamera(HubCharacterController hubCharacter)
        {
            // Идемпотентность по компоненту; имя — только запасной поиск.
            GameObject go;
            var behaviourExisting = Object.FindAnyObjectByType<CinemachineHubCameraBehaviour>(FindObjectsInactive.Include);
            if (behaviourExisting != null)
            {
                go = behaviourExisting.gameObject;
            }
            else
            {
                go = GameObject.Find(HubCameraName);
                if (go == null)
                {
                    go = new GameObject(HubCameraName);
                    Undo.RegisterCreatedObjectUndo(go, "Create " + HubCameraName);
                    Debug.Log("[Editor] Создан " + HubCameraName + ".");
                }
            }

            var cmCam = go.GetComponent<CinemachineCamera>();
            if (cmCam == null)
                cmCam = Undo.AddComponent<CinemachineCamera>(go);

            if (go.GetComponent<CinemachineFollow>() == null)
                Undo.AddComponent<CinemachineFollow>(go);

            if (go.GetComponent<CinemachineRotationComposer>() == null)
                Undo.AddComponent<CinemachineRotationComposer>(go);

            var behaviour = go.GetComponent<CinemachineHubCameraBehaviour>();
            if (behaviour == null)
            {
                behaviour = Undo.AddComponent<CinemachineHubCameraBehaviour>(go);
                Debug.Log("[Editor] Добавлен CinemachineHubCameraBehaviour на " + go.name + ".");
            }

            Undo.RecordObject(cmCam, "Setup Hub Camera");
            cmCam.Priority = CameraPriorities.Active;

            if (hubCharacter != null)
            {
                cmCam.Follow = hubCharacter.transform;
                cmCam.LookAt = hubCharacter.transform;
                Debug.Log("[Editor] " + go.name + ": Follow/LookAt = HubCharacter.");
            }
            else
            {
                Debug.LogWarning("[Editor] HubCharacter не найден — Follow/LookAt камеры хаба не заданы.");
            }
            EditorUtility.SetDirty(cmCam);

            var so = new SerializedObject(behaviour);
            SetRef(so, "_camera", cmCam, "CinemachineCamera");

            // Конфиг — свежей ссылкой (после OpenScene).
            var config = AssetDatabase.LoadAssetAtPath<HubCameraConfig>(CreateBuildingSoSetEditor.CameraConfigPath);
            SetRef(so, "_config", config, "HubCameraConfig_Default");
            so.ApplyModifiedProperties();
        }

        // ── Канвасы-префабы ─────────────────────────────────────────────────────

        private static MobileInputCanvas EnsureMobileInputCanvas(Scene scene)
        {
            var mic = Object.FindAnyObjectByType<MobileInputCanvas>(FindObjectsInactive.Include);
            if (mic == null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BuildMobileInputCanvasEditor.PrefabPath);
                if (prefab == null)
                {
                    Debug.LogWarning("[Editor] " + BuildMobileInputCanvasEditor.PrefabPath + " не найден — " +
                                     "запусти 'FlatOutLike/UI/Build Mobile Input Canvas' в игровой сцене и повтори.");
                    return null;
                }

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                Undo.RegisterCreatedObjectUndo(instance, "Create MobileInputCanvas");
                mic = instance.GetComponent<MobileInputCanvas>();
                Debug.Log("[Editor] Создан инстанс MobileInputCanvas.");
            }
            else
            {
                Debug.Log("[Editor] MobileInputCanvas уже в сцене — переиспользуем.");
            }

            if (mic != null)
            {
                var so = new SerializedObject(mic);
                var hubGroup = so.FindProperty("_hubGroup");
                if (hubGroup == null || hubGroup.objectReferenceValue == null)
                    Debug.LogWarning("[Editor] В MobileInputCanvas не прошит _hubGroup — префаб собран до шага 11a. " +
                                     "Запусти 'FlatOutLike/UI/Build Mobile Input Canvas' и повтори.");
            }

            return mic;
        }

        private static PauseOverlayView EnsurePauseOverlay(Scene scene)
        {
            var view = Object.FindAnyObjectByType<PauseOverlayView>(FindObjectsInactive.Include);
            if (view != null)
            {
                Debug.Log("[Editor] PauseOverlayCanvas уже в сцене — переиспользуем.");
                return view;
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BuildPauseOverlayEditor.PrefabPath);
            if (prefab == null)
            {
                Debug.LogWarning("[Editor] " + BuildPauseOverlayEditor.PrefabPath + " не найден — " +
                                 "запусти 'FlatOutLike/UI/Build Pause Overlay' в игровой сцене и повтори.");
                return null;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            Undo.RegisterCreatedObjectUndo(instance, "Create PauseOverlayCanvas");
            Debug.Log("[Editor] Создан инстанс PauseOverlayCanvas.");
            return instance.GetComponent<PauseOverlayView>();
        }

        // ── Прошивка Composition Root ───────────────────────────────────────────

        private static void WireScope(Scene scene, HubCharacterController hubCharacter,
            MobileInputCanvas mobileCanvas, PauseOverlayView pauseView)
        {
            var scope = Object.FindAnyObjectByType<GameLifetimeScope>(FindObjectsInactive.Include);
            if (scope == null)
            {
                Debug.LogWarning("[Editor] GameLifetimeScope не найден — прошивка пропущена.");
                return;
            }

            // Конфиги — свежими ссылками (после OpenScene).
            var charConfig = AssetDatabase.LoadAssetAtPath<HubCharacterConfig>(CreateBuildingSoSetEditor.CharacterConfigPath);
            var catalog    = AssetDatabase.LoadAssetAtPath<BuildingCatalogConfig>(CreateBuildingSoSetEditor.CatalogPath);
            InputConfig inputConfig = CreateDefaultInputConfigEditor.EnsureAsset();

            var so = new SerializedObject(scope);
            SetRef(so, "_hubCharacterConfig", charConfig, "HubCharacterConfig_Default");
            SetRef(so, "_hubCharacter", hubCharacter, "HubCharacterController");
            SetRef(so, "_inputConfig", inputConfig, "InputConfig_Default");

            // Каталог зданий в scope контрактом шага 11a не предусмотрен — прошиваем, только если поле есть.
            var catalogProp = so.FindProperty("_buildingCatalogConfig");
            if (catalogProp != null)
                SetRef(so, "_buildingCatalogConfig", catalog, "BuildingCatalogConfig_Default");
            else
                Debug.Log("[Editor] GameLifetimeScope._buildingCatalogConfig отсутствует — пропуск (появится в шаге 11b).");

            so.ApplyModifiedProperties();

            // Каталоги машин/апгрейдов — как на мета-сценах (метод сам сохраняет активную сцену).
            SetupMetaConfigsEditor.SetupMetaConfigs();

            // Аудио: инстанс AudioPlayer, единственный AudioListener, _audioConfig/_audioPlayerController/_uiClickSoundRelay.
            SetupAudioEditor.WireOpenedScene(scene, scope);

            // Мобильный ввод и пауза — единые точки прошивки проекта.
            if (mobileCanvas != null)
                BuildMobileInputCanvasEditor.BindMobileInputCanvas(mobileCanvas);
            if (pauseView != null)
                BindMetaUIRefsEditor.BindPauseOverlayView(pauseView);
        }

        // ── Утилиты ─────────────────────────────────────────────────────────────

        private static GameObject GetOrCreateRoot(Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == name)
                    return root;

            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Create " + name);
            Debug.Log("[Editor] Создан корневой объект " + name + ".");
            return go;
        }

        private static GameObject FindOrCreateChild(Transform parent, string name)
        {
            var existing = parent.Find(name);
            if (existing != null)
                return existing.gameObject;

            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Create " + name);
            go.transform.SetParent(parent, false);
            Debug.Log("[Editor] Создан " + parent.name + "/" + name + ".");
            return go;
        }

        /// <summary>Прошивает ссылку, не присваивая null: при отсутствии объекта/поля — LogWarning, значение не тронуто.</summary>
        private static void SetRef(SerializedObject so, string field, Object value, string humanName)
        {
            var prop = so.FindProperty(field);
            string owner = so.targetObject.GetType().Name;

            if (prop == null)
            {
                Debug.LogWarning("[Editor] Поле " + owner + "." + field + " не найдено (рантайм ещё не скомпилирован?).");
                return;
            }

            if (value == null)
            {
                Debug.LogWarning("[Editor] " + humanName + " не найден — поле " + owner + "." + field + " не тронуто.");
                return;
            }

            if (prop.objectReferenceValue == value)
                return;

            prop.objectReferenceValue = value;
            Debug.Log("[Editor] " + owner + "." + field + " прошит.");
        }
    }
}
#endif
