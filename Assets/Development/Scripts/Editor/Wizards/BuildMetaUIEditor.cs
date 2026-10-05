#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using FlatOutLike.Editor.Configs;
using FlatOutLike.Editor.Tools;
using FlatOutLike.Editor.UI;
using FlatOutLike.Models;

namespace FlatOutLike.Editor.Wizards
{
    /// <summary>
    /// Шаг 8c (агрегатор): полностью наполняет UI мета-экранов проекта одним прогоном.
    /// Порядок:
    ///   1. Generate TMP Font Assets   — Bungee/RussoOne/TitanOne (сцено-независимо).
    ///   2. Generate Car Icons         — превью машин из CarCatalogConfig_Default (сцено-независимо).
    ///   3. Create Default StuntsCatalogConfig — 6 трюков (сцено-независимо).
    ///   4. Build Garage UI            — в сцене Garage.
    ///   5. Build MatchEnd UI          — в сцене Derby_Arena_01.
    /// UI хаба (BaseHub) собирается отдельно (шаг 11b).
    /// Прошивка GameLifetimeScope (Шаг 8) выполняется ВНУТРИ каждого Build*UIEditor
    /// сразу после сборки своего View — см. BindMetaUIRefsEditor.
    ///
    /// Меню-путь по спецификации Шага 8c лежит под UI/, хотя это Wizard-оркестратор
    /// (запускает несколько отдельных editor-скриптов по порядку) — сами шаги доступны
    /// и по отдельности через свои собственные пункты меню.
    ///
    /// Идемпотентность: каждый шаг идемпотентен сам по себе (см. соответствующие скрипты);
    /// повторный запуск дозаполняет недостающее, не дублирует и не затирает готовое.
    ///
    /// Запускать ПОСЛЕ:
    ///   FlatOutLike/Scenes/Build Meta Scenes (Step 8b)
    ///   FlatOutLike/Configs/Create Default CarCatalogConfig
    /// </summary>
    public static class BuildMetaUIEditor
    {
        private const string ScenesFolder = "Assets/Development/Scenes";

        [MenuItem("FlatOutLike/UI/Build Meta UI (Step 8c)")]
        public static void BuildMetaUI()
        {
            // Даём пользователю сохранить (или осознанно выбросить) несохранённые правки
            // активной сцены ДО того, как OpenScene(Single) молча их потеряет; Cancel = выход.
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log("[Editor] Build Meta UI отменён пользователем (несохранённая сцена).");
                return;
            }

            Undo.SetCurrentGroupName("Build Meta UI (Step 8c)");
            int undoGroup = Undo.GetCurrentGroup();

            string originalScenePath = SceneManager.GetActiveScene().path;

            try
            {
                // ── Сцено-независимые шаги ──────────────────────────────────────
                GenerateTmpFontAssetsEditor.GenerateTmpFontAssets();
                GenerateCarIconsEditor.GenerateCarIcons();
                CreateDefaultStuntsCatalogConfigEditor.CreateDefaultStuntsCatalogConfig();

                // ── UI по сценам ─────────────────────────────────────────────────
                RunInScene(SceneKeys.Garage, BuildGarageUIEditor.BuildGarageUI);
                RunInScene(SceneKeys.DerbyArena01, BuildMatchEndUIEditor.BuildMatchEndUI);

                Debug.Log("[Editor] Build Meta UI (Step 8c) завершён.");
            }
            finally
            {
                RestoreOriginalScene(originalScenePath);
                Undo.CollapseUndoOperations(undoGroup);
            }
        }

        private static void RunInScene(string sceneKey, System.Action buildAction)
        {
            string scenePath = GetScenePath(sceneKey);
            string fullPath = Path.Combine(Directory.GetCurrentDirectory(), scenePath);

            if (!File.Exists(fullPath))
            {
                Debug.LogWarning("[Editor] Сцена " + scenePath + " не найдена — шаг пропущен. " +
                                  "Запусти 'FlatOutLike/Scenes/Build Meta Scenes (Step 8b)' сначала.");
                return;
            }

            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            buildAction();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, scenePath);
            Debug.Log("[Editor] UI сцены " + sceneKey + " сохранён.");
        }

        private static void RestoreOriginalScene(string originalScenePath)
        {
            if (string.IsNullOrEmpty(originalScenePath))
            {
                Debug.Log("[Editor] Исходная сцена была несохранённой (Untitled) — не восстанавливаем.");
                return;
            }

            if (SceneManager.GetActiveScene().path == originalScenePath)
                return;

            EditorSceneManager.OpenScene(originalScenePath, OpenSceneMode.Single);
            Debug.Log("[Editor] Возврат в исходную сцену: " + originalScenePath);
        }

        private static string GetScenePath(string sceneKey) => ScenesFolder + "/" + sceneKey + ".unity";
    }
}
#endif
