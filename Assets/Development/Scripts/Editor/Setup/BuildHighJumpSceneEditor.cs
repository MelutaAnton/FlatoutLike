#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using FlatOutLike.Controllers.Camera;
using FlatOutLike.Controllers.Stunts;
using FlatOutLike.Data.Configs.Camera;
using FlatOutLike.Data.Configs.Combat;
using FlatOutLike.Data.Configs.Driver;
using FlatOutLike.Data.Configs.Stunts;
using FlatOutLike.Data.Configs.Vfx;
using FlatOutLike.Installers;
using FlatOutLike.Models;

namespace FlatOutLike.Editor.Setup
{
    /// <summary>
    /// Собирает сцену Stunts_HighJump.unity (Шаг 7): разгонная полоса → рампа →
    /// eject-зона, camera rig (chase + orbit) и debug-overlay с текстом состояния полёта.
    /// Приземление больше не определяется триггер-зоной — DriverFlightService решает
    /// это по успокоению ragdoll (velocity-based).
    ///
    /// Что создаётся:
    ///   • Ground_Strip  — разгонная полоса (куб 20x1x120).
    ///   • Ramp          — трамплин (куб 12x1x18, наклон -20° по X).
    ///   • EjectZone     — триггер-зона на верхушке рампы (EjectTriggerZone + kinematic Rigidbody).
    ///   • Car_Sport_09  — машина игрока.
    ///   • Main Camera   — CinemachineBrain + CM_ChaseCamera + CM_OrbitCamera + CameraRig (Cinemachine 3.x).
    ///   • Directional Light.
    ///   • DebugOverlay  — Canvas + Text + HighJumpDebugOverlayController.
    ///   • GameLifetimeScope + GameBootstrap, со всеми ссылками, кроме _derbyMatchConfig
    ///     (умышленно оставлен null — сцена не Derby-путь).
    ///
    /// Идемпотентность: find-or-create по имени с проверкой КОМПОНЕНТА, повторный
    /// запуск не дублирует объекты, только обновляет ссылки. Все FindProperty —
    /// с null-guard: рантайм Шага 7 может быть ещё не скомпилирован параллельным агентом.
    ///
    /// Запускать ПОСЛЕ:
    ///   FlatOutLike/Configs/Create Default DriverFlightConfig
    ///   FlatOutLike/Configs/Create Default HighJumpRulesConfig
    ///   (DamageConfig / VfxConfig / DriverEjectConfig / OrbitCameraConfig / ChaseCameraConfig
    ///    должны уже существовать из предыдущих шагов)
    /// </summary>
    public static class BuildHighJumpSceneEditor
    {
        private const string ScenePath        = "Assets/Development/Scenes/" + SceneKeys.StuntsHighJump + ".unity";
        private const string ScenesFolder     = "Assets/Development/Scenes";
        private const string CarPrefabPath    = "Assets/Development/Prefabs/Vehicles/Car_Sport_09.prefab";

        private const string ChaseCameraConfigPath = "Assets/Development/Data/Configs/Camera/ChaseCameraConfig_Default.asset";
        // HighJump-вариант орбит-конфига (фиксированный ракурс «сзади», как chase при езде).
        private const string OrbitCameraConfigPath = "Assets/Development/Data/Configs/Camera/OrbitCameraConfig_HighJump.asset";
        private const string DamageConfigPath       = "Assets/Development/Data/Configs/Combat/DamageConfig.asset";
        private const string VfxConfigPath           = "Assets/Development/Data/Configs/Vfx/VfxConfig.asset";
        // HighJump-вариант eject-конфига: импульс тюнится отдельно от Derby (per-scene поле scope).
        private const string DriverEjectConfigPath   = "Assets/Development/Data/Configs/Driver/DriverEjectConfig_HighJump.asset";
        private const string DriverFlightConfigPath  = "Assets/Development/Data/Configs/Driver/DriverFlightConfig_Default.asset";
        private const string HighJumpRulesConfigPath = "Assets/Development/Data/Configs/Stunts/HighJumpRulesConfig_Default.asset";

        // ── Геометрия трассы: разгонная полоса → рампа → зона приземления ──────
        // Ground_Strip: куб scale(20,1,120), позиция (0,-0.5,40) → верхняя грань y=0,
        // полоса покрывает z ∈ [-20, 100].
        private static readonly Vector3 GroundStripScale    = new Vector3(20f, 1f, 120f);
        private static readonly Vector3 GroundStripPosition = new Vector3(0f, -0.5f, 40f);

