#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using FlatOutLike.Data.Configs.Stunts;
using FlatOutLike.Editor.Setup;
using FlatOutLike.Views.UI.Stunts;

namespace FlatOutLike.Editor.UI
{
    /// <summary>
    /// Шаг 8c: наполняет UICanvas сцены Stunts_Hub — фон, сетка 3x2 из шести
    /// StuntsCardView (превью/тайтл/бейдж "Coming soon"/кнопка) и кнопку Back.
    /// Начальные тайтл/превью/видимость бейджа читаются из StuntsCatalogConfig_Default
    /// (индекс карточки == порядок enum StuntType) как удобное превью в редакторе —
    /// на рантайме StuntsHubView перечитывает те же данные из каталога сама.
    ///
    /// Вешает StuntsHubView на UICanvas, прошивает _cards и прошивает GameLifetimeScope
    /// (_stuntsHubView + _stuntsCatalogConfig) через BindMetaUIRefsEditor.
    ///
    /// Запускать на ОТКРЫТОЙ сцене Stunts_Hub, ПОСЛЕ:
    ///   FlatOutLike/Scenes/Build Meta Scenes (Step 8b)
    ///   FlatOutLike/Tools/Generate TMP Font Assets
    ///   FlatOutLike/Configs/Create Default StuntsCatalogConfig
    /// </summary>
    public static class BuildStuntsHubUIEditor
    {
        private const string BackgroundPath = UIBuildHelpers.UiPackRoot + "/Textures/dark back1.jpg";
        private const string CardBgPath     = UIBuildHelpers.UiPackRoot + "/Buttons/png/button2.png";
        private const string BackButtonPath = UIBuildHelpers.UiPackRoot + "/Buttons/png/back button.png";
        private const string StuntsCatalogPath =
            "Assets/Development/Data/Configs/Stunts/StuntsCatalogConfig_Default.asset";

        private const int Columns = 3;
        private const int Rows = 2;
        private const int CardCount = Columns * Rows;

        [MenuItem("FlatOutLike/UI/Build StuntsHub UI")]
        public static void BuildStuntsHubUI()
        {
            Undo.SetCurrentGroupName("Build StuntsHub UI (Step 8c)");
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

            var catalog = AssetDatabase.LoadAssetAtPath<StuntsCatalogConfig>(StuntsCatalogPath);
            if (catalog == null)
                Debug.LogWarning("[Editor] StuntsCatalogConfig_Default не найден по " + StuntsCatalogPath +
                                  ". Карточки соберутся с плейсхолдер-текстом (данные подставит рантайм).");

            var grid = UIBuildHelpers.FindOrCreateChild(root, "StuntsGrid");
            UIBuildHelpers.Anchor(grid, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(1360f, 700f));

            var gridLayout = grid.GetComponent<GridLayoutGroup>();
            if (gridLayout == null) gridLayout = Undo.AddComponent<GridLayoutGroup>(grid.gameObject);
            gridLayout.cellSize = new Vector2(420f, 320f);
            gridLayout.spacing = new Vector2(40f, 40f);
            gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayout.constraintCount = Columns;
            gridLayout.childAlignment = TextAnchor.MiddleCenter;

            var cardViews = new StuntsCardView[CardCount];
            for (int i = 0; i < CardCount; i++)
                cardViews[i] = BuildStuntsCard(grid, i, catalog);

            var backBtn = UIBuildHelpers.CreateButton(root, "BackButton", BackButtonPath, null,
                UIBuildHelpers.DisplayFontPath, 32f, Color.white);
            UIBuildHelpers.Anchor((RectTransform)backBtn.transform, new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(0f, 0f), new Vector2(40f, 40f), new Vector2(160f, 80f));

            var view = canvas.GetComponent<StuntsHubView>();
            if (view == null) view = Undo.AddComponent<StuntsHubView>(canvas.gameObject);

            var so = new SerializedObject(view);
            var cardsProp = so.FindProperty("_cards");
            if (cardsProp != null)
            {
                cardsProp.arraySize = cardViews.Length;
                for (int i = 0; i < cardViews.Length; i++)
                    cardsProp.GetArrayElementAtIndex(i).objectReferenceValue = cardViews[i];
            }
            else
            {
                Debug.LogWarning("[Editor] Поле StuntsHubView._cards не найдено (рантайм ещё не скомпилирован?).");
            }

            var backProp = so.FindProperty("_backButton");
            if (backProp != null) backProp.objectReferenceValue = backBtn;
            so.ApplyModifiedProperties();

            BindMetaUIRefsEditor.BindStuntsHubView(view);
            BindMetaUIRefsEditor.BindStuntsCatalogConfig();

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Undo.CollapseUndoOperations(undoGroup);
            Debug.Log("[Editor] StuntsHub UI собран.");
        }

