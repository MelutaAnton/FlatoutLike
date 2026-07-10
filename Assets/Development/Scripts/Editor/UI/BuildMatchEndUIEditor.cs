#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using FlatOutLike.Editor.Setup;
using FlatOutLike.Views.UI.Overlay;

namespace FlatOutLike.Editor.UI
{
    /// <summary>
    /// Шаг 8c: наполняет сцену Derby_Arena_01 экраном итогов матча — панель (изначально
    /// НЕАКТИВНАЯ, включается рантаймом при завершении матча), огромный _resultLabel,
    /// _rewardLabel и кнопку CONTINUE. В отличие от MainMenu/Garage/Stunts_Hub, у арены
    /// нет готового UICanvas от BuildMetaScenesEditor — Canvas+EventSystem создаются здесь
    /// же (UIBuildHelpers.EnsureCanvasAndEventSystem), поверх остального содержимого сцены.
    ///
    /// Вешает MatchEndView на Canvas (не на саму панель — панель стартует неактивной,
    /// а поле _panel на MatchEndView ссылается на неё для включения/выключения рантаймом),
    /// прошивает GameLifetimeScope (_matchEndView + _stuntsCatalogConfig) через
    /// BindMetaUIRefsEditor.
    ///
    /// Запускать на ОТКРЫТОЙ сцене Derby_Arena_01, ПОСЛЕ:
    ///   FlatOutLike/Tools/Generate TMP Font Assets
    /// </summary>
    public static class BuildMatchEndUIEditor
    {
        private const string ContinueButtonPath = UIBuildHelpers.UiPackRoot + "/Buttons/png/button1.png";

        [MenuItem("FlatOutLike/UI/Build MatchEnd UI")]
        public static void BuildMatchEndUI()
        {
            Undo.SetCurrentGroupName("Build MatchEnd UI (Step 8c)");
            int undoGroup = Undo.GetCurrentGroup();

            var canvas = UIBuildHelpers.EnsureCanvasAndEventSystem();
            var root = canvas.transform;

            var panelRt = UIBuildHelpers.FindOrCreateChild(root, "MatchEndPanel");
            UIBuildHelpers.Stretch(panelRt);

            var dim = panelRt.GetComponent<Image>();
            if (dim == null) dim = Undo.AddComponent<Image>(panelRt.gameObject);
            // Затемнение задаётся безусловно (не только у свежего Image) — повторный прогон
            // обязан гарантировать чёрную подложку, а не оставить экран без затемнения.
            dim.sprite = null;
            dim.color = new Color(0f, 0f, 0f, 0.75f);

            var contentRt = UIBuildHelpers.FindOrCreateChild(panelRt, "Content");
            UIBuildHelpers.Anchor(contentRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 500f));

            var vLayout = contentRt.GetComponent<VerticalLayoutGroup>();
            if (vLayout == null) vLayout = Undo.AddComponent<VerticalLayoutGroup>(contentRt.gameObject);
            vLayout.spacing = 36f;
            vLayout.childAlignment = TextAnchor.MiddleCenter;
            vLayout.childControlWidth = true;
            vLayout.childControlHeight = true;
            vLayout.childForceExpandWidth = true;
            vLayout.childForceExpandHeight = false;

            var resultLabel = UIBuildHelpers.CreateLabel(contentRt, "ResultLabel", "VICTORY",
                UIBuildHelpers.DisplayFontPath, 96f, Color.white);
            var resultLe = resultLabel.GetComponent<LayoutElement>();
            if (resultLe == null) resultLe = Undo.AddComponent<LayoutElement>(resultLabel.gameObject);
            resultLe.preferredHeight = 140f;

            var rewardLabel = UIBuildHelpers.CreateLabel(contentRt, "RewardLabel", "+0",
                UIBuildHelpers.BodyFontPath, 48f, Color.yellow);
            var rewardLe = rewardLabel.GetComponent<LayoutElement>();
            if (rewardLe == null) rewardLe = Undo.AddComponent<LayoutElement>(rewardLabel.gameObject);
            rewardLe.preferredHeight = 70f;

            var continueBtn = UIBuildHelpers.CreateButton(contentRt, "ContinueButton", ContinueButtonPath,
                "CONTINUE", UIBuildHelpers.DisplayFontPath, 36f, UIBuildHelpers.DarkTextColor);
            var continueLe = continueBtn.GetComponent<LayoutElement>();
            if (continueLe == null) continueLe = Undo.AddComponent<LayoutElement>(continueBtn.gameObject);
            continueLe.preferredWidth = 420f;
            continueLe.preferredHeight = 110f;

            // Панель стартует выключенной — рантайм включает её при завершении матча.
            panelRt.gameObject.SetActive(false);

            var view = canvas.GetComponent<MatchEndView>();
            if (view == null) view = Undo.AddComponent<MatchEndView>(canvas.gameObject);

            var so = new SerializedObject(view);
            SetRef(so, "_panel", panelRt.gameObject);
            SetRef(so, "_resultLabel", resultLabel);
            SetRef(so, "_rewardLabel", rewardLabel);
            SetRef(so, "_continueButton", continueBtn);
            so.ApplyModifiedProperties();

            BindMetaUIRefsEditor.BindMatchEndView(view);
            BindMetaUIRefsEditor.BindStuntsCatalogConfig();

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Undo.CollapseUndoOperations(undoGroup);
            Debug.Log("[Editor] MatchEnd UI собран на Derby_Arena_01.");
        }

        private static void SetRef(SerializedObject so, string propName, Object value)
        {
            var prop = so.FindProperty(propName);
            if (prop != null)
                prop.objectReferenceValue = value;
            else
                Debug.LogWarning("[Editor] Поле MatchEndView." + propName + " не найдено (рантайм ещё не скомпилирован?).");
        }
    }
}
#endif