        // Ramp: куб scale(12,1,18), наклон -20° по X, центр (0,2.5,88).
        // Ближняя (нижняя) грань верхней поверхности стыкуется с концом полосы,
        // дальняя (верхняя) грань — точка отрыва машины у EjectZone.
        private static readonly Vector3 RampScale    = new Vector3(12f, 1f, 18f);
        private static readonly Vector3 RampPosition = new Vector3(0f, 2.5f, 88f);
        private const float RampTiltDegrees = -20f;

        // EjectZone — над дальней (верхней) гранью рампа.
        private static readonly Vector3 EjectZonePosition = new Vector3(0f, 6f, 96f);
        private static readonly Vector3 EjectZoneSize      = new Vector3(12f, 4f, 2f);

        [MenuItem("FlatOutLike/Scenes/Build HighJump Scene")]
        public static void BuildHighJumpScene()
        {
            Undo.SetCurrentGroupName("Build HighJump Scene");
            int undoGroup = Undo.GetCurrentGroup();

            // ── 1. Открываем или создаём сцену ───────────────────────────────
            var scene = OpenOrCreateScene();

            // ── 2. Трасса: полоса, рампа, eject-зона, зона приземления ────────
            CreateGroundStrip();
            CreateRamp();
            CreateEjectZone();
            RemoveLegacyLandingZone();

            // ── 3. Машина игрока ────────────────────────────────────────────
            var car = CreatePlayerCar();

            // ── 4. Камера: chase + orbit + rig (Cinemachine) ───────────────
            var cameraRig = CreateCameraRig();

            // ── 5. Directional Light ───────────────────────────────────────
            CreateDirectionalLight();

            // ── 6. Debug overlay (Canvas + Text + controller) ──────────────
            CreateDebugOverlay();

            // ── 7. GameLifetimeScope + GameBootstrap ───────────────────────
            var scope = CreateGameLifetimeScope();
            CreateGameBootstrap();

            // ── 8. Заполняем ссылки Composition Root ───────────────────────
            FillScopeReferences(scope, car, cameraRig);

            // ── 8b. Измерительная сетка-стена с отметками высоты ───────────
            SetupHighJumpNetEditor.Setup();

            // ── 9. Сохраняем сцену ──────────────────────────────────────────
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("[Editor] Сцена сохранена: " + ScenePath);

            Undo.CollapseUndoOperations(undoGroup);
            Debug.Log("[Editor] Build HighJump Scene завершён.");
        }

        // ─────────────────────────────────────────────────────────────────────

        private static Scene OpenOrCreateScene()
        {
            if (!AssetDatabase.IsValidFolder(ScenesFolder))
                AssetDatabase.CreateFolder("Assets/Development", "Scenes");

            string fullPath = Path.Combine(Directory.GetCurrentDirectory(), ScenePath);

            if (File.Exists(fullPath))
            {
                var existing = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                Debug.Log("[Editor] Открыта существующая " + SceneKeys.StuntsHighJump + ".unity.");
                return existing;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("[Editor] Создана новая " + SceneKeys.StuntsHighJump + ".unity.");
            return scene;
        }

        private static void CreateGroundStrip()
        {
            var go = GameObject.Find("Ground_Strip");
            if (go == null)
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Undo.RegisterCreatedObjectUndo(go, "Create Ground_Strip");
                go.name = "Ground_Strip";
                Debug.Log("[Editor] Создан Ground_Strip.");
            }
            else
            {
                Debug.Log("[Editor] Ground_Strip уже есть, обновляем трансформ.");
            }

            go.transform.position   = GroundStripPosition;
            go.transform.rotation   = Quaternion.identity;
            go.transform.localScale = GroundStripScale;
        }

        private static void CreateRamp()
        {
            var go = GameObject.Find("Ramp");
            if (go == null)
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Undo.RegisterCreatedObjectUndo(go, "Create Ramp");
                go.name = "Ramp";
                Debug.Log("[Editor] Создан Ramp.");
            }
            else
            {
                Debug.Log("[Editor] Ramp уже есть, обновляем трансформ.");
            }

            go.transform.position   = RampPosition;
            go.transform.rotation   = Quaternion.Euler(RampTiltDegrees, 0f, 0f);
            go.transform.localScale = RampScale;
        }

