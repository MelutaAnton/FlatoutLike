#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using FlatOutLike.Editor.Setup;
using FlatOutLike.Views.UI.Garage;

namespace FlatOutLike.Editor.UI
{
    /// <summary>
    /// Шаг 8c: наполняет UICanvas сцены Garage — фон, панель монет, ряд из трёх
    /// CarCardView (карточка машины: иконка/тайтл/цена/lock/selected-маркер/кнопка-целиком),
    /// колонку из трёх UpgradeRowView (Armor/Power/Nitro: иконка/уровень/цена/кнопка BUY)
    /// и кнопку Back. Вешает GarageView на UICanvas, прошивает массивы карточек/рядов и
    /// прошивает GameLifetimeScope (_garageView + _stuntsCatalogConfig) через BindMetaUIRefsEditor.
    ///
    /// Запускать на ОТКРЫТОЙ сцене Garage, ПОСЛЕ:
    ///   FlatOutLike/Scenes/Build Meta Scenes (Step 8b)
    ///   FlatOutLike/Tools/Generate TMP Font Assets
    /// </summary>
    public static class BuildGarageUIEditor
    {
        private const string BackgroundPath = UIBuildHelpers.UiPackRoot + "/Textures/garage.jpg";
        private const string CoinIconPath   = UIBuildHelpers.UiPackRoot + "/Icons/png/dollar.png";
        private const string LockIconPath   = UIBuildHelpers.UiPackRoot + "/Icons/png/locked.png";
        private const string CardBgPath     = UIBuildHelpers.UiPackRoot + "/Buttons/png/button2.png";
        private const string BuyButtonPath  = UIBuildHelpers.UiPackRoot + "/Buttons/png/button10.png";
        private const string BackButtonPath = UIBuildHelpers.UiPackRoot + "/Buttons/png/back button.png";

        private const string ArmorIconPath = UIBuildHelpers.UiPackRoot + "/Icons/png/repair.png";
        private const string PowerIconPath = UIBuildHelpers.UiPackRoot + "/Icons/png/motor.png";
        private const string NitroIconPath = UIBuildHelpers.UiPackRoot + "/Icons/png/nos icon.png";

        private const int CardCount = 3;

        private static readonly (string Label, string IconPath)[] UpgradeRows =
        {
            ("ARMOR", ArmorIconPath),
            ("POWER", PowerIconPath),
            ("NITRO", NitroIconPath),
        };

        [MenuItem("FlatOutLike/UI/Build Garage UI")]
        public static void BuildGarageUI()
        {
            Undo.SetCurrentGroupName("Build Garage UI (Step 8c)");
            int undoGroup = Undo.GetCurrentGroup();

            var canvas = Object.FindAnyObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogWarning("[Editor] UICanvas не найден в активной сцене. Сначала запусти " +
                                  "'FlatOutLike/Scenes/Build Meta Scenes (Step 8b)'.");
                Undo.CollapseUndoOperations(undoGroup);
                return;
            }

            var root = canvas.transform;

            UIBuildHelpers.CreateFullscreenBackground(root, BackgroundPath);

            // ── Панель монет (верх-право) ────────────────────────────────────────
            var coinsPanel = UIBuildHelpers.FindOrCreateChild(root, "CoinsPanel");
            UIBuildHelpers.Anchor(coinsPanel, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(1f, 1f), new Vector2(-40f, -40f), new Vector2(280f, 80f));

            var coinIconRt = UIBuildHelpers.FindOrCreateChild(coinsPanel, "CoinIcon");
            UIBuildHelpers.Anchor(coinIconRt, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(64f, 64f));
            var coinIconImg = coinIconRt.GetComponent<Image>();
            if (coinIconImg == null) coinIconImg = Undo.AddComponent<Image>(coinIconRt.gameObject);
            coinIconImg.sprite = UIBuildHelpers.LoadSprite(CoinIconPath);
            coinIconImg.preserveAspect = true;

