#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using FlatOutLike.Editor.Setup;
using FlatOutLike.Views.UI.Hud;
using Object = UnityEngine.Object;

namespace FlatOutLike.Editor.UI
{
    /// <summary>
    /// Шаг 8d: заменяет ВРЕМЕННЫЙ debug-оверлей "High Jump" (Text-заглушка, см.
    /// HighJumpDebugOverlayController — сам тип уже удалён из проекта) полноценным MVP-HUD'ом.
    /// Сначала выполняет миграцию — удаляет старый объект "DebugOverlay" по имени
    /// (вместе с Missing Script от удалённого типа), затем строит новый Canvas
    /// (переиспользуется через UIBuildHelpers.EnsureCanvasAndEventSystem) и HighJumpHudView.
    ///
    /// Структура HUD:
    ///   _drivingGroup  — низ по центру: крупная скорость + подсказка "HOLD W".
    ///   _flightGroup   — справа по центру (скрыт до полёта): высота/рекорд/буст.
    ///   _scorePanel    — центр (скрыт до приземления): тёмная подложка, итоговый счёт,
    ///                    NEW RECORD! и кнопка CONTINUE.
    ///
    /// Запускать на ОТКРЫТОЙ сцене Stunts_HighJump, ПОСЛЕ:
    ///   FlatOutLike/Tools/Generate TMP Font Assets
    /// </summary>
    public static class BuildHighJumpHudEditor
    {
        private const string ContinueButtonPath = UIBuildHelpers.UiPackRoot + "/Buttons/png/button1.png";

        private static readonly Color YellowColor = new Color(1f, 0.85f, 0.1f, 1f);

        [MenuItem("FlatOutLike/UI/Build HighJump HUD")]
        public static void BuildHighJumpHud()
        {
            Undo.SetCurrentGroupName("Build HighJump HUD (Step 8d)");
            int undoGroup = Undo.GetCurrentGroup();

            RemoveLegacyDebugOverlay();

            var canvas = UIBuildHelpers.EnsureCanvasAndEventSystem();
            var root = canvas.transform;

            var hudRt = UIBuildHelpers.FindOrCreateChild(root, "HighJumpHud");
            UIBuildHelpers.Stretch(hudRt);

            var drivingGroup = BuildDrivingGroup(hudRt, out var speedLabel);
            var flightGroup = BuildFlightGroup(hudRt, out var heightLabel, out var bestLabel, out var boostLabel);
            var scorePanel = BuildScorePanel(hudRt, out var scoreLabel, out var breakdownLabel, out var newRecordLabel, out var continueButton);

            // Флайт/скор стартуют скрытыми — рантайм включает их по мере хода заезда.
            flightGroup.SetActive(false);
            scorePanel.SetActive(false);

            var view = hudRt.GetComponent<HighJumpHudView>();
            if (view == null) view = Undo.AddComponent<HighJumpHudView>(hudRt.gameObject);

            var so = new SerializedObject(view);
            SetRef(so, "_drivingGroup", drivingGroup);
            SetRef(so, "_speedLabel", speedLabel);
            SetRef(so, "_flightGroup", flightGroup);
            SetRef(so, "_heightLabel", heightLabel);
            SetRef(so, "_bestLabel", bestLabel);
            SetRef(so, "_boostLabel", boostLabel);
            SetRef(so, "_scorePanel", scorePanel);
            SetRef(so, "_scoreLabel", scoreLabel);
            SetRef(so, "_breakdownLabel", breakdownLabel);
            SetRef(so, "_newRecordLabel", newRecordLabel);
            SetRef(so, "_continueButton", continueButton);
            so.ApplyModifiedProperties();

            BindMetaUIRefsEditor.BindHighJumpHudView(view);

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Undo.CollapseUndoOperations(undoGroup);
            Debug.Log("[Editor] HighJump HUD собран на Stunts_HighJump.");
        }

        /// <summary>
        /// Миграция: старый debug-оверлей больше не нужен — удаляем весь его GameObject
        /// (Canvas + Text + HighJumpDebugOverlayController), идемпотентно по имени/компоненту.
        /// </summary>
        private static void RemoveLegacyDebugOverlay()
        {
            // Только по имени: сам тип HighJumpDebugOverlayController УДАЛЁН из проекта
            // (Шаг 8d) — в сцене от него в худшем случае останется Missing Script на этом
            // же объекте, и он сносится вместе с GameObject.
            var byName = GameObject.Find("DebugOverlay");
            if (byName != null)
            {
                Undo.DestroyObjectImmediate(byName);
                Debug.Log("[Editor] Удалён старый DebugOverlay (заменён HighJump HUD).");
            }
        }

        private static GameObject BuildDrivingGroup(Transform hudRt, out TMPro.TMP_Text speedLabel)
        {
            var groupRt = UIBuildHelpers.FindOrCreateChild(hudRt, "DrivingGroup");
            UIBuildHelpers.Anchor(groupRt, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f), new Vector2(0f, 80f), new Vector2(600f, 180f));

            speedLabel = UIBuildHelpers.CreateLabel(groupRt, "SpeedLabel", "0 KM/H",
                UIBuildHelpers.DisplayFontPath, 84f, Color.white);
            UIBuildHelpers.Anchor((RectTransform)speedLabel.transform, new Vector2(0f, 0.4f), new Vector2(1f, 1f),
                new Vector2(0.5f, 1f), Vector2.zero, Vector2.zero);

            var hintLabel = UIBuildHelpers.CreateLabel(groupRt, "HintLabel", "HOLD W",
                UIBuildHelpers.BodyFontPath, 28f, new Color(1f, 1f, 1f, 0.7f));
            UIBuildHelpers.Anchor((RectTransform)hintLabel.transform, new Vector2(0f, 0f), new Vector2(1f, 0.4f),
                new Vector2(0.5f, 0f), Vector2.zero, Vector2.zero);

