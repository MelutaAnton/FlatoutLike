#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using FlatOutLike.Editor.Setup;
using FlatOutLike.Installers;
using FlatOutLike.Models;

namespace FlatOutLike.Editor.Scenes
{
    /// <summary>
    /// Шаг 8b: собирает три мета-сцены (MainMenu, Garage, Stunts_Hub) и регистрирует
    /// ВСЕ игровые сцены проекта в Build Settings.
    ///
    /// Что создаётся в каждой из трёх сцен:
    ///   • Main Camera   — tag MainCamera, Solid Color фон, БЕЗ CameraController'ов
    ///                     (мета-сцены статичны, камера не следует ни за чем).
    ///   • GameLifetimeScope + GameBootstrap — Composition Root сцены.
    ///   • UICanvas      — Screen Space - Overlay, CanvasScaler Scale With Screen Size
    ///                     1920x1080, GraphicRaycaster. Пустой — наполнение в Шаге 8c.
    ///   • EventSystem   — + StandaloneInputModule (Active Input Handling = Both).
    ///
    /// После сборки каждой сцены прошиваются ссылки на каталоги (Шаг 8a) через
    /// SetupMetaConfigsEditor.SetupMetaConfigs() — этот метод сам ищет GameLifetimeScope
    /// в АКТИВНОЙ сцене, поэтому вызывается сразу после того, как сцена собрана и открыта.
    ///
    /// Boot.unity дособирается тем же прогоном через уже существующий идемпотентный
    /// BuildBootstrapSceneEditor.BuildBootstrapScene() (без Canvas/EventSystem — Boot
    /// чисто служебная сцена).
    ///
    /// Идемпотентность: повторный запуск не дублирует объекты (проверка по компоненту,
    /// не по имени GameObject) и не дублирует entries в Build Settings.
    ///
    /// Запускать ПОСЛЕ:
    ///   FlatOutLike/Configs/Create Default CarCatalogConfig
    ///   FlatOutLike/Configs/Create Default UpgradeCatalogConfig
    /// </summary>
    public static class BuildMetaScenesEditor
    {
        private const string ScenesFolder = "Assets/Development/Scenes";
        private const string ScenesParent = "Assets/Development";

        private const float UiReferenceWidth  = 1920f;
        private const float UiReferenceHeight = 1080f;

        // Единственное место, где перечислен порядок сцен для Build Settings.
        private static readonly string[] BuildSceneOrder =
        {
            SceneKeys.Boot,
            SceneKeys.MainMenu,
            SceneKeys.Garage,
            SceneKeys.StuntsHub,
            SceneKeys.DerbyArena01,
            SceneKeys.StuntsHighJump,
        };

        [MenuItem("FlatOutLike/Scenes/Build Meta Scenes (Step 8b)")]
        public static void BuildMetaScenes()
        {
            Undo.SetCurrentGroupName("Build Meta Scenes (Step 8b)");
            int undoGroup = Undo.GetCurrentGroup();

            // Запоминаем исходную сцену, чтобы вернуть пользователя в неё в конце.
            string originalScenePath = SceneManager.GetActiveScene().path;

            // finally: даже если сборка одной из сцен бросит исключение,
            // пользователь вернётся в исходную сцену, а Undo-группа схлопнется.
            try
            {
                // ── 1. Boot.unity — дособираем существующим идемпотентным скриптом ──
                BuildBootstrapSceneEditor.BuildBootstrapScene();

                // ── 2. Три мета-сцены ────────────────────────────────────────────
                BuildMetaScene(SceneKeys.MainMenu);
                BuildMetaScene(SceneKeys.Garage);
                BuildMetaScene(SceneKeys.StuntsHub);

                // ── 3. Build Settings — регистрируем все игровые сцены ──────────
                RegisterAllScenesInBuildSettings();

                Debug.Log("[Editor] Build Meta Scenes (Step 8b) завершён.");
            }
            finally
            {
                // ── 4. Возврат в исходную сцену ──────────────────────────────────
                RestoreOriginalScene(originalScenePath);
                Undo.CollapseUndoOperations(undoGroup);
            }
        }

