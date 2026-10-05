#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using FlatOutLike.Editor.Setup;
using FlatOutLike.Views.UI.Overlay;
using Object = UnityEngine.Object;

namespace FlatOutLike.Editor.UI
{
    /// <summary>
    /// Шаг 10 (Polish) + миграция на префаб: наполняет ТЕКУЩУЮ игровую сцену
    /// (Derby_Arena_01, Stunts_HighJump) оверлеем паузы — Canvas'ом "PauseOverlayCanvas"
    /// ПОВЕРХ HUD и мобильного ввода (тот держит sortingOrder=50 — см.
    /// BuildMobileInputCanvasEditor, здесь 60, чтобы пауза перехватывала тапы даже поверх
    /// мобильных элементов управления).
    ///
    /// Повторяющийся канвас — единый префаб <see cref="PrefabPath"/> с connected-инстансами
    /// в сценах (образец флоу сохранения — BuildMobileInputCanvasEditor, MobileInputCanvas).
    ///
    /// Миграционная идемпотентность (скрипт уже прогонялся ДО перехода на префаб — в сценах
    /// живут НЕ-префабные копии PauseOverlayCanvas):
    ///  - Ассета ещё нет → собираем канвас как раньше (переиспользуя сценную копию, если она
    ///    есть, иначе с нуля) и ВПЕРВЫЕ сохраняем как префаб через
    ///    SaveAsPrefabAssetAndConnect — сценный объект становится connected-инстансом.
    ///  - Ассет уже есть, а в сцене — connected-инстанс этого же префаба → ничего не
    ///    пересобираем, только перепрошиваем ссылки.
    ///  - Ассет уже есть, а в сцене — старая НЕ-префабная копия (или копия другого префаба) →
    ///    копия сносится, на её месте создаётся connected-инстанс. Вёрстка не теряется: префаб
    ///    был сохранён из идентичной копии, которую этот же скрипт когда-то построил в первой
    ///    прогнанной сцене — все PauseOverlayCanvas в проекте строились одним кодом.
    ///  - Ассет уже есть, сценного объекта нет → просто инстанцируем префаб.
    ///
    /// PauseOverlayView вешается на корень Canvas'а, Panel стартует НЕАКТИВНОЙ — включает
    /// рантайм при паузе. Прошивка _panel/_resumeButton/_leaveButton — на этапе сборки (только
    /// когда префаба ещё нет); ссылка на сам View в GameLifetimeScope._pauseOverlayView — через
    /// BindMetaUIRefsEditor (единая точка прошивки, см. Шаг 8c/8d) — выполняется в КАЖДОМ кейсе.
    ///
    /// Порядок использования при миграции: прогнать меню сначала в Derby_Arena_01 (там
    /// сценная копия станет префабом + connected-инстансом), затем в Stunts_HighJump (тамошняя
    /// копия будет снесена и заменена инстансом того же префаба).
    ///
    /// Запускать на ОТКРЫТОЙ игровой сцене, ПОСЛЕ:
    ///   FlatOutLike/Tools/Generate TMP Font Assets
    /// </summary>
    public static class BuildPauseOverlayEditor
    {
        private const string CardBgPath   = UIBuildHelpers.UiPackRoot + "/Buttons/png/button2.png";
        private const string ResumeButtonPath = UIBuildHelpers.UiPackRoot + "/Buttons/png/button1.png";
        private const string LeaveButtonPath  = UIBuildHelpers.UiPackRoot + "/Buttons/png/back button.png";

        /// <summary>ПОВЕРХ мобильного ввода (тот держит 50, см. BuildMobileInputCanvasEditor).</summary>
        private const int SortingOrder = 60;

        public const string PrefabPath = "Assets/Development/Prefabs/UI/PauseOverlayCanvas.prefab";

        [MenuItem("FlatOutLike/UI/Build Pause Overlay")]
        public static void BuildPauseOverlay()
        {
            Undo.SetCurrentGroupName("Build Pause Overlay (Step 10 + Prefab Migration)");
            int undoGroup = Undo.GetCurrentGroup();

            // Идемпотентность — ищем ИМЕННО компонент PauseOverlayView, не по имени
            // GameObject (панель на сцене может быть неактивна вместе со всем канвасом).
            var existingView = Object.FindAnyObjectByType<PauseOverlayView>(FindObjectsInactive.Include);
            GameObject sceneGo = existingView != null ? existingView.gameObject : null;

            var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);

