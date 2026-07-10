#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using FlatOutLike.Editor.UI;
using FlatOutLike.Models;

namespace FlatOutLike.Editor.Wizards
{
    /// <summary>
    /// Шаг 8d (агрегатор): собирает боевой HUD обеих игровых сцен одним прогоном.
    /// Порядок:
    ///   1. Build Derby HUD    — в сцене Derby_Arena_01 (HP + нитро + счётчик живых).
    ///   2. Build HighJump HUD — в сцене Stunts_HighJump (сначала мигрирует debug-оверлей,
    ///      затем строит driving/flight/score группы).
    /// Прошивка GameLifetimeScope (_derbyHudView / _highJumpHudView) выполняется ВНУТРИ
    /// каждого Build*HudEditor сразу после сборки своего View — см. BindMetaUIRefsEditor.
    ///
    /// Идемпотентность: каждый шаг идемпотентен сам по себе; повторный запуск дозаполняет
    /// недостающее, не дублирует и не затирает готовое.
    ///
    /// Запускать ПОСЛЕ:
    ///   FlatOutLike/Tools/Generate TMP Font Assets
    ///   FlatOutLike/UI/Build Meta UI (Step 8c) — Derby_Arena_01 уже должен иметь Canvas/MatchEndPanel
    /// </summary>
    public static class BuildGameHudEditor
    {
        private const string ScenesFolder = "Assets/Development/Scenes";

        [MenuItem("FlatOutLike/UI/Build Game HUD (Step 8d)")]
        public static void BuildGameHud()
        {
            // Даём пользователю сохранить (или осознанно выбросить) несохранённые правки
            // активной сцены ДО того, как OpenScene(Single) молча их потеряет; Cancel = выход.
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log("[Editor] Build Game HUD отменён пользователем (несохранённая сцена).");
                return;
            }

            Undo.SetCurrentGroupName("Build Game HUD (Step 8d)");
            int undoGroup = Undo.GetCurrentGroup();

            string originalScenePath = SceneManager.GetActiveScene().path;

            try
            {
                RunInScene(SceneKeys.DerbyArena01, BuildDerbyHudEditor.BuildDerbyHud);
                RunInScene(SceneKeys.StuntsHighJump, BuildHighJumpHudEditor.BuildHighJumpHud);

                Debug.Log("[Editor] Build Game HUD (Step 8d) завершён.");
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
                Debug.LogWarning("[Editor] Сцена " + scenePath + " не найдена — шаг пропущен.");
                return;
            }

            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            buildAction();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, scenePath);
            Debug.Log("[Editor] HUD сцены " + sceneKey + " сохранён.");
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
