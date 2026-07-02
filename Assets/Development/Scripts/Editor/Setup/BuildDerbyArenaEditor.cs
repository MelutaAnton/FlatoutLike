#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using FlatOutLike.Controllers.Camera;
using FlatOutLike.Data.Configs.Camera;
using FlatOutLike.Installers;
using FlatOutLike.Models;

namespace FlatOutLike.Editor.Setup
{
    /// <summary>
    /// Создаёт или открывает сцену Derby_Arena_01.unity и наполняет её объектами
    /// для рантайм-спавна машин через CarFactory (шаг 5a Derby).
    ///
    /// Что создаётся:
    ///   • StadiumVisual  — визуал из ThirdParty battle_stadium_003.prefab (ссылка, без правок).
    ///                      Если prefab не найден — создаётся запасной Plane для визуала.
    ///   • ArenaFloor     — Plane (60x60) с MeshCollider: функциональный пол арены.
    ///   • ArenaWalls     — 4 невидимых BoxCollider на слое Wall: замкнутый периметр.
    ///   • Directional Light, Main Camera с ChaseCarCameraBehaviour.
    ///     _config = ChaseCameraConfig_Default; _target не задан (выставляется в рантайме
    ///     через GameLifetimeScope.SetTarget после спавна машины игрока).
    ///   • GameLifetimeScope + GameBootstrap.
    ///
    /// Слой Wall должен быть зарегистрирован ДО запуска этого скрипта.
    /// NavMesh НЕ запекается — AI использует Rigidbody-физику.
    ///
    /// Запускать ПОСЛЕ:
    ///   FlatOutLike/Setup/Create Physics Layers
    ///   FlatOutLike/Setup/Build Enemy Car Prefab
    /// </summary>
    public static class BuildDerbyArenaEditor
    {
        private const string ScenePath              = "Assets/Development/Scenes/Derby_Arena_01.unity";
        private const string ScenesFolder           = "Assets/Development/Scenes";
        private const string StadiumPrefabPath      = "Assets/ThirdParty/Battle_Stadium/Prefabs/Stadium/battle_stadium_003.prefab";
        private const string ChaseCameraConfigPath  = "Assets/Development/Data/Configs/Camera/ChaseCameraConfig_Default.asset";

        // Параметры арены (прямоугольник 60x60, стены 4 единицы высотой)
        private const float ArenaHalfSize = 30f;
        private const float WallHeight    = 4f;
        private const float WallThickness = 1f;

        [MenuItem("FlatOutLike/Scenes/Build Derby Arena")]
        public static void BuildDerbyArena()
        {
            Undo.SetCurrentGroupName("Build Derby Arena");
            int undoGroup = Undo.GetCurrentGroup();

            // ── 1. Открываем или создаём сцену ───────────────────────────────
            var scene = OpenOrCreateScene();

            // ── 2. Визуальный стадион (ThirdParty-префаб, ссылка без правок) ─
            CreateStadiumVisual();

            // ── 3. Функциональный пол с коллайдером ─────────────────────────
            CreateArenaFloor();

            // ── 4. Невидимые стены на слое Wall (замкнутый периметр) ─────────
            CreateArenaWalls();

            // ── 5. Directional Light ──────────────────────────────────────────
            CreateDirectionalLight();

            // ── 6. Chase-камера (Main Camera + ChaseCarCameraBehaviour + _config) ──
            CreateChaseCamera();

            // ── 7. GameLifetimeScope ──────────────────────────────────────────
            CreateGameLifetimeScope();

            // ── 8. GameBootstrap ──────────────────────────────────────────────
            CreateGameBootstrap();

            // ── 9. Сохраняем сцену ───────────────────────────────────────────
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("[Editor] Сцена сохранена: " + ScenePath);

            Undo.CollapseUndoOperations(undoGroup);
            Debug.Log("[Editor] Build Derby Arena завершён. " +
                      "Следующий шаг: FlatOutLike/Spawn/Populate Derby Arena Spawn Points.");
        }

        // ─────────────────────────────────────────────────────────────────────