            GameObject canvasGo;
            PauseOverlayView view;

            if (prefabAsset != null && sceneGo != null && PrefabUtility.IsPartOfPrefabInstance(sceneGo)
                && PrefabUtility.GetCorrespondingObjectFromSource(sceneGo) == prefabAsset)
            {
                // Уже мигрировано — переиспользуем connected-инстанс без пересборки.
                canvasGo = sceneGo;
                view = existingView;
                Debug.Log("[Editor] PauseOverlayCanvas уже мигрирован на префаб, переиспользуем инстанс.");
            }
            else if (prefabAsset != null && sceneGo != null)
            {
                // Старая НЕ-префабная копия (или инстанс другого объекта) — сносим и
                // ставим на её место connected-инстанс актуального префаба.
                Undo.DestroyObjectImmediate(sceneGo);
                Debug.Log("[Editor] Миграция: локальная копия PauseOverlayCanvas заменена инстансом префаба.");

                canvasGo = (GameObject)PrefabUtility.InstantiatePrefab(prefabAsset);
                Undo.RegisterCreatedObjectUndo(canvasGo, "Instantiate PauseOverlayCanvas");
                view = canvasGo.GetComponent<PauseOverlayView>();
            }
            else if (prefabAsset != null)
            {
                // Сценного объекта нет вовсе — просто инстанцируем префаб.
                canvasGo = (GameObject)PrefabUtility.InstantiatePrefab(prefabAsset);
                Undo.RegisterCreatedObjectUndo(canvasGo, "Instantiate PauseOverlayCanvas");
                view = canvasGo.GetComponent<PauseOverlayView>();
                Debug.Log("[Editor] PauseOverlayCanvas инстанцирован из префаба " + PrefabPath + ".");
            }
            else
            {
                // Префаба ещё нет — собираем канвас (как раньше), переиспользуя сценную
                // копию, если она уже есть, и сохраняем результат как первый префаб-ассет.
                canvasGo = BuildCanvasFromScratch(sceneGo);
                view = canvasGo.GetComponent<PauseOverlayView>();

                EnsureFolder("Assets/Development/Prefabs", "UI");
                PrefabUtility.SaveAsPrefabAssetAndConnect(canvasGo, PrefabPath, InteractionMode.AutomatedAction);
                Debug.Log("[Editor] Префаб создан: " + PrefabPath + ".");
            }

            // Прошивка GameLifetimeScope._pauseOverlayView — единая точка, во ВСЕХ кейсах.
            BindMetaUIRefsEditor.BindPauseOverlayView(view);

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Undo.CollapseUndoOperations(undoGroup);
            Debug.Log("[Editor] Pause Overlay готов на " + SceneManager.GetActiveScene().name + ".");
        }