        private static void CreateEjectZone()
        {
            var go = GameObject.Find("EjectZone");
            if (go == null)
            {
                go = new GameObject("EjectZone");
                Undo.RegisterCreatedObjectUndo(go, "Create EjectZone");
                Debug.Log("[Editor] Создан EjectZone.");
            }
            else
            {
                Debug.Log("[Editor] EjectZone уже есть, переиспользуем.");
            }

            go.transform.position = EjectZonePosition;
            go.transform.rotation = Quaternion.identity;

            var box = go.GetComponent<BoxCollider>();
            if (box == null)
                box = Undo.AddComponent<BoxCollider>(go);
            box.isTrigger = true;
            box.size       = EjectZoneSize;

            // Триггеры требуют Rigidbody хотя бы на одном из участников (CLAUDE.md, КРИТИЧНО)
            var rb = go.GetComponent<Rigidbody>();
            if (rb == null)
                rb = Undo.AddComponent<Rigidbody>(go);
            rb.isKinematic = true;
            rb.useGravity  = false;

            if (go.GetComponent<EjectTriggerZone>() == null)
            {
                Undo.AddComponent<EjectTriggerZone>(go);
                Debug.Log("[Editor] Добавлен EjectTriggerZone на EjectZone.");
            }
        }

        /// <summary>
        /// Миграционный шаг: приземление теперь определяется по успокоению ragdoll
        /// (velocity-based, DriverFlightService), триггер-зона LandingZone упразднена.
        /// Если на сцене остался устаревший GameObject "LandingZone" — удаляем его.
        /// Идемпотентно: если объекта нет, ничего не делаем.
        /// </summary>
        private static void RemoveLegacyLandingZone()
        {
            var go = GameObject.Find("LandingZone");
            if (go == null)
                return;

            Undo.DestroyObjectImmediate(go);
            Debug.Log("[Editor] Удалён устаревший LandingZone (приземление теперь по успокоению ragdoll).");
        }

        private static GameObject CreatePlayerCar()
        {
            var existing = GameObject.Find("Car_Sport_09");
            if (existing != null)
            {
                Debug.Log("[Editor] Car_Sport_09 уже есть на сцене, переиспользуем.");
                return existing;
            }

            var carPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CarPrefabPath);
            if (carPrefab == null)
            {
                Debug.LogWarning("[Editor] Car_Sport_09.prefab не найден по " + CarPrefabPath +
                                  ". Машина не заспавнена — заполнить _sceneCars придётся вручную.");
                return null;
            }