            var coinsLabel = UIBuildHelpers.CreateLabel(coinsPanel, "CoinsLabel", "0",
                UIBuildHelpers.BodyFontPath, 42f, Color.white, TMPro.TextAlignmentOptions.MidlineLeft);
            UIBuildHelpers.Anchor((RectTransform)coinsLabel.transform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(0f, 0.5f), new Vector2(80f, 0f), new Vector2(-80f, 64f));

            // ── Ряд карточек машин ───────────────────────────────────────────────
            var cardsRow = UIBuildHelpers.FindOrCreateChild(root, "CarCardsRow");
            UIBuildHelpers.Anchor(cardsRow, new Vector2(0.5f, 0.6f), new Vector2(0.5f, 0.6f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1180f, 440f));

            var layout = cardsRow.GetComponent<HorizontalLayoutGroup>();
            if (layout == null) layout = Undo.AddComponent<HorizontalLayoutGroup>(cardsRow.gameObject);
            layout.spacing = 40f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var cardViews = new CarCardView[CardCount];
            for (int i = 0; i < CardCount; i++)
                cardViews[i] = BuildCarCard(cardsRow, i);

            // ── Колонка апгрейдов ────────────────────────────────────────────────
            var upgradesColumn = UIBuildHelpers.FindOrCreateChild(root, "UpgradesColumn");
            UIBuildHelpers.Anchor(upgradesColumn, new Vector2(0.5f, 0.16f), new Vector2(0.5f, 0.16f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000f, 280f));

            var vLayout = upgradesColumn.GetComponent<VerticalLayoutGroup>();
            if (vLayout == null) vLayout = Undo.AddComponent<VerticalLayoutGroup>(upgradesColumn.gameObject);
            vLayout.spacing = 12f;
            vLayout.childAlignment = TextAnchor.MiddleCenter;
            vLayout.childControlWidth = true;
            vLayout.childControlHeight = true;
            vLayout.childForceExpandWidth = true;
            vLayout.childForceExpandHeight = false;

            var rowViews = new UpgradeRowView[UpgradeRows.Length];
            for (int i = 0; i < UpgradeRows.Length; i++)
                rowViews[i] = BuildUpgradeRow(upgradesColumn, i, UpgradeRows[i].Label, UpgradeRows[i].IconPath);

            // ── Back ─────────────────────────────────────────────────────────────
            var backBtn = UIBuildHelpers.CreateButton(root, "BackButton", BackButtonPath, null,
                UIBuildHelpers.DisplayFontPath, 32f, Color.white);
            UIBuildHelpers.Anchor((RectTransform)backBtn.transform, new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(0f, 0f), new Vector2(40f, 40f), new Vector2(160f, 80f));

            // ── GarageView на UICanvas ───────────────────────────────────────────
            var view = canvas.GetComponent<GarageView>();
            if (view == null) view = Undo.AddComponent<GarageView>(canvas.gameObject);

            var so = new SerializedObject(view);
            SetRef(so, "_coinsLabel", coinsLabel);
            SetObjectArray(so, "_carCards", cardViews);
            SetObjectArray(so, "_upgradeRows", rowViews);
            SetRef(so, "_backButton", backBtn);
            so.ApplyModifiedProperties();

            BindMetaUIRefsEditor.BindGarageView(view);
            BindMetaUIRefsEditor.BindStuntsCatalogConfig();

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Undo.CollapseUndoOperations(undoGroup);
            Debug.Log("[Editor] Garage UI собран.");
        }

