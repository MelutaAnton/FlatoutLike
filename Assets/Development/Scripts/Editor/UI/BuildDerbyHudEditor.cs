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
    /// Шаг 8d: наполняет сцену Derby_Arena_01 боевым HUD'ом (HP + нитро-бар слева внизу,
    /// счётчик живых машин справа вверху). Canvas переиспользуется через
    /// UIBuildHelpers.EnsureCanvasAndEventSystem (тот же Canvas, что и MatchEndPanel из
    /// Шага 8c) — HUD помещается ПЕРЕД MatchEndPanel по sibling-порядку, чтобы оверлей
    /// итогов матча всегда рисовался поверх HUD.
    ///
    /// DerbyHudView вешается на корень "DerbyHud", прошивка полей делается здесь же
    /// (SerializedObject), а ссылка на сам View в GameLifetimeScope._derbyHudView —
    /// через BindMetaUIRefsEditor.BindDerbyHudView (единая точка прошивки, см. 8c).
    ///
    /// Запускать на ОТКРЫТОЙ сцене Derby_Arena_01, ПОСЛЕ:
    ///   FlatOutLike/Tools/Generate TMP Font Assets
    ///   FlatOutLike/UI/Build MatchEnd UI (создаёт Canvas/MatchEndPanel, если ещё нет)
    /// </summary>
    public static class BuildDerbyHudEditor
    {
        private const string HpBarBgPath     = UIBuildHelpers.UiPackRoot + "/Speedometer/png/nos bar 1.png";
        private const string HpBarFillPath   = UIBuildHelpers.UiPackRoot + "/Speedometer/png/nos bar fill.png";
        private const string NitroBarBgPath  = UIBuildHelpers.UiPackRoot + "/Speedometer/png/nos bar 1.png";
        private const string NitroBarFillPath = UIBuildHelpers.UiPackRoot + "/Speedometer/png/nos bar fill.png";
        private const string CarIconPath     = UIBuildHelpers.UiPackRoot + "/Icons/png/car.png";

        [MenuItem("FlatOutLike/UI/Build Derby HUD")]
        public static void BuildDerbyHud()
        {
            Undo.SetCurrentGroupName("Build Derby HUD (Step 8d)");
            int undoGroup = Undo.GetCurrentGroup();

            var canvas = UIBuildHelpers.EnsureCanvasAndEventSystem();
            var root = canvas.transform;

            var hudRt = UIBuildHelpers.FindOrCreateChild(root, "DerbyHud");
            UIBuildHelpers.Stretch(hudRt);
            // HUD не должен перекрывать MatchEndPanel — ставим его самым первым сиблингом
            // (MatchEndPanel создаётся BuildMatchEndUIEditor'ом позже и рисуется поверх).
            hudRt.SetAsFirstSibling();

            // ── HP-бар (снизу слева) ────────────────────────────────────────────
            var hpBarRt = UIBuildHelpers.FindOrCreateChild(hudRt, "HpBar");
            UIBuildHelpers.Anchor(hpBarRt, new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(0f, 0f), new Vector2(60f, 60f), new Vector2(320f, 40f));

            var hpBg = hpBarRt.GetComponent<Image>();
            if (hpBg == null) hpBg = Undo.AddComponent<Image>(hpBarRt.gameObject);
            hpBg.sprite = UIBuildHelpers.LoadSprite(HpBarBgPath);
            hpBg.type = Image.Type.Simple;
            // Спрайт подложки в паке БЕЛЫЙ — без затемнения пустая часть бара неотличима
            // от заполненной (обе белые). Тёмный tint = «пустая» часть читается сразу.
            hpBg.color = new Color(0.1f, 0.1f, 0.1f, 0.85f);

            var hpFillRt = UIBuildHelpers.FindOrCreateChild(hpBarRt, "Fill");
            UIBuildHelpers.Stretch(hpFillRt);
            var hpFill = hpFillRt.GetComponent<Image>();
            if (hpFill == null) hpFill = Undo.AddComponent<Image>(hpFillRt.gameObject);
            hpFill.sprite = UIBuildHelpers.LoadSprite(HpBarFillPath);
            hpFill.type = Image.Type.Filled;
            hpFill.fillMethod = Image.FillMethod.Horizontal;
            hpFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            // Значение красит presenter рантаймом (зелёный→жёлтый→красный по HP) —
            // здесь только базовый зелёный цвет и fillAmount = 1 (полное здоровье).
            hpFill.color = new Color(0.25f, 0.85f, 0.25f, 1f);
            hpFill.fillAmount = 1f;

            // ── Нитро-бар (рядом с HP, справа от него) ──────────────────────────
            var nitroBarRt = UIBuildHelpers.FindOrCreateChild(hudRt, "NitroBar");
            UIBuildHelpers.Anchor(nitroBarRt, new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(0f, 0f), new Vector2(60f, 110f), new Vector2(320f, 40f));

            var nitroBg = nitroBarRt.GetComponent<Image>();
            if (nitroBg == null) nitroBg = Undo.AddComponent<Image>(nitroBarRt.gameObject);
            nitroBg.sprite = UIBuildHelpers.LoadSprite(NitroBarBgPath);
            nitroBg.type = Image.Type.Simple;
            nitroBg.color = new Color(0.1f, 0.1f, 0.1f, 0.85f); // см. комментарий у HP-бара

            var nitroFillRt = UIBuildHelpers.FindOrCreateChild(nitroBarRt, "Fill");
            UIBuildHelpers.Stretch(nitroFillRt);
            var nitroFill = nitroFillRt.GetComponent<Image>();
            if (nitroFill == null) nitroFill = Undo.AddComponent<Image>(nitroFillRt.gameObject);
            nitroFill.sprite = UIBuildHelpers.LoadSprite(NitroBarFillPath);
            nitroFill.type = Image.Type.Filled;
            nitroFill.fillMethod = Image.FillMethod.Horizontal;
            nitroFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            nitroFill.color = new Color(0.3f, 0.6f, 1f, 1f);
            nitroFill.fillAmount = 1f;

            // ── Счётчик живых машин (сверху справа) ─────────────────────────────
            var aliveGroupRt = UIBuildHelpers.FindOrCreateChild(hudRt, "AliveCounter");
            UIBuildHelpers.Anchor(aliveGroupRt, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(1f, 1f), new Vector2(-60f, -60f), new Vector2(180f, 60f));

            var aliveIconRt = UIBuildHelpers.FindOrCreateChild(aliveGroupRt, "Icon");
            UIBuildHelpers.Anchor(aliveIconRt, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f), Vector2.zero, new Vector2(48f, 48f));
            var aliveIcon = aliveIconRt.GetComponent<Image>();
            if (aliveIcon == null) aliveIcon = Undo.AddComponent<Image>(aliveIconRt.gameObject);
            aliveIcon.sprite = UIBuildHelpers.LoadSprite(CarIconPath);
            aliveIcon.type = Image.Type.Simple;
            aliveIcon.preserveAspect = true;

            var aliveLabel = UIBuildHelpers.CreateLabel(aliveGroupRt, "AliveLabel", "0",
                UIBuildHelpers.BodyFontPath, 40f, Color.white, TMPro.TextAlignmentOptions.MidlineLeft);
            var aliveLabelRt = (RectTransform)aliveLabel.transform;
            UIBuildHelpers.Anchor(aliveLabelRt, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(0f, 0.5f), new Vector2(56f, 0f), new Vector2(120f, 60f));

            // ── DerbyHudView на корне DerbyHud ───────────────────────────────────
            var view = hudRt.GetComponent<DerbyHudView>();
            if (view == null) view = Undo.AddComponent<DerbyHudView>(hudRt.gameObject);

            var so = new SerializedObject(view);
            SetRef(so, "_hpBar", hpFill);
            SetRef(so, "_nitroBar", nitroFill);
            SetRef(so, "_aliveLabel", aliveLabel);
            SetRef(so, "_root", hudRt.gameObject);
            so.ApplyModifiedProperties();

            BindMetaUIRefsEditor.BindDerbyHudView(view);

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Undo.CollapseUndoOperations(undoGroup);
            Debug.Log("[Editor] Derby HUD собран на Derby_Arena_01.");
        }

        private static void SetRef(SerializedObject so, string propName, Object value)
        {
            var prop = so.FindProperty(propName);
            if (prop != null)
                prop.objectReferenceValue = value;
            else
                Debug.LogWarning("[Editor] Поле DerbyHudView." + propName + " не найдено (рантайм ещё не скомпилирован?).");
        }
    }
}
#endif