        /// <summary>
        /// Собирает канвас паузы с нуля (или доукомплектовывает переданную существующую
        /// копию) — вызывается только пока префаб-ассет ещё не создан.
        /// </summary>
        private static GameObject BuildCanvasFromScratch(GameObject existingGo)
        {
            GameObject canvasGo = existingGo;

            if (canvasGo == null)
            {
                canvasGo = new GameObject("PauseOverlayCanvas",
                    typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                Undo.RegisterCreatedObjectUndo(canvasGo, "Create PauseOverlayCanvas");
                Debug.Log("[Editor] Создан PauseOverlayCanvas.");
            }
            else
            {
                Debug.Log("[Editor] PauseOverlayCanvas уже есть на сцене, переиспользуем.");
            }

            var canvas = canvasGo.GetComponent<Canvas>();
            if (canvas == null) canvas = Undo.AddComponent<Canvas>(canvasGo);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = Undo.AddComponent<CanvasScaler>(canvasGo);
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            if (canvasGo.GetComponent<GraphicRaycaster>() == null)
                Undo.AddComponent<GraphicRaycaster>(canvasGo);

            EnsureEventSystem();

            var root = canvasGo.transform;

            // ── Panel (затемнение + карточка) ────────────────────────────────
            var panelRt = UIBuildHelpers.FindOrCreateChild(root, "Panel");
            UIBuildHelpers.Stretch(panelRt);

            var dim = panelRt.GetComponent<Image>();
            if (dim == null) dim = Undo.AddComponent<Image>(panelRt.gameObject);
            dim.sprite = null;
            dim.color = new Color(0f, 0f, 0f, 0.75f);

            var cardRt = UIBuildHelpers.FindOrCreateChild(panelRt, "Card");
            UIBuildHelpers.Anchor(cardRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(700f, 480f));

            var cardImg = cardRt.GetComponent<Image>();
            if (cardImg == null) cardImg = Undo.AddComponent<Image>(cardRt.gameObject);
            cardImg.sprite = UIBuildHelpers.LoadSprite(CardBgPath);
            cardImg.type = Image.Type.Sliced;

            var contentRt = UIBuildHelpers.FindOrCreateChild(cardRt, "Content");
            UIBuildHelpers.Stretch(contentRt);

            var vLayout = contentRt.GetComponent<VerticalLayoutGroup>();
            if (vLayout == null) vLayout = Undo.AddComponent<VerticalLayoutGroup>(contentRt.gameObject);
            vLayout.spacing = 32f;
            vLayout.childAlignment = TextAnchor.MiddleCenter;
            vLayout.childControlWidth = true;
            vLayout.childControlHeight = true;
            vLayout.childForceExpandWidth = true;
            vLayout.childForceExpandHeight = false;
            vLayout.padding = new RectOffset(60, 60, 60, 60);

            var titleLabel = UIBuildHelpers.CreateLabel(contentRt, "TitleLabel", "PAUSED",
                UIBuildHelpers.DisplayFontPath, 72f, UIBuildHelpers.DarkTextColor);
            var titleLe = titleLabel.GetComponent<LayoutElement>();
            if (titleLe == null) titleLe = Undo.AddComponent<LayoutElement>(titleLabel.gameObject);
            titleLe.preferredHeight = 110f;

            var resumeBtn = UIBuildHelpers.CreateButton(contentRt, "ResumeButton", ResumeButtonPath,
                "RESUME", UIBuildHelpers.DisplayFontPath, 34f, UIBuildHelpers.DarkTextColor);
            var resumeLe = resumeBtn.GetComponent<LayoutElement>();
            if (resumeLe == null) resumeLe = Undo.AddComponent<LayoutElement>(resumeBtn.gameObject);
            resumeLe.preferredWidth = 420f;
            resumeLe.preferredHeight = 100f;

            var leaveBtn = UIBuildHelpers.CreateButton(contentRt, "LeaveButton", LeaveButtonPath,
                "LEAVE", UIBuildHelpers.DisplayFontPath, 34f, UIBuildHelpers.DarkTextColor);
            var leaveLe = leaveBtn.GetComponent<LayoutElement>();
            if (leaveLe == null) leaveLe = Undo.AddComponent<LayoutElement>(leaveBtn.gameObject);
            leaveLe.preferredWidth = 420f;
            leaveLe.preferredHeight = 100f;

            // Panel по умолчанию неактивен — рантайм включает при постановке на паузу.
            panelRt.gameObject.SetActive(false);

            var view = canvasGo.GetComponent<PauseOverlayView>();
            if (view == null) view = Undo.AddComponent<PauseOverlayView>(canvasGo);

            var so = new SerializedObject(view);
            SetRef(so, "_panel", panelRt.gameObject);
            SetRef(so, "_resumeButton", resumeBtn);
            SetRef(so, "_leaveButton", leaveBtn);
            so.ApplyModifiedProperties();

            return canvasGo;
        }

        /// <summary>Создаёт EventSystem, если на сцене его ещё нет (например, скрипт запущен
        /// первым, до HUD/мобильного ввода).</summary>
        private static void EnsureEventSystem()
        {
            var existingEs = Object.FindAnyObjectByType<EventSystem>();
            if (existingEs != null) return;

            var esGo = new GameObject("EventSystem");
            Undo.RegisterCreatedObjectUndo(esGo, "Create EventSystem");
            Undo.AddComponent<EventSystem>(esGo);
            Undo.AddComponent<StandaloneInputModule>(esGo);
            Debug.Log("[Editor] Создан EventSystem.");
        }

        private static void EnsureFolder(string parent, string folderName)
        {
            string full = parent + "/" + folderName;
            if (!AssetDatabase.IsValidFolder(full))
                AssetDatabase.CreateFolder(parent, folderName);
        }

        private static void SetRef(SerializedObject so, string propName, Object value)
        {
            var prop = so.FindProperty(propName);
            if (prop != null)
                prop.objectReferenceValue = value;
            else
                Debug.LogWarning("[Editor] Поле PauseOverlayView." + propName + " не найдено (рантайм ещё не скомпилирован?).");
        }
    }
}
#endif
