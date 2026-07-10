#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using FlatOutLike.Editor.Setup;
using FlatOutLike.Views.UI.MainMenu;

namespace FlatOutLike.Editor.UI
{
    /// <summary>
    /// Шаг 8c: наполняет UICanvas сцены MainMenu — фон, лого (ВРЕМЕННЫЙ placeholder,
    /// заменить перед релизом на финальный арт) и вертикальный столбец из трёх кнопок
    /// DERBY / STUNTS / GARAGE. Вешает MainMenuView на UICanvas, прошивает ссылки на
    /// кнопки и прошивает GameLifetimeScope (_mainMenuView + _stuntsCatalogConfig)
    /// через BindMetaUIRefsEditor.
    ///
    /// Запускать на ОТКРЫТОЙ сцене MainMenu, ПОСЛЕ:
    ///   FlatOutLike/Scenes/Build Meta Scenes (Step 8b)
    ///   FlatOutLike/Tools/Generate TMP Font Assets
    /// </summary>
    public static class BuildMainMenuUIEditor
    {
        private const string BackgroundPath   = UIBuildHelpers.UiPackRoot + "/Textures/menu back.jpg";
        private const string LogoPath         = UIBuildHelpers.UiPackRoot + "/Textures/Fastlane-Logo-copy-copy.png";
        private const string ButtonSpritePath = UIBuildHelpers.UiPackRoot + "/Buttons/png/button1.png";

        [MenuItem("FlatOutLike/UI/Build MainMenu UI")]
        public static void BuildMainMenuUI()
        {
            Undo.SetCurrentGroupName("Build MainMenu UI (Step 8c)");
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

            // Лого — ВРЕМЕННЫЙ placeholder из пака (Fastlane), заменить перед релизом.
            var logoRt = UIBuildHelpers.FindOrCreateChild(root, "Logo");
            UIBuildHelpers.Anchor(logoRt, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(640f, 220f));
            var logoImg = logoRt.GetComponent<Image>();
            if (logoImg == null) logoImg = Undo.AddComponent<Image>(logoRt.gameObject);
            logoImg.sprite = UIBuildHelpers.LoadSprite(LogoPath);
            logoImg.preserveAspect = true;
            logoImg.raycastTarget = false;

            // Столбец из трёх кнопок, по центру экрана.
            var buttonsRt = UIBuildHelpers.FindOrCreateChild(root, "ButtonsColumn");
            UIBuildHelpers.Anchor(buttonsRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(560f, 480f));

            var layout = buttonsRt.GetComponent<VerticalLayoutGroup>();
            if (layout == null) layout = Undo.AddComponent<VerticalLayoutGroup>(buttonsRt.gameObject);
            layout.spacing = 32f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var derbyBtn  = CreateMenuButton(buttonsRt, "DerbyButton", "DERBY");
            var stuntsBtn = CreateMenuButton(buttonsRt, "StuntsButton", "STUNTS");
            var garageBtn = CreateMenuButton(buttonsRt, "GarageButton", "GARAGE");

            // MainMenuView — единственный View-компонент этой мета-сцены, вешаем на сам UICanvas.
            var view = canvas.GetComponent<MainMenuView>();
            if (view == null) view = Undo.AddComponent<MainMenuView>(canvas.gameObject);

            var so = new SerializedObject(view);
            SetRef(so, "_derbyButton", derbyBtn);
            SetRef(so, "_stuntsButton", stuntsBtn);
            SetRef(so, "_garageButton", garageBtn);
            so.ApplyModifiedProperties();

            BindMetaUIRefsEditor.BindMainMenuView(view);
            BindMetaUIRefsEditor.BindStuntsCatalogConfig();

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Undo.CollapseUndoOperations(undoGroup);
            Debug.Log("[Editor] MainMenu UI собран.");
        }

        private static Button CreateMenuButton(Transform parent, string name, string label)
        {
            var btn = UIBuildHelpers.CreateButton(parent, name, ButtonSpritePath, label,
                UIBuildHelpers.DisplayFontPath, 40f, UIBuildHelpers.DarkTextColor);

            var le = btn.GetComponent<LayoutElement>();
            if (le == null) le = Undo.AddComponent<LayoutElement>(btn.gameObject);
            le.preferredHeight = 120f;
            return btn;
        }

        private static void SetRef(SerializedObject so, string propName, Object value)
        {
            var prop = so.FindProperty(propName);
            if (prop != null)
                prop.objectReferenceValue = value;
            else
                Debug.LogWarning("[Editor] Поле MainMenuView." + propName + " не найдено (рантайм ещё не скомпилирован?).");
        }
    }
}
#endif