        private static StuntsCardView BuildStuntsCard(Transform parent, int index, StuntsCatalogConfig catalog)
        {
            var cardRt = UIBuildHelpers.FindOrCreateChild(parent, "StuntsCard_" + index);

            var bgImg = cardRt.GetComponent<Image>();
            if (bgImg == null) bgImg = Undo.AddComponent<Image>(cardRt.gameObject);
            bgImg.sprite = UIBuildHelpers.LoadSprite(CardBgPath);
            bgImg.type = Image.Type.Sliced;

            var button = cardRt.GetComponent<Button>();
            if (button == null) button = Undo.AddComponent<Button>(cardRt.gameObject);
            button.targetGraphic = bgImg;

            var previewRt = UIBuildHelpers.FindOrCreateChild(cardRt, "Preview");
            UIBuildHelpers.Anchor(previewRt, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(220f, 180f));
            var previewImg = previewRt.GetComponent<Image>();
            if (previewImg == null) previewImg = Undo.AddComponent<Image>(previewRt.gameObject);
            previewImg.preserveAspect = true;

            string title = "Stunt " + (index + 1);
            bool enabled = false;
            Sprite preview = null;

            // Читаем entry напрямую через SerializedObject по полям контракта (_type/_title/_enabled/_preview),
            // а не через рантайм-API StuntsCatalogConfig — методы/свойства конфига ещё не зафиксированы контрактом.
            if (catalog != null)
            {
                var catalogSo = new SerializedObject(catalog);
                var entriesProp = catalogSo.FindProperty("_entries");
                if (entriesProp != null)
                {
                    for (int i = 0; i < entriesProp.arraySize; i++)
                    {
                        var entry = entriesProp.GetArrayElementAtIndex(i);
                        var typeProp = entry.FindPropertyRelative("_type");
                        if (typeProp == null || typeProp.enumValueIndex != index)
                            continue;

                        var titleProp2 = entry.FindPropertyRelative("_title");
                        var enabledProp = entry.FindPropertyRelative("_enabled");
                        var previewProp2 = entry.FindPropertyRelative("_preview");

                        if (titleProp2 != null && !string.IsNullOrEmpty(titleProp2.stringValue))
                            title = titleProp2.stringValue;
                        if (enabledProp != null)
                            enabled = enabledProp.boolValue;
                        if (previewProp2 != null)
                            preview = previewProp2.objectReferenceValue as Sprite;

                        break;
                    }
                }
            }

            previewImg.sprite = preview;

            var titleLabel = UIBuildHelpers.CreateLabel(cardRt, "TitleLabel", title,
                UIBuildHelpers.DisplayFontPath, 28f, UIBuildHelpers.DarkTextColor); // карточка белая — белый тайтл невидим
            UIBuildHelpers.Anchor((RectTransform)titleLabel.transform, new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(-20f, 48f));

            var badgeRt = UIBuildHelpers.FindOrCreateChild(cardRt, "ComingSoonBadge");
            UIBuildHelpers.Anchor(badgeRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300f, 70f));
            var badgeBg = badgeRt.GetComponent<Image>();
            if (badgeBg == null) badgeBg = Undo.AddComponent<Image>(badgeRt.gameObject);
            if (badgeBg.sprite == null) badgeBg.color = new Color(0f, 0f, 0f, 0.7f);
            badgeBg.raycastTarget = false;

            UIBuildHelpers.CreateLabel(badgeRt, "Label", "COMING SOON",
                UIBuildHelpers.DisplayFontPath, 24f, Color.white);

            badgeRt.gameObject.SetActive(!enabled);
            button.interactable = enabled;

            var view = cardRt.GetComponent<StuntsCardView>();
            if (view == null) view = Undo.AddComponent<StuntsCardView>(cardRt.gameObject);

            var so = new SerializedObject(view);
            var previewProp = so.FindProperty("_preview");
            if (previewProp != null) previewProp.objectReferenceValue = previewImg;
            var titleProp = so.FindProperty("_titleLabel");
            if (titleProp != null) titleProp.objectReferenceValue = titleLabel;
            var badgeProp = so.FindProperty("_comingSoonBadge");
            if (badgeProp != null) badgeProp.objectReferenceValue = badgeRt.gameObject;
            var buttonProp = so.FindProperty("_button");
            if (buttonProp != null) buttonProp.objectReferenceValue = button;
            so.ApplyModifiedProperties();

            return view;
        }
    }
}
#endif
