#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using FlatOutLike.Editor.Tools;
using FlatOutLike.Installers;
using FlatOutLike.Models;
using FlatOutLike.Views.UI.Common;
using Object = UnityEngine.Object;

namespace FlatOutLike.Editor.UI
{
    /// <summary>
    /// Шаг 11c: собирает канвас фейда между сценами — единый префаб
    /// <see cref="PrefabPath"/> — и ставит его connected-инстанс в четыре сцены
    /// (BaseHub, Garage, Derby_Arena_01, Stunts_HighJump), прошивая
    /// GameLifetimeScope._screenFadeView. Boot не трогаем: там фейда намеренно нет.
    ///
    /// Структура префаба: корень ScreenFadeCanvas (Canvas Overlay, CanvasScaler, GraphicRaycaster,
    /// CanvasGroup, ScreenFadeView) + дочерний Fade (растянутый чёрный Image).
    /// Стартовое состояние — alpha=1 и blocksRaycasts=true: первый кадр новой сцены чёрный, клики
    /// заблокированы, пока рантайм не запустит FadeIn.
    ///
    /// Идемпотентность: префаб существует → дособираем недостающее и перевыставляем инварианты
    /// (alpha/blocksRaycasts/Canvas); длительности ScreenFadeView не трогаем. Сцена с готовым
    /// инстансом префаба → только перепрошиваем ссылку на scope.
    /// </summary>
    public static class BuildScreenFadeCanvasEditor
    {
        public const string PrefabPath = "Assets/Development/Prefabs/UI/ScreenFadeCanvas.prefab";

        private const string RootName = "ScreenFadeCanvas";
        private const string FadeName = "Fade";
        private const string ScopeFadeField = "_screenFadeView";
        private const string ViewCanvasGroupField = "_canvasGroup";

        /// <summary>
        /// Поверх PauseOverlay (60, см. BuildPauseOverlayEditor) и MobileInputCanvas (50,
        /// см. BuildMobileInputCanvasEditor) — фейд перекрывает вообще всё.
        /// </summary>
        private const int SortingOrder = 100;

        private static readonly string[] SceneKeysToProcess =
        {
            SceneKeys.BaseHub,
            SceneKeys.Garage,
            SceneKeys.DerbyArena01,
            SceneKeys.StuntsHighJump,
        };

        [MenuItem("FlatOutLike/UI/Build Screen Fade Canvas (All Scenes)")]
        public static void BuildAllScenes()
        {
            // Открытие сцен в режиме Single выгрузит несохранённые изменения — спрашиваем заранее.
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log("[Editor] Build Screen Fade Canvas отменён: сохранение сцен не подтверждено.");
                return;
            }

            string originalScenePath = SceneManager.GetActiveScene().path;

            BuildOrUpdatePrefab();

            int processed = 0;
            foreach (var key in SceneKeysToProcess)
            {
                if (WireScene(key))
                    processed++;
            }

            // Возвращаем сцену, в которой пользователь был до запуска.
            if (!string.IsNullOrEmpty(originalScenePath)
                && SceneManager.GetActiveScene().path != originalScenePath)
            {
                EditorSceneManager.OpenScene(originalScenePath, OpenSceneMode.Single);
                Debug.Log("[Editor] Возвращена исходная сцена " + originalScenePath + ".");
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[Editor] Build Screen Fade Canvas завершён: обработано сцен " + processed
                      + " из " + SceneKeysToProcess.Length + ".");
        }

        // ── Префаб ─────────────────────────────────────────────────────────────