        [MenuItem("FlatOutLike/Scenes/Register All Scenes In Build Settings")]
        public static void RegisterAllScenesInBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            var existingPaths = new HashSet<string>();
            foreach (var s in scenes)
                existingPaths.Add(s.path);

            bool changed = false;

            foreach (var sceneKey in BuildSceneOrder)
            {
                string scenePath = GetScenePath(sceneKey);

                if (existingPaths.Contains(scenePath))
                    continue;

                if (!File.Exists(Path.Combine(Directory.GetCurrentDirectory(), scenePath)))
                {
                    Debug.LogWarning("[Editor] Сцена " + scenePath + " не найдена на диске — " +
                                      "в Build Settings не добавлена. Собери её сначала.");
                    continue;
                }

                scenes.Add(new EditorBuildSettingsScene(scenePath, true));
                existingPaths.Add(scenePath);
                changed = true;
                Debug.Log("[Editor] Добавлена сцена в Build Settings: " + scenePath);
            }

            // Boot должна быть первой в списке (точка входа сборки), не удаляя остальные.
            string bootPath = GetScenePath(SceneKeys.Boot);
            int bootIndex = scenes.FindIndex(s => s.path == bootPath);
            if (bootIndex > 0)
            {
                var bootScene = scenes[bootIndex];
                scenes.RemoveAt(bootIndex);
                scenes.Insert(0, bootScene);
                changed = true;
                Debug.Log("[Editor] Boot.unity перемещена в начало списка Build Settings.");
            }

            if (changed)
            {
                EditorBuildSettings.scenes = scenes.ToArray();
                Debug.Log("[Editor] Build Settings обновлены (" + scenes.Count + " сцен всего, " +
                          "чужие entries не удалены).");
            }
            else
            {
                Debug.Log("[Editor] Build Settings уже содержат все нужные сцены — изменений нет.");
            }
        }

        // ─────────────────────────────────────────────────────────────────────

