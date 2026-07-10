#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using FlatOutLike.Data.Configs.Stunts;
using FlatOutLike.Installers;
using FlatOutLike.Views.UI.Garage;
using FlatOutLike.Views.UI.Hud;
using FlatOutLike.Views.UI.MainMenu;
using FlatOutLike.Views.UI.Overlay;
using FlatOutLike.Views.UI.Stunts;
using Object = UnityEngine.Object;

namespace FlatOutLike.Editor.Setup
{
    /// <summary>
    /// Шаг 8c/8d: прошивает GameLifetimeScope активной сцены ссылками на UI-View'ы
    /// (_mainMenuView / _garageView / _stuntsHubView / _matchEndView / _derbyHudView /
    /// _highJumpHudView) и на StuntsCatalogConfig_Default (_stuntsCatalogConfig). Единственное место, где
    /// прошиваются эти поля — каждый Build*UIEditor вызывает соответствующий метод
    /// сразу после создания своего View; отдельный пункт меню — для ручной перепрошивки.
    ///
    /// Правила editor-files.md:
    ///   • null не присваиваем — если ассет/поле не найдено, Warning и существующая
    ///     ссылка остаётся нетронутой (иначе повторный прогон ломает рабочую проводку).
    ///   • У StuntsCatalogConfig вариантов по сцене нет — ассет один (_Default),
    ///     прошивается одинаково во всех сценах, где есть scope.
    /// </summary>
    public static class BindMetaUIRefsEditor
    {
        private const string StuntsCatalogPath =
            "Assets/Development/Data/Configs/Stunts/StuntsCatalogConfig_Default.asset";

        [MenuItem("FlatOutLike/Setup/Bind Meta UI Refs (Step 8c)")]
        public static void BindAllInActiveScene()
        {
            Undo.SetCurrentGroupName("Bind Meta UI Refs (Step 8c)");
            int undoGroup = Undo.GetCurrentGroup();

            var mainMenuView = Object.FindAnyObjectByType<MainMenuView>();
            if (mainMenuView != null) BindMainMenuView(mainMenuView);

            var garageView = Object.FindAnyObjectByType<GarageView>();
            if (garageView != null) BindGarageView(garageView);

            var stuntsHubView = Object.FindAnyObjectByType<StuntsHubView>();
            if (stuntsHubView != null) BindStuntsHubView(stuntsHubView);

            var matchEndView = Object.FindAnyObjectByType<MatchEndView>();
            if (matchEndView != null) BindMatchEndView(matchEndView);

            var derbyHudView = Object.FindAnyObjectByType<DerbyHudView>();
            if (derbyHudView != null) BindDerbyHudView(derbyHudView);

            var highJumpHudView = Object.FindAnyObjectByType<HighJumpHudView>();
            if (highJumpHudView != null) BindHighJumpHudView(highJumpHudView);

            BindStuntsCatalogConfig();

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Undo.CollapseUndoOperations(undoGroup);
            Debug.Log("[Editor] Bind Meta UI Refs (Step 8c) завершён для активной сцены.");
        }

        public static void BindMainMenuView(MainMenuView view)   => BindField("_mainMenuView", view);
        public static void BindGarageView(GarageView view)       => BindField("_garageView", view);
        public static void BindStuntsHubView(StuntsHubView view) => BindField("_stuntsHubView", view);
        public static void BindMatchEndView(MatchEndView view)   => BindField("_matchEndView", view);
        public static void BindDerbyHudView(DerbyHudView view)         => BindField("_derbyHudView", view);
        public static void BindHighJumpHudView(HighJumpHudView view)   => BindField("_highJumpHudView", view);

        public static void BindStuntsCatalogConfig()
        {
            var scope = Object.FindAnyObjectByType<GameLifetimeScope>();
            if (scope == null)
            {
                Debug.LogWarning("[Editor] GameLifetimeScope не найден в активной сцене — " +
                                  "_stuntsCatalogConfig не прошит.");
                return;
            }

            var config = AssetDatabase.LoadAssetAtPath<StuntsCatalogConfig>(StuntsCatalogPath);
            if (config == null)
            {
                Debug.LogWarning("[Editor] StuntsCatalogConfig_Default не найден по " + StuntsCatalogPath +
                                  ". Запусти 'FlatOutLike/Configs/Create Default StuntsCatalogConfig' сначала.");
                return;
            }

            var so = new SerializedObject(scope);
            var prop = so.FindProperty("_stuntsCatalogConfig");
            if (prop == null)
            {
                Debug.LogWarning("[Editor] Поле GameLifetimeScope._stuntsCatalogConfig не найдено " +
                                  "(рантайм ещё не скомпилирован?).");
                return;
            }

            prop.objectReferenceValue = config;
            so.ApplyModifiedProperties();
            Debug.Log("[Editor] GameLifetimeScope._stuntsCatalogConfig прошит.");
        }

        private static void BindField(string fieldName, Object value)
        {
            var scope = Object.FindAnyObjectByType<GameLifetimeScope>();
            if (scope == null)
            {
                Debug.LogWarning("[Editor] GameLifetimeScope не найден в активной сцене — " +
                                  fieldName + " не прошит.");
                return;
            }

            var so = new SerializedObject(scope);
            var prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                Debug.LogWarning("[Editor] Поле GameLifetimeScope." + fieldName + " не найдено " +
                                  "(рантайм ещё не скомпилирован?).");
                return;
            }

            prop.objectReferenceValue = value;
            so.ApplyModifiedProperties();
            Debug.Log("[Editor] GameLifetimeScope." + fieldName + " прошит.");
        }
    }
}
#endif