        private static void BuildOrUpdatePrefab()
        {
            EnsureFolder("Assets/Development/Prefabs", "UI");

            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
            {
                var contents = PrefabUtility.LoadPrefabContents(PrefabPath);
                try
                {
                    ConfigureRoot(contents);
                    PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
                    Debug.Log("[Editor] Префаб обновлён: " + PrefabPath + ".");
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(contents);
                }
            }
            else
            {
                var temp = new GameObject(RootName, typeof(RectTransform));
                try
                {
                    ConfigureRoot(temp);
                    PrefabUtility.SaveAsPrefabAsset(temp, PrefabPath);
                    Debug.Log("[Editor] Префаб создан: " + PrefabPath + ".");
                }
                finally
                {
                    Object.DestroyImmediate(temp);
                }
            }

            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Доукомплектовывает корень и дочерний Fade. Работает и на временном GameObject,
        /// и на содержимом префаба (поэтому без Undo — он неприменим к prefab contents).
        /// </summary>
        private static void ConfigureRoot(GameObject root)
        {
            // ── Canvas ──
            var canvas = root.GetComponent<Canvas>();
            if (canvas == null) canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;

            // Настройки скейлера — как у остальных канвасов проекта (см. BuildPauseOverlayEditor).
            var scaler = root.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            if (root.GetComponent<GraphicRaycaster>() == null)
                root.AddComponent<GraphicRaycaster>();

            // ── CanvasGroup: стартовое состояние — чёрный экран, клики заблокированы ──
            var group = root.GetComponent<CanvasGroup>();
            if (group == null) group = root.AddComponent<CanvasGroup>();
            group.alpha = 1f;
            group.interactable = false;
            group.blocksRaycasts = true;

            // ── ScreenFadeView + прошивка _canvasGroup (длительности не трогаем) ──
            var view = root.GetComponent<ScreenFadeView>();
            if (view == null) view = root.AddComponent<ScreenFadeView>();

            var so = new SerializedObject(view);
            var groupProp = so.FindProperty(ViewCanvasGroupField);
            if (groupProp != null)
            {
                groupProp.objectReferenceValue = group;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            else
            {
                Debug.LogWarning("[Editor] У ScreenFadeView не найдено поле " + ViewCanvasGroupField
                                 + " — ссылка на CanvasGroup не прошита.");
            }

            // ── Fade: растянутый чёрный Image ──
            var fadeTf = root.transform.Find(FadeName);
            GameObject fadeGo;
            if (fadeTf == null)
            {
                fadeGo = new GameObject(FadeName, typeof(RectTransform));
                fadeGo.transform.SetParent(root.transform, false);
            }
            else
            {
                fadeGo = fadeTf.gameObject;
            }

            var fadeRt = fadeGo.GetComponent<RectTransform>();
            fadeRt.anchorMin = Vector2.zero;
            fadeRt.anchorMax = Vector2.one;
            fadeRt.pivot = new Vector2(0.5f, 0.5f);
            fadeRt.offsetMin = Vector2.zero;
            fadeRt.offsetMax = Vector2.zero;

            var image = fadeGo.GetComponent<Image>();
            if (image == null) image = fadeGo.AddComponent<Image>();
            image.sprite = null;
            image.color = Color.black;
            image.raycastTarget = true;   // блокирует клики, пока экран затемнён
            image.maskable = false;
        }

        // ── Сцены ──────────────────────────────────────────────────────────────

        private static bool WireScene(string sceneKey)
        {
            string path = ScenePaths.For(sceneKey);
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
            {
                Debug.LogWarning("[Editor] Сцена не найдена, пропускаем: " + path);
                return false;
            }

            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            Debug.Log("[Editor] Открыта сцена " + path + ".");

            // Префаб грузим ПОСЛЕ OpenScene — открытие выгружает неиспользуемые ассеты.
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                Debug.LogWarning("[Editor] Префаб не загружен: " + PrefabPath + " — сцена " + sceneKey + " пропущена.");
                return false;
            }

            ScreenFadeView instance = FindInstance(prefab);
            if (instance == null)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                Undo.RegisterCreatedObjectUndo(go, "Instantiate ScreenFadeCanvas");
                instance = go.GetComponent<ScreenFadeView>();
                Debug.Log("[Editor] ScreenFadeCanvas инстанцирован в " + sceneKey + ".");
            }
            else
            {
                Debug.Log("[Editor] ScreenFadeCanvas уже есть в " + sceneKey + ", переиспользуем.");
            }

            if (instance == null)
            {
                Debug.LogWarning("[Editor] На инстансе нет ScreenFadeView — прошивка scope пропущена в " + sceneKey + ".");
            }
            else
            {
                WireScope(instance, sceneKey);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Editor] Сцена " + sceneKey + " сохранена.");
            return true;
        }

        /// <summary>
        /// Ищет в открытой сцене инстанс именно нашего префаба (по ScreenFadeView). Если их
        /// несколько — предупреждаем, ничего не удаляем, возвращаем первый.
        /// </summary>
        private static ScreenFadeView FindInstance(GameObject prefab)
        {
            var all = Object.FindObjectsByType<ScreenFadeView>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            ScreenFadeView first = null;
            int count = 0;
            foreach (var view in all)
            {
                if (view == null) continue;
                if (PrefabUtility.GetCorrespondingObjectFromSource(view.gameObject) != prefab) continue;

                count++;
                if (first == null) first = view;
            }

            if (count > 1)
                Debug.LogWarning("[Editor] В сцене найдено " + count + " инстансов ScreenFadeCanvas — удалите лишние вручную; используется первый.");

            return first;
        }

        private static void WireScope(ScreenFadeView instance, string sceneKey)
        {
            var scope = Object.FindAnyObjectByType<GameLifetimeScope>(FindObjectsInactive.Include);
            if (scope == null)
            {
                Debug.LogWarning("[Editor] GameLifetimeScope не найден в " + sceneKey + " — _screenFadeView не прошит.");
                return;
            }

            var so = new SerializedObject(scope);
            var prop = so.FindProperty(ScopeFadeField);
            if (prop == null)
            {
                Debug.LogWarning("[Editor] У GameLifetimeScope нет поля " + ScopeFadeField + " (сцена " + sceneKey + ") — ничего не присвоено.");
                return;
            }

            prop.objectReferenceValue = instance;
            so.ApplyModifiedProperties();
            Debug.Log("[Editor] GameLifetimeScope." + ScopeFadeField + " прошит в " + sceneKey + ".");
        }

        private static void EnsureFolder(string parent, string folderName)
        {
            string full = parent + "/" + folderName;
            if (!AssetDatabase.IsValidFolder(full))
                AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}
#endif