        private static void BuildMetaScene(string sceneKey)
        {
            string scenePath = GetScenePath(sceneKey);
            var scene = OpenOrCreateScene(scenePath, sceneKey);

            CreateMainCamera();
            CreateGameLifetimeScope();
            CreateGameBootstrap();
            CreateUICanvas();
            CreateEventSystem();

            // Прошиваем ссылки на каталоги (Шаг 8a) в GameLifetimeScope АКТИВНОЙ сцены.
            SetupMetaConfigsEditor.SetupMetaConfigs();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, scenePath);
            Debug.Log("[Editor] Мета-сцена собрана и сохранена: " + scenePath);
        }

        private static Scene OpenOrCreateScene(string scenePath, string sceneKey)
        {
            if (!AssetDatabase.IsValidFolder(ScenesFolder))
            {
                AssetDatabase.CreateFolder(ScenesParent, "Scenes");
                Debug.Log("[Editor] Создана папка " + ScenesFolder);
            }

            string fullPath = Path.Combine(Directory.GetCurrentDirectory(), scenePath);

            if (File.Exists(fullPath))
            {
                var existing = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                Debug.Log("[Editor] Открыта существующая сцена " + sceneKey + ".unity, дособираем недостающее.");
                return existing;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, scenePath);
            Debug.Log("[Editor] Создана новая сцена " + sceneKey + ".unity.");
            return scene;
        }

        private static void RestoreOriginalScene(string originalScenePath)
        {
            if (string.IsNullOrEmpty(originalScenePath))
            {
                Debug.Log("[Editor] Исходная сцена была несохранённой (Untitled) — не восстанавливаем.");
                return;
            }

            if (SceneManager.GetActiveScene().path == originalScenePath)
                return;

            EditorSceneManager.OpenScene(originalScenePath, OpenSceneMode.Single);
            Debug.Log("[Editor] Возврат в исходную сцену: " + originalScenePath);
        }

        private static string GetScenePath(string sceneKey) => ScenesFolder + "/" + sceneKey + ".unity";

        // ── Компоненты мета-сцены ───────────────────────────────────────────────

        private static void CreateMainCamera()
        {
            // Ищем по типу через Camera.main (учитывает тег MainCamera), а не по имени —
            // в сцене может уже быть камера без нужных настроек.
            var cam = Camera.main;
            GameObject camGo;

            if (cam != null)
            {
                camGo = cam.gameObject;
                Debug.Log("[Editor] Main Camera уже есть, переиспользуем.");
            }
            else
            {
                camGo = new GameObject("Main Camera");
                Undo.RegisterCreatedObjectUndo(camGo, "Create Main Camera");
                cam = Undo.AddComponent<Camera>(camGo);
                Debug.Log("[Editor] Создана Main Camera.");
            }

            if (!camGo.CompareTag("MainCamera"))
                camGo.tag = "MainCamera";

            // Мета-сцены статичны — Solid Color фон, без CameraController/Cinemachine.
            cam.clearFlags      = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;

            Debug.Log("[Editor] Main Camera настроена: tag MainCamera, Solid Color фон.");
        }

        private static void CreateGameLifetimeScope()
        {
            var existing = Object.FindAnyObjectByType<GameLifetimeScope>();
            if (existing != null)
            {
                Debug.Log("[Editor] GameLifetimeScope уже есть, пропускаем.");
                return;
            }

            var scopeGo = new GameObject("GameLifetimeScope");
            Undo.RegisterCreatedObjectUndo(scopeGo, "Create GameLifetimeScope");
            Undo.AddComponent<GameLifetimeScope>(scopeGo);
            Debug.Log("[Editor] Создан GameLifetimeScope.");
        }

        private static void CreateGameBootstrap()
        {
            var existing = Object.FindAnyObjectByType<FlatOutLike.GameBootstrap>();
            if (existing != null)
            {
                Debug.Log("[Editor] GameBootstrap уже есть, пропускаем.");
                return;
            }

            var bootstrapGo = new GameObject("GameBootstrap");
            Undo.RegisterCreatedObjectUndo(bootstrapGo, "Create GameBootstrap");
            Undo.AddComponent<FlatOutLike.GameBootstrap>(bootstrapGo);
            Debug.Log("[Editor] Создан GameBootstrap.");
        }

        private static void CreateUICanvas()
        {
            // Идемпотентность по КОМПОНЕНТУ, не по имени (правило editor-files.md):
            // существующий Canvas в сцене переиспользуем, каким бы именем он ни назывался.
            var existingCanvas = Object.FindAnyObjectByType<Canvas>();
            var canvasGo = existingCanvas != null ? existingCanvas.gameObject : null;
            Canvas canvas;

            if (canvasGo == null)
            {
                canvasGo = new GameObject("UICanvas");
                Undo.RegisterCreatedObjectUndo(canvasGo, "Create UICanvas");
                canvas = Undo.AddComponent<Canvas>(canvasGo);
                Undo.AddComponent<CanvasScaler>(canvasGo);
                Undo.AddComponent<GraphicRaycaster>(canvasGo);
                Debug.Log("[Editor] Создан UICanvas.");
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
                Debug.Log("[Editor] UICanvas уже есть, переиспользуем.");
            }

            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(UiReferenceWidth, UiReferenceHeight);
            scaler.screenMatchMode     = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight  = 0.5f;

            Debug.Log("[Editor] UICanvas настроен: Screen Space - Overlay, " +
                      "Scale With Screen Size 1920x1080. Наполнение — Шаг 8c.");
        }

        private static void CreateEventSystem()
        {
            var existing = Object.FindAnyObjectByType<EventSystem>();
            GameObject esGo;

            if (existing != null)
            {
                esGo = existing.gameObject;
                Debug.Log("[Editor] EventSystem уже есть, переиспользуем.");
            }
            else
            {
                esGo = new GameObject("EventSystem");
                Undo.RegisterCreatedObjectUndo(esGo, "Create EventSystem");
                Undo.AddComponent<EventSystem>(esGo);
                Debug.Log("[Editor] Создан EventSystem.");
            }

            // Active Input Handling проекта = Both → StandaloneInputModule работает штатно.
            if (esGo.GetComponent<StandaloneInputModule>() == null)
            {
                Undo.AddComponent<StandaloneInputModule>(esGo);
                Debug.Log("[Editor] Добавлен StandaloneInputModule.");
            }
        }
    }
}
#endif