        private static Scene OpenOrCreateScene()
        {
            // Убеждаемся, что папка Scenes существует (Boot.unity уже там, но на всякий случай)
            if (!AssetDatabase.IsValidFolder(ScenesFolder))
                AssetDatabase.CreateFolder("Assets/Development", "Scenes");

            string fullPath = System.IO.Path.Combine(
                System.IO.Directory.GetCurrentDirectory(), ScenePath);

            if (System.IO.File.Exists(fullPath))
            {
                var existing = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                Debug.Log("[Editor] Открыта существующая Derby_Arena_01.unity.");
                return existing;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("[Editor] Создана новая Derby_Arena_01.unity.");
            return scene;
        }

        private static void CreateStadiumVisual()
        {
            // Используем ThirdParty-префаб как визуальный слой.
            // Содержимое пакета не редактируем — только инстанцируем ссылкой.
            // Функциональные коллайдеры обеспечивают ArenaFloor и ArenaWalls ниже.
            var existing = GameObject.Find("StadiumVisual");
            if (existing != null)
            {
                Debug.Log("[Editor] StadiumVisual уже есть, пропускаем.");
                return;
            }

            var stadiumPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(StadiumPrefabPath);
            if (stadiumPrefab == null)
            {
                Debug.LogWarning("[Editor] battle_stadium_003.prefab не найден по " + StadiumPrefabPath +
                                 ". Используем запасной Plane-визуал (StadiumVisual).");
                CreateFallbackFloorVisual();
                return;
            }

            var stadiumGo = (GameObject)PrefabUtility.InstantiatePrefab(stadiumPrefab);
            Undo.RegisterCreatedObjectUndo(stadiumGo, "Instantiate Stadium Visual");
            stadiumGo.name               = "StadiumVisual";
            stadiumGo.transform.position = Vector3.zero;
            // ThirdParty-содержимое не правим (CLAUDE.md: Assets/ThirdParty не редактируются)
            Debug.Log("[Editor] Инстанцирован battle_stadium_003.prefab как StadiumVisual " +
                      "(ссылка без правок). Функциональный пол/стены обеспечены отдельными коллайдерами.");
        }

        /// <summary>
        /// Запасной визуальный пол для случая, когда ThirdParty-стадион недоступен.
        /// Создаётся ТОЛЬКО когда battle_stadium_003.prefab не найден.
        /// </summary>
        private static void CreateFallbackFloorVisual()
        {
            var plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            Undo.RegisterCreatedObjectUndo(plane, "Create Fallback Floor Visual");
            plane.name               = "StadiumVisual";
            // Plane по умолчанию 10x10 единиц; scale (6,1,6) → 60x60
            plane.transform.localScale = new Vector3(ArenaHalfSize / 5f, 1f, ArenaHalfSize / 5f);
            // Убираем MeshCollider с визуального пола — функциональный коллайдер
            // будет отдельно в ArenaFloor
            var mc = plane.GetComponent<MeshCollider>();
            if (mc != null) Undo.DestroyObjectImmediate(mc);
            Debug.Log("[Editor] Создан запасной визуальный пол StadiumVisual (Plane 60x60).");
        }

        private static void CreateArenaFloor()
        {
            // Plane с MeshCollider — функциональный пол арены.
            // Независимо от того, есть ли у ThirdParty-стадиона свой пол,
            // этот объект гарантирует коллизию для физики машин.
            var existing = GameObject.Find("ArenaFloor");
            if (existing != null)
            {
                Debug.Log("[Editor] ArenaFloor уже есть, пропускаем.");
                return;
            }

            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            Undo.RegisterCreatedObjectUndo(floor, "Create Arena Floor");
            floor.name               = "ArenaFloor";
            floor.transform.position = Vector3.zero;
            // Plane 10x10 × scale(6,1,6) = 60x60 единиц; совпадает с размером стен
            floor.transform.localScale = new Vector3(ArenaHalfSize / 5f, 1f, ArenaHalfSize / 5f);
            // MeshCollider уже есть на Plane из коробки — не удаляем
            Debug.Log("[Editor] Создан ArenaFloor — Plane 60x60, MeshCollider (функциональный пол).");
        }

        private static void CreateArenaWalls()
        {
            // 4 невидимых BoxCollider-стены формируют замкнутый периметр арены.
            // Слой Wall нужен для AI-сенсоров (AISensorConfig._obstacleLayerMask = Wall|Hazard).
            var existing = GameObject.Find("ArenaWalls");
            if (existing != null)
            {
                Debug.Log("[Editor] ArenaWalls уже есть, пропускаем.");
                return;
            }

            int wallLayer = LayerMask.NameToLayer(Layers.Wall);
            if (wallLayer < 0)
            {
                Debug.LogWarning("[Editor] Слой '" + Layers.Wall + "' не зарегистрирован. " +
                                 "Запусти 'FlatOutLike/Setup/Create Physics Layers' сначала. " +
                                 "Стены будут созданы без слоя Wall — AI их не увидит.");
            }

            var wallsParent = new GameObject("ArenaWalls");
            Undo.RegisterCreatedObjectUndo(wallsParent, "Create ArenaWalls");

            // Длина стены = ширина арены + толщина (углы без щелей)
            float wallLen = ArenaHalfSize * 2f + WallThickness;

            // North (+Z), South (-Z), East (+X), West (-X)
            CreateWall(wallsParent, "WallNorth",
                pos:  new Vector3(0f, WallHeight / 2f, ArenaHalfSize + WallThickness / 2f),
                size: new Vector3(wallLen, WallHeight, WallThickness),
                wallLayer);

            CreateWall(wallsParent, "WallSouth",
                pos:  new Vector3(0f, WallHeight / 2f, -ArenaHalfSize - WallThickness / 2f),
                size: new Vector3(wallLen, WallHeight, WallThickness),
                wallLayer);

            CreateWall(wallsParent, "WallEast",
                pos:  new Vector3(ArenaHalfSize + WallThickness / 2f, WallHeight / 2f, 0f),
                size: new Vector3(WallThickness, WallHeight, wallLen),
                wallLayer);

            CreateWall(wallsParent, "WallWest",
                pos:  new Vector3(-ArenaHalfSize - WallThickness / 2f, WallHeight / 2f, 0f),
                size: new Vector3(WallThickness, WallHeight, wallLen),
                wallLayer);

            Debug.Log("[Editor] ArenaWalls создан: 4 стены " +
                      (ArenaHalfSize * 2f) + "x" + WallHeight + " ед., слой " + Layers.Wall + ".");
        }

        private static void CreateWall(GameObject parent, string wallName, Vector3 pos, Vector3 size, int layer)
        {
            var wallGo = new GameObject(wallName);
            Undo.RegisterCreatedObjectUndo(wallGo, "Create " + wallName);
            wallGo.transform.SetParent(parent.transform);
            wallGo.transform.localPosition = pos;
            if (layer >= 0) wallGo.layer = layer;

            // BoxCollider.size задаётся в локальном пространстве объекта;
            // localScale у wallGo = (1,1,1), поэтому size == world-размер.
            var bc = Undo.AddComponent<BoxCollider>(wallGo);
            bc.size = size;

            Debug.Log("[Editor] Создана стена " + wallName +
                      " pos=" + pos + " size=" + size +
                      " layer=" + (layer >= 0 ? layer.ToString() : "Default") + ".");
        }

        private static void CreateDirectionalLight()
        {
            // Ищем по типу, не по имени — чтобы не дублировать DefaultDirectionalLight
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

        private static void CreateChaseCamera()
        {
            // Chase-камера: Main Camera с компонентом ChaseCarCameraBehaviour.
            // _config заполняется здесь через SerializedObject.
            // _target НЕ задаём — после спавна игрока GameLifetimeScope зовёт
            // ICameraService.FollowPlayerCar (Шаг 6), который активирует chase через CameraRig.
            Camera mainCam = Camera.main;
            GameObject camGo;

            if (mainCam == null)
            {
                camGo = new GameObject("Main Camera");
                Undo.RegisterCreatedObjectUndo(camGo, "Create Main Camera");
                camGo.tag = "MainCamera";
                Undo.AddComponent<Camera>(camGo);
                Undo.AddComponent<AudioListener>(camGo);
                Debug.Log("[Editor] Создан Main Camera GameObject.");
            }
            else
            {
                camGo = mainCam.gameObject;
                Debug.Log("[Editor] Найдена существующая Main Camera, переиспользуем.");
            }

            // Начальная позиция — разумная стартовая точка;
            // ChaseCarCameraBehaviour перехватит управление в первом же LateUpdate.
            camGo.transform.position = new Vector3(0f, 10f, -15f);
            camGo.transform.rotation = Quaternion.Euler(20f, 0f, 0f);

            // Добавляем ChaseCarCameraBehaviour идемпотентно
            var chaseCam = camGo.GetComponent<ChaseCarCameraBehaviour>();
            if (chaseCam == null)
            {
                chaseCam = Undo.AddComponent<ChaseCarCameraBehaviour>(camGo);
                Debug.Log("[Editor] ChaseCarCameraBehaviour добавлен на Main Camera.");
            }
            else
            {
                Debug.Log("[Editor] ChaseCarCameraBehaviour уже есть на Main Camera, переиспользуем.");
            }

            // Заполняем _config; _target не трогаем — выставляется в рантайме
            var chaseCamConfig = AssetDatabase.LoadAssetAtPath<ChaseCameraConfig>(ChaseCameraConfigPath);
            if (chaseCamConfig == null)
            {
                Debug.LogWarning("[Editor] ChaseCameraConfig_Default не найден по " + ChaseCameraConfigPath +
                                 ". Поле _config не заполнено. Создай ассет и повтори запуск скрипта.");
            }
            else
            {
                var chaseCamSo = new SerializedObject(chaseCam);
                chaseCamSo.FindProperty("_config").objectReferenceValue = chaseCamConfig;
                chaseCamSo.ApplyModifiedProperties();
                Debug.Log("[Editor] ChaseCarCameraBehaviour._config = ChaseCameraConfig_Default.");
            }

            Debug.Log("[Editor] Chase-камера настроена на Main Camera. " +
                      "_target не задан — GameLifetimeScope.SetTarget вызывается при спавне игрока.");
        }

        private static void CreateGameLifetimeScope()
        {
            var existing = Object.FindAnyObjectByType<GameLifetimeScope>();
            if (existing == null)
            {
                var scopeGo = new GameObject("GameLifetimeScope");
                Undo.RegisterCreatedObjectUndo(scopeGo, "Create GameLifetimeScope");
                Undo.AddComponent<GameLifetimeScope>(scopeGo);
                Debug.Log("[Editor] Создан GameLifetimeScope.");
            }
            else
            {
                Debug.Log("[Editor] GameLifetimeScope уже есть, пропускаем.");
            }
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
    }
}
#endif