        private static CarCardView BuildCarCard(Transform parent, int index)
        {
            var cardRt = UIBuildHelpers.FindOrCreateChild(parent, "CarCard_" + index);
            var le = cardRt.GetComponent<LayoutElement>();
            if (le == null) le = Undo.AddComponent<LayoutElement>(cardRt.gameObject);
            le.preferredWidth = 340f;
            le.preferredHeight = 420f;

            var bgImg = cardRt.GetComponent<Image>();
            if (bgImg == null) bgImg = Undo.AddComponent<Image>(cardRt.gameObject);
            bgImg.sprite = UIBuildHelpers.LoadSprite(CardBgPath);
            bgImg.type = Image.Type.Sliced;

            var actionButton = cardRt.GetComponent<Button>();
            if (actionButton == null) actionButton = Undo.AddComponent<Button>(cardRt.gameObject);
            actionButton.targetGraphic = bgImg;

            var iconRt = UIBuildHelpers.FindOrCreateChild(cardRt, "Icon");
            UIBuildHelpers.Anchor(iconRt, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(260f, 200f));
            var iconImg = iconRt.GetComponent<Image>();
            if (iconImg == null) iconImg = Undo.AddComponent<Image>(iconRt.gameObject);
            iconImg.preserveAspect = true;

            var titleLabel = UIBuildHelpers.CreateLabel(cardRt, "Title", "Car " + (index + 1),
                UIBuildHelpers.DisplayFontPath, 30f, UIBuildHelpers.DarkTextColor); // карточка белая — белый тайтл невидим
            UIBuildHelpers.Anchor((RectTransform)titleLabel.transform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -240f), new Vector2(-20f, 48f));