            var carGo = (GameObject)PrefabUtility.InstantiatePrefab(carPrefab);
            Undo.RegisterCreatedObjectUndo(carGo, "Instantiate Player Car");
            carGo.name = "Car_Sport_09";
            carGo.transform.position = new Vector3(0f, 0.5f, 0f);
            carGo.transform.rotation = Quaternion.identity; // смотрит в +Z
            Debug.Log("[Editor] Инстанцирован Car_Sport_09 в (0, 0.5, 0), смотрит в +Z.");
            return carGo;
        }

        private static CameraRig CreateCameraRig()
        {
            // Camera rig мигрировал на Cinemachine 3.x — вся сборка CinemachineBrain +
            // CM_ChaseCamera + CM_OrbitCamera + CameraRig делегирована переиспользуемым
            // методам SetupCinemachineCameraRigEditor (замена ChaseCarCameraBehaviour/
            // OrbitDriverCameraBehaviour на CinemachineChaseCameraBehaviour/
            // CinemachineOrbitDriverCameraBehaviour). Target не выставляется здесь —
            // ICameraService переключает CameraRig в рантайме (как и раньше).
            SetupCinemachineCameraRigEditor.EnsureBrain();
            SetupCinemachineCameraRigEditor.RemoveMissingScriptsInScene();

            var chaseCameraConfig = AssetDatabase.LoadAssetAtPath<ChaseCameraConfig>(ChaseCameraConfigPath);
            if (chaseCameraConfig == null)
                Debug.LogWarning("[Editor] ChaseCameraConfig_Default не найден по " + ChaseCameraConfigPath + ". Поле _config не заполнено.");
            var chase = SetupCinemachineCameraRigEditor.EnsureChaseCamera(chaseCameraConfig, priority: 10);

            var orbitCameraConfig = AssetDatabase.LoadAssetAtPath<OrbitCameraConfig>(OrbitCameraConfigPath);
            if (orbitCameraConfig == null)
                Debug.LogWarning("[Editor] OrbitCameraConfig_HighJump не найден по " + OrbitCameraConfigPath + ". Поле _config не заполнено.");
            var orbit = SetupCinemachineCameraRigEditor.EnsureOrbitCamera(orbitCameraConfig, priority: 0);

            Camera mainCam = Camera.main;
            GameObject camGo = mainCam != null ? mainCam.gameObject : GameObject.Find("Main Camera");
            if (camGo != null)
            {
                camGo.transform.position = new Vector3(0f, 5f, -10f);
                camGo.transform.rotation = Quaternion.Euler(15f, 0f, 0f);
            }
            else
            {
                Debug.LogWarning("[Editor] Main Camera не найдена после EnsureBrain() — позиция камеры не выставлена.");
            }

            var rig = SetupCinemachineCameraRigEditor.EnsureCameraRig(camGo, chase, orbit);

            Debug.Log("[Editor] Camera rig (Cinemachine) собран: chase+orbit+CameraRig на Main Camera.");
            return rig;
        }

        private static void CreateDirectionalLight()
        {
            var allLights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            foreach (var l in allLights)
            {
                if (l.type == LightType.Directional)
                {
                    Debug.Log("[Editor] Directional Light уже есть, пропускаем.");
                    return;
                }
            }

            var lightGo = new GameObject("Directional Light");
            Undo.RegisterCreatedObjectUndo(lightGo, "Create Directional Light");
            var lightComp = Undo.AddComponent<Light>(lightGo);
            lightComp.type      = LightType.Directional;
            lightComp.intensity = 1.2f;
            lightGo.transform.rotation = Quaternion.Euler(55f, -30f, 0f);
            Debug.Log("[Editor] Создан Directional Light (intensity=1.2, Euler 55/-30/0).");
        }

        private static void CreateDebugOverlay()
        {
            var canvasGo = GameObject.Find("DebugOverlay");
            Canvas canvas;

            if (canvasGo == null)
            {
                canvasGo = new GameObject("DebugOverlay");
                Undo.RegisterCreatedObjectUndo(canvasGo, "Create DebugOverlay Canvas");
                canvas = Undo.AddComponent<Canvas>(canvasGo);
                Undo.AddComponent<CanvasScaler>(canvasGo);
                Undo.AddComponent<GraphicRaycaster>(canvasGo);
                Debug.Log("[Editor] Создан DebugOverlay Canvas.");
            }
            else
            {
                canvas = canvasGo.GetComponent<Canvas>();
                if (canvas == null)
                    canvas = Undo.AddComponent<Canvas>(canvasGo);
                if (canvasGo.GetComponent<CanvasScaler>() == null)
                    Undo.AddComponent<CanvasScaler>(canvasGo);
                if (canvasGo.GetComponent<GraphicRaycaster>() == null)
                    Undo.AddComponent<GraphicRaycaster>(canvasGo);
                Debug.Log("[Editor] DebugOverlay Canvas уже есть, переиспользуем.");
            }

            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            // ── Дочерний Text ────────────────────────────────────────────────
            Transform textTransform = canvasGo.transform.Find("OverlayText");
            Text text;

            if (textTransform == null)
            {
                var textGo = new GameObject("OverlayText");
                Undo.RegisterCreatedObjectUndo(textGo, "Create OverlayText");
                textGo.transform.SetParent(canvasGo.transform, false);
                text = Undo.AddComponent<Text>(textGo);
                Debug.Log("[Editor] Создан OverlayText.");
            }
            else
            {
                text = textTransform.GetComponent<Text>();
                if (text == null)
                    text = Undo.AddComponent<Text>(textTransform.gameObject);
                Debug.Log("[Editor] OverlayText уже есть, переиспользуем.");
            }

            text.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize  = 32;
            text.color     = Color.black;
            text.alignment = TextAnchor.UpperLeft;

            var rect = text.rectTransform;
            rect.anchorMin        = new Vector2(0f, 1f);
            rect.anchorMax        = new Vector2(0f, 1f);
            rect.pivot            = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(20f, -20f);
            rect.sizeDelta         = new Vector2(500f, 200f);

            // ── HighJumpDebugOverlayController на корне Canvas ─────────────────
            var overlayController = canvasGo.GetComponent<HighJumpDebugOverlayController>();
            if (overlayController == null)
            {
                overlayController = Undo.AddComponent<HighJumpDebugOverlayController>(canvasGo);
                Debug.Log("[Editor] Добавлен HighJumpDebugOverlayController на DebugOverlay.");
            }

            var overlaySo = new SerializedObject(overlayController);
            var textProp = overlaySo.FindProperty("_text");
            if (textProp != null) textProp.objectReferenceValue = text;
            overlaySo.ApplyModifiedProperties();
            Debug.Log("[Editor] HighJumpDebugOverlayController._text = OverlayText.");
        }

        private static GameLifetimeScope CreateGameLifetimeScope()
        {
            var existing = Object.FindAnyObjectByType<GameLifetimeScope>();
            if (existing != null)
            {
                Debug.Log("[Editor] GameLifetimeScope уже есть, переиспользуем.");
                return existing;
            }

            var scopeGo = new GameObject("GameLifetimeScope");
            Undo.RegisterCreatedObjectUndo(scopeGo, "Create GameLifetimeScope");
            var scope = Undo.AddComponent<GameLifetimeScope>(scopeGo);
            Debug.Log("[Editor] Создан GameLifetimeScope.");
            return scope;
        }

        private static void CreateGameBootstrap()
        {
            var existing = Object.FindAnyObjectByType<FlatOutLike.GameBootstrap>();
            if (existing == null)
            {
                var bootstrapGo = new GameObject("GameBootstrap");
                Undo.RegisterCreatedObjectUndo(bootstrapGo, "Create GameBootstrap");
                Undo.AddComponent<FlatOutLike.GameBootstrap>(bootstrapGo);
                Debug.Log("[Editor] Создан GameBootstrap.");
            }
            else
            {
                Debug.Log("[Editor] GameBootstrap уже есть, пропускаем.");
            }
        }

        private static void FillScopeReferences(GameLifetimeScope scope, GameObject car, CameraRig cameraRig)
        {
            if (scope == null)
            {
                Debug.LogWarning("[Editor] GameLifetimeScope отсутствует — ссылки не заполнены.");
                return;
            }

            var so = new SerializedObject(scope);

            // _sceneCars
            var sceneCarsProp = so.FindProperty("_sceneCars");
            if (sceneCarsProp != null)
            {
                if (car != null)
                {
                    sceneCarsProp.arraySize = 1;
                    sceneCarsProp.GetArrayElementAtIndex(0).objectReferenceValue = car;
                }
                else
                {
                    sceneCarsProp.arraySize = 0;
                }
            }

            // _damageConfig
            AssignConfig<DamageConfig>(so, "_damageConfig", DamageConfigPath,
                "FlatOutLike/Configs/Create Default DamageConfig");

            // _vfxConfig
            AssignConfig<VfxConfig>(so, "_vfxConfig", VfxConfigPath,
                "FlatOutLike/Configs/Create Default VfxConfig");

            // _driverEjectConfig
            AssignConfig<DriverEjectConfig>(so, "_driverEjectConfig", DriverEjectConfigPath,
                "FlatOutLike/Configs/Create HighJump DriverEjectConfig");

            // _orbitCameraConfig
            AssignConfig<OrbitCameraConfig>(so, "_orbitCameraConfig", OrbitCameraConfigPath,
                "FlatOutLike/Configs/Create Default OrbitCameraConfig");

            // _driverFlightConfig
            AssignConfig<DriverFlightConfig>(so, "_driverFlightConfig", DriverFlightConfigPath,
                "FlatOutLike/Configs/Create Default DriverFlightConfig");

            // _highJumpRulesConfig
            AssignConfig<HighJumpRulesConfig>(so, "_highJumpRulesConfig", HighJumpRulesConfigPath,
                "FlatOutLike/Configs/Create Default HighJumpRulesConfig");

            // _cameraRig
            var cameraRigProp = so.FindProperty("_cameraRig");
            if (cameraRigProp != null)
                cameraRigProp.objectReferenceValue = cameraRig;
            else
                Debug.LogWarning("[Editor] Поле GameLifetimeScope._cameraRig не найдено.");

            // _derbyMatchConfig — умышленно НЕ заполняем: сцена не Derby-путь.

            so.ApplyModifiedProperties();
            Debug.Log("[Editor] GameLifetimeScope: ссылки заполнены (кроме _derbyMatchConfig — умышленно).");
        }

        private static void AssignConfig<T>(SerializedObject scopeSo, string fieldName, string assetPath, string createMenuItem)
            where T : Object
        {
            var prop = scopeSo.FindProperty(fieldName);
            if (prop == null)
            {
                Debug.LogWarning("[Editor] Поле GameLifetimeScope." + fieldName + " не найдено. " +
                                  "Возможно, рантайм Шага 7 ещё не скомпилировался.");
                return;
            }

            var asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            if (asset == null)
            {
                Debug.LogWarning("[Editor] " + typeof(T).Name + " не найден по " + assetPath +
                                  ". Запусти '" + createMenuItem + "' сначала. Поле " + fieldName + " останется null.");
                return;
            }

            prop.objectReferenceValue = asset;
        }
    }
}
#endif