            return groupRt.gameObject;
        }

        private static GameObject BuildFlightGroup(Transform hudRt, out TMPro.TMP_Text heightLabel,
            out TMPro.TMP_Text bestLabel, out TMPro.TMP_Text boostLabel)
        {
            var groupRt = UIBuildHelpers.FindOrCreateChild(hudRt, "FlightGroup");
            UIBuildHelpers.Anchor(groupRt, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f), new Vector2(-80f, 0f), new Vector2(360f, 320f));

            var layout = groupRt.GetComponent<VerticalLayoutGroup>();
            if (layout == null) layout = Undo.AddComponent<VerticalLayoutGroup>(groupRt.gameObject);
            layout.spacing = 16f;
            layout.childAlignment = TextAnchor.MiddleRight;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            heightLabel = UIBuildHelpers.CreateLabel(groupRt, "HeightLabel", "0.0 M",
                UIBuildHelpers.DisplayFontPath, 64f, Color.white, TMPro.TextAlignmentOptions.MidlineRight);
            EnsurePreferredHeight(heightLabel.gameObject, 90f);

            bestLabel = UIBuildHelpers.CreateLabel(groupRt, "BestLabel", "BEST 0.0 M",
                UIBuildHelpers.BodyFontPath, 32f, new Color(1f, 1f, 1f, 0.75f), TMPro.TextAlignmentOptions.MidlineRight);
            EnsurePreferredHeight(bestLabel.gameObject, 48f);

            boostLabel = UIBuildHelpers.CreateLabel(groupRt, "BoostLabel", "BOOST x0",
                UIBuildHelpers.DisplayFontPath, 40f, YellowColor, TMPro.TextAlignmentOptions.MidlineRight);
            EnsurePreferredHeight(boostLabel.gameObject, 60f);

            return groupRt.gameObject;
        }

        private static GameObject BuildScorePanel(Transform hudRt, out TMPro.TMP_Text scoreLabel,
            out TMPro.TMP_Text breakdownLabel, out GameObject newRecordLabel, out Button continueButton)
        {
            var panelRt = UIBuildHelpers.FindOrCreateChild(hudRt, "ScorePanel");
            UIBuildHelpers.Stretch(panelRt);

            var dim = panelRt.GetComponent<Image>();
            if (dim == null) dim = Undo.AddComponent<Image>(panelRt.gameObject);
            dim.sprite = null;
            dim.color = new Color(0f, 0f, 0f, 0.75f);

            var contentRt = UIBuildHelpers.FindOrCreateChild(panelRt, "Content");
            UIBuildHelpers.Anchor(contentRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 560f));

            var vLayout = contentRt.GetComponent<VerticalLayoutGroup>();
            if (vLayout == null) vLayout = Undo.AddComponent<VerticalLayoutGroup>(contentRt.gameObject);
            vLayout.spacing = 32f;
            vLayout.childAlignment = TextAnchor.MiddleCenter;
            vLayout.childControlWidth = true;
            vLayout.childControlHeight = true;
            vLayout.childForceExpandWidth = true;
            vLayout.childForceExpandHeight = false;

            var newRecordTmp = UIBuildHelpers.CreateLabel(contentRt, "NewRecordLabel", "NEW RECORD!",
                UIBuildHelpers.BodyFontPath, 40f, YellowColor);
            EnsurePreferredHeight(newRecordTmp.gameObject, 60f);
            newRecordLabel = newRecordTmp.gameObject;

            // Результат трюка = только ВЫСОТА (композитный счёт удалён решением геймдизайна).
            var scoreCaption = UIBuildHelpers.CreateLabel(contentRt, "ScoreCaption", "HEIGHT",
                UIBuildHelpers.BodyFontPath, 32f, Color.white);
            EnsurePreferredHeight(scoreCaption.gameObject, 44f);

            scoreLabel = UIBuildHelpers.CreateLabel(contentRt, "ScoreLabel", "0.0 M",
                UIBuildHelpers.DisplayFontPath, 120f, Color.white);
            EnsurePreferredHeight(scoreLabel.gameObject, 170f);

            // Личный рекорд под результатом — рантайм перезапишет.
            breakdownLabel = UIBuildHelpers.CreateLabel(contentRt, "BreakdownLabel", "BEST: 0.0 M",
                UIBuildHelpers.BodyFontPath, 34f, YellowColor);
            EnsurePreferredHeight(breakdownLabel.gameObject, 50f);

            continueButton = UIBuildHelpers.CreateButton(contentRt, "ContinueButton", ContinueButtonPath,
                "CONTINUE", UIBuildHelpers.DisplayFontPath, 36f, UIBuildHelpers.DarkTextColor);
            EnsurePreferredSize(continueButton.gameObject, 420f, 110f);

            return panelRt.gameObject;
        }

        private static void EnsurePreferredHeight(GameObject go, float height)
        {
            var le = go.GetComponent<LayoutElement>();
            if (le == null) le = Undo.AddComponent<LayoutElement>(go);
            le.preferredHeight = height;
        }

        private static void EnsurePreferredSize(GameObject go, float width, float height)
        {
            var le = go.GetComponent<LayoutElement>();
            if (le == null) le = Undo.AddComponent<LayoutElement>(go);
            le.preferredWidth = width;
            le.preferredHeight = height;
        }

        private static void SetRef(SerializedObject so, string propName, Object value)
        {
            var prop = so.FindProperty(propName);
            if (prop != null)
                prop.objectReferenceValue = value;
            else
                Debug.LogWarning("[Editor] Поле HighJumpHudView." + propName + " не найдено (рантайм ещё не скомпилирован?).");
        }
    }
}
#endif