            var priceLabel = UIBuildHelpers.CreateLabel(cardRt, "PriceLabel", "0",
                UIBuildHelpers.BodyFontPath, 26f, Color.yellow);
            UIBuildHelpers.Anchor((RectTransform)priceLabel.transform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -290f), new Vector2(-20f, 40f));

            var lockRt = UIBuildHelpers.FindOrCreateChild(cardRt, "LockIcon");
            UIBuildHelpers.Anchor(lockRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(96f, 96f));
            var lockImg = lockRt.GetComponent<Image>();
            if (lockImg == null) lockImg = Undo.AddComponent<Image>(lockRt.gameObject);
            lockImg.sprite = UIBuildHelpers.LoadSprite(LockIconPath);
            lockImg.raycastTarget = false;
            lockRt.gameObject.SetActive(false);

            // Маркер выбора — ПОЛУПРОЗРАЧНАЯ жёлтая подсветка + подпись SELECTED внизу.
            // Непрозрачный спрайт на весь прямоугольник (первая версия) накрывал контент
            // карточки целиком — иконка/тайтл/цена «пропадали» у выбранной машины.
            var selectedRt = UIBuildHelpers.FindOrCreateChild(cardRt, "SelectedMarker");
            UIBuildHelpers.Stretch(selectedRt);
            var selectedImg = selectedRt.GetComponent<Image>();
            if (selectedImg == null) selectedImg = Undo.AddComponent<Image>(selectedRt.gameObject);
            selectedImg.sprite = null;
            selectedImg.color = new Color(1f, 0.8f, 0.1f, 0.22f);
            selectedImg.raycastTarget = false;

            var selectedLabel = UIBuildHelpers.CreateLabel(selectedRt, "Label", "SELECTED",
                UIBuildHelpers.DisplayFontPath, 26f, UIBuildHelpers.DarkTextColor);
            UIBuildHelpers.Anchor((RectTransform)selectedLabel.transform, new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(-20f, 40f));

            selectedRt.SetAsLastSibling();
            selectedRt.gameObject.SetActive(false);

            var view = cardRt.GetComponent<CarCardView>();
            if (view == null) view = Undo.AddComponent<CarCardView>(cardRt.gameObject);

            var so = new SerializedObject(view);
            SetRef(so, "_icon", iconImg);
            SetRef(so, "_title", titleLabel);
            SetRef(so, "_priceLabel", priceLabel);
            SetRef(so, "_lockIcon", lockRt.gameObject);
            SetRef(so, "_selectedMarker", selectedRt.gameObject);
            SetRef(so, "_actionButton", actionButton);
            so.ApplyModifiedProperties();

            return view;
        }

        private static UpgradeRowView BuildUpgradeRow(Transform parent, int index, string label, string iconPath)
        {
            var rowRt = UIBuildHelpers.FindOrCreateChild(parent, "UpgradeRow_" + index);
            var le = rowRt.GetComponent<LayoutElement>();
            if (le == null) le = Undo.AddComponent<LayoutElement>(rowRt.gameObject);
            le.preferredHeight = 76f;

            var hLayout = rowRt.GetComponent<HorizontalLayoutGroup>();
            if (hLayout == null) hLayout = Undo.AddComponent<HorizontalLayoutGroup>(rowRt.gameObject);
            hLayout.spacing = 20f;
            hLayout.childAlignment = TextAnchor.MiddleLeft;
            hLayout.childControlWidth = true;
            hLayout.childControlHeight = true;
            hLayout.childForceExpandWidth = false;
            hLayout.childForceExpandHeight = true;
            hLayout.padding = new RectOffset(20, 20, 8, 8);

            var bgImg = rowRt.GetComponent<Image>();
            if (bgImg == null) bgImg = Undo.AddComponent<Image>(rowRt.gameObject);
            if (bgImg.sprite == null) bgImg.color = new Color(0f, 0f, 0f, 0.45f);

            var iconRt = UIBuildHelpers.FindOrCreateChild(rowRt, "Icon");
            var iconLe = iconRt.GetComponent<LayoutElement>();
            if (iconLe == null) iconLe = Undo.AddComponent<LayoutElement>(iconRt.gameObject);
            iconLe.preferredWidth = 56f;
            iconLe.preferredHeight = 56f;
            var iconImg = iconRt.GetComponent<Image>();
            if (iconImg == null) iconImg = Undo.AddComponent<Image>(iconRt.gameObject);
            iconImg.sprite = UIBuildHelpers.LoadSprite(iconPath);
            iconImg.preserveAspect = true;

            var titleLabel = CreateRowLabel(rowRt, "TitleLabel", label, 220f);
            var levelLabel = CreateRowLabel(rowRt, "LevelLabel", "Lv 0", 160f);
            var priceLabel = CreateRowLabel(rowRt, "PriceLabel", "0", 160f);
            priceLabel.color = Color.yellow;

            var buyBtn = UIBuildHelpers.CreateButton(rowRt, "BuyButton", BuyButtonPath, "BUY",
                UIBuildHelpers.DisplayFontPath, 26f, UIBuildHelpers.DarkTextColor);
            var buyLe = buyBtn.GetComponent<LayoutElement>();
            if (buyLe == null) buyLe = Undo.AddComponent<LayoutElement>(buyBtn.gameObject);
            buyLe.preferredWidth = 200f;
            buyLe.preferredHeight = 60f;

            var view = rowRt.GetComponent<UpgradeRowView>();
            if (view == null) view = Undo.AddComponent<UpgradeRowView>(rowRt.gameObject);

            var so = new SerializedObject(view);
            SetRef(so, "_icon", iconImg);
            SetRef(so, "_titleLabel", titleLabel);
            SetRef(so, "_levelLabel", levelLabel);
            SetRef(so, "_priceLabel", priceLabel);
            SetRef(so, "_buyButton", buyBtn);
            so.ApplyModifiedProperties();

            return view;
        }

        private static TMPro.TMP_Text CreateRowLabel(Transform parent, string name, string text, float width)
        {
            var label = UIBuildHelpers.CreateLabel(parent, name, text, UIBuildHelpers.BodyFontPath,
                28f, Color.white, TMPro.TextAlignmentOptions.MidlineLeft);
            var le = label.GetComponent<LayoutElement>();
            if (le == null) le = Undo.AddComponent<LayoutElement>(label.gameObject);
            le.preferredWidth = width;
            return label;
        }

        private static void SetRef(SerializedObject so, string propName, Object value)
        {
            var prop = so.FindProperty(propName);
            if (prop != null)
                prop.objectReferenceValue = value;
            else
                Debug.LogWarning("[Editor] Поле " + so.targetObject.GetType().Name + "." + propName +
                                  " не найдено (рантайм ещё не скомпилирован?).");
        }

        private static void SetObjectArray(SerializedObject so, string propName, Object[] values)
        {
            var prop = so.FindProperty(propName);
            if (prop == null)
            {
                Debug.LogWarning("[Editor] Поле " + so.targetObject.GetType().Name + "." + propName +
                                  " не найдено (рантайм ещё не скомпилирован?).");
                return;
            }

            prop.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
    }
}
#endif
