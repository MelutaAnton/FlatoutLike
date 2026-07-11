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
    /// DerbyHudView вешается на корень "DerbyHud" (компонент тикает всегда, даже когда HUD
    /// спрятан), а его SerializeField _root прошивается на дочерний "GameplayHud" —
    /// НЕ на сам корень. Причина: SetVisible(false) гасит _root, а следом рантайм вызывает
    /// ShowWreckedPanel(); если бы _root был самим DerbyHud, WreckedPanel (его дочерний
    /// объект) гас бы вместе с родителем и не мог бы показаться. Поэтому геймплейные
    /// виджеты (HpBar/NitroBar/AliveCounter/KillFeedContainer) лежат в GameplayHud,
    /// а WreckedPanel — прямой ребёнок DerbyHud, сиблинг GameplayHud, поверх него по
    /// порядку сиблингов (не гасится вместе с _root).
    ///
    /// Прошивка полей делается здесь же (SerializedObject), а ссылка на сам View
    /// в GameLifetimeScope._derbyHudView — через BindMetaUIRefsEditor.BindDerbyHudView
    /// (единая точка прошивки, см. 8c).
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
        private const string CardBgPath      = UIBuildHelpers.UiPackRoot + "/Buttons/png/button2.png";
        private const string WatchButtonPath = UIBuildHelpers.UiPackRoot + "/Buttons/png/button1.png";
        private const string LeaveButtonPath = UIBuildHelpers.UiPackRoot + "/Buttons/png/back button.png";

        private const int KillFeedItemCount = 4;

        /// <summary>Имена геймплейных виджетов, которые до фикса лежали прямыми детьми
        /// DerbyHud — миграция переносит их под GameplayHud (см. MigrateLegacyChildren).</summary>
        private static readonly string[] GameplayChildNames =
        {
            "HpBar", "NitroBar", "AliveCounter", "KillFeedContainer"
        };

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

            // ── GameplayHud (то, что гасится SetVisible(false) при смерти игрока) ──
            var gameplayRt = UIBuildHelpers.FindOrCreateChild(hudRt, "GameplayHud");
            UIBuildHelpers.Stretch(gameplayRt);
            gameplayRt.SetAsFirstSibling();

            // Миграция старых сцен: HpBar/NitroBar/AliveCounter/KillFeedContainer могли
            // остаться прямыми детьми DerbyHud с версии до фикса WreckedPanel.
            MigrateLegacyChildren(hudRt, gameplayRt);

            // ── HP-бар (снизу слева) ────────────────────────────────────────────
            var hpBarRt = UIBuildHelpers.FindOrCreateChild(gameplayRt, "HpBar");
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
            var nitroBarRt = UIBuildHelpers.FindOrCreateChild(gameplayRt, "NitroBar");
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
            var aliveGroupRt = UIBuildHelpers.FindOrCreateChild(gameplayRt, "AliveCounter");
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

            // ── Kill-feed (сверху слева, под HP/nitro-барами не заходит — те снизу) ─
            var killFeedItems = BuildKillFeed(gameplayRt);

            // ── WreckedPanel (центр, по умолчанию неактивен) ─────────────────────
            // Прямой ребёнок DerbyHud (НЕ GameplayHud) — не должен гаснуть вместе
            // с _root. Ставим последним сиблингом, чтобы рисовался поверх GameplayHud.
            var wreckedRefs = BuildWreckedPanel(hudRt);
            ((RectTransform)wreckedRefs.Panel.transform).SetAsLastSibling();

            // ── DerbyHudView на корне DerbyHud ───────────────────────────────────
            var view = hudRt.GetComponent<DerbyHudView>();
            if (view == null) view = Undo.AddComponent<DerbyHudView>(hudRt.gameObject);

            var so = new SerializedObject(view);
            SetRef(so, "_hpBar", hpFill);
            SetRef(so, "_nitroBar", nitroFill);
            SetRef(so, "_aliveLabel", aliveLabel);
            // _root — GameplayHud, а не корень DerbyHud (см. комментарий класса):
            // WreckedPanel должен оставаться видимым, когда SetVisible(false) гасит _root.
            SetRef(so, "_root", gameplayRt.gameObject);
            SetRef(so, "_killFeedContainer", killFeedItems.Container);
            SetObjectArray(so, "_killFeedItems", killFeedItems.Items);
            SetRef(so, "_wreckedPanel", wreckedRefs.Panel);
            SetRef(so, "_wreckedWatchButton", wreckedRefs.WatchButton);
            SetRef(so, "_wreckedLeaveButton", wreckedRefs.LeaveButton);
            so.ApplyModifiedProperties();

            BindMetaUIRefsEditor.BindDerbyHudView(view);

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Undo.CollapseUndoOperations(undoGroup);
            Debug.Log("[Editor] Derby HUD собран на Derby_Arena_01.");
        }

        private readonly struct KillFeedRefs
        {
            public readonly RectTransform Container;
            public readonly KillFeedItemView[] Items;

            public KillFeedRefs(RectTransform container, KillFeedItemView[] items)
            {
                Container = container;
                Items = items;
            }
        }

        private readonly struct WreckedPanelRefs
        {
            public readonly GameObject Panel;
            public readonly Button WatchButton;
            public readonly Button LeaveButton;

            public WreckedPanelRefs(GameObject panel, Button watchButton, Button leaveButton)
            {
                Panel = panel;
                WatchButton = watchButton;
                LeaveButton = leaveButton;
            }
        }

        /// <summary>
        /// Верх-лево, под барами (те снизу слева — HpBar/NitroBar, см. выше) не заходит.
        /// 4 идентичных слота, все SetActive(false) по умолчанию — рантайм включает по мере
        /// поступления фрагов и сдвигает/затирает текст.
        /// </summary>
        private static KillFeedRefs BuildKillFeed(Transform hudRt)
        {
            var containerRt = UIBuildHelpers.FindOrCreateChild(hudRt, "KillFeedContainer");
            UIBuildHelpers.Anchor(containerRt, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(60f, -60f), new Vector2(520f, 200f));

            var layout = containerRt.GetComponent<VerticalLayoutGroup>();
            if (layout == null) layout = Undo.AddComponent<VerticalLayoutGroup>(containerRt.gameObject);
            layout.spacing = 6f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var items = new KillFeedItemView[KillFeedItemCount];
            for (int i = 0; i < KillFeedItemCount; i++)
                items[i] = BuildKillFeedItem(containerRt, i);

            return new KillFeedRefs(containerRt, items);
        }

        private static KillFeedItemView BuildKillFeedItem(Transform parent, int index)
        {
            var itemRt = UIBuildHelpers.FindOrCreateChild(parent, "KillFeedItem_" + index);
            var le = itemRt.GetComponent<LayoutElement>();
            if (le == null) le = Undo.AddComponent<LayoutElement>(itemRt.gameObject);
            le.preferredHeight = 44f;

            // Полупрозрачная тёмная подложка — белый текст поверх геймплея без неё нечитаем.
            var bg = itemRt.GetComponent<Image>();
            if (bg == null) bg = Undo.AddComponent<Image>(itemRt.gameObject);
            bg.sprite = null;
            bg.color = new Color(0f, 0f, 0f, 0.55f);

            var label = UIBuildHelpers.CreateLabel(itemRt, "Label", "Player wrecked Enemy",
                UIBuildHelpers.BodyFontPath, 28f, Color.white, TMPro.TextAlignmentOptions.MidlineLeft);
            var labelRt = (RectTransform)label.transform;
            UIBuildHelpers.Anchor(labelRt, Vector2.zero, Vector2.one,
                new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
            labelRt.offsetMin = new Vector2(16f, 0f);
            labelRt.offsetMax = new Vector2(-16f, 0f);

            var view = itemRt.GetComponent<KillFeedItemView>();
            if (view == null) view = Undo.AddComponent<KillFeedItemView>(itemRt.gameObject);

            var itemSo = new SerializedObject(view);
            var labelProp = itemSo.FindProperty("_label");
            if (labelProp != null)
                labelProp.objectReferenceValue = label;
            else
                Debug.LogWarning("[Editor] Поле KillFeedItemView._label не найдено (рантайм ещё не скомпилирован?).");
            itemSo.ApplyModifiedProperties();

            // Слот скрыт, пока рантайм не покажет фраг.
            itemRt.gameObject.SetActive(false);

            return view;
        }

        /// <summary>Центральная панель "WRECKED" (стиль MatchEnd) — WATCH продолжить наблюдать
        /// за матчем, LEAVE выйти. По умолчанию неактивна.</summary>
        private static WreckedPanelRefs BuildWreckedPanel(Transform hudRt)
        {
            var panelRt = UIBuildHelpers.FindOrCreateChild(hudRt, "WreckedPanel");
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

            var titleLabel = UIBuildHelpers.CreateLabel(contentRt, "TitleLabel", "WRECKED",
                UIBuildHelpers.DisplayFontPath, 72f, UIBuildHelpers.DarkTextColor);
            var titleLe = titleLabel.GetComponent<LayoutElement>();
            if (titleLe == null) titleLe = Undo.AddComponent<LayoutElement>(titleLabel.gameObject);
            titleLe.preferredHeight = 110f;

            var watchBtn = UIBuildHelpers.CreateButton(contentRt, "WatchButton", WatchButtonPath,
                "WATCH", UIBuildHelpers.DisplayFontPath, 34f, UIBuildHelpers.DarkTextColor);
            var watchLe = watchBtn.GetComponent<LayoutElement>();
            if (watchLe == null) watchLe = Undo.AddComponent<LayoutElement>(watchBtn.gameObject);
            watchLe.preferredWidth = 420f;
            watchLe.preferredHeight = 100f;

            var leaveBtn = UIBuildHelpers.CreateButton(contentRt, "LeaveButton", LeaveButtonPath,
                "LEAVE", UIBuildHelpers.DisplayFontPath, 34f, UIBuildHelpers.DarkTextColor);
            var leaveLe = leaveBtn.GetComponent<LayoutElement>();
            if (leaveLe == null) leaveLe = Undo.AddComponent<LayoutElement>(leaveBtn.gameObject);
            leaveLe.preferredWidth = 420f;
            leaveLe.preferredHeight = 100f;

            // Неактивна по умолчанию — рантайм включает при разрушении машины игрока.
            panelRt.gameObject.SetActive(false);

            return new WreckedPanelRefs(panelRt.gameObject, watchBtn, leaveBtn);
        }

        /// <summary>
        /// Миграция со старой иерархии HUD'а (до фикса WreckedPanel): в уже собранных
        /// сценах HpBar/NitroBar/AliveCounter/KillFeedContainer — прямые дети DerbyHud.
        /// Перепарентовываем их под GameplayHud, если найдены на старом месте.
        /// GameplayHud растянут на весь экран так же, как DerbyHud (Stretch), поэтому
        /// SetParent(parent, false) сохраняет anchoredPosition/anchors без визуального
        /// сдвига. WreckedPanel НЕ трогаем — он и должен остаться прямым ребёнком DerbyHud.
        /// Идемпотентно: на повторных прогонах объекты уже лежат под GameplayHud и
        /// hudRt.Find(name) больше не находит их на старом месте — миграция молчит.
        /// </summary>
        private static void MigrateLegacyChildren(Transform hudRt, Transform gameplayRt)
        {
            foreach (var childName in GameplayChildNames)
            {
                var legacy = hudRt.Find(childName);
                if (legacy == null) continue;

                Undo.SetTransformParent(legacy, gameplayRt, "Migrate " + childName + " under GameplayHud");
                if (legacy is RectTransform legacyRt)
                    legacyRt.SetParent(gameplayRt, false);
                Debug.Log("[Editor] Мигрирован " + childName + " из DerbyHud в GameplayHud (фикс WreckedPanel).");
            }
        }

        private static void SetObjectArray(SerializedObject so, string propName, Object[] values)
        {
            var prop = so.FindProperty(propName);
            if (prop == null)
            {
                Debug.LogWarning("[Editor] Поле DerbyHudView." + propName + " не найдено (рантайм ещё не скомпилирован?).");
                return;
            }

            prop.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
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
