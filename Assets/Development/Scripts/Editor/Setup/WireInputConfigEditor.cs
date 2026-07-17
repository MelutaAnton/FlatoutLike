#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using FlatOutLike.Data.Configs.Input;
using FlatOutLike.Editor.Configs;
using FlatOutLike.Installers;

namespace FlatOutLike.Editor.Setup
{
    /// <summary>
    /// Прошивает единый InputConfig (см. CreateDefaultInputConfigEditor) в поле
    /// GameLifetimeScope._inputConfig на КАЖДОЙ enabled-сцене из Build Settings.
    ///
    /// До этого шага переключение мобильный HUD / клавиатура держалось на поле
    /// _forceMobileInput прямо на GameLifetimeScope — теперь оно вынесено в общий
    /// ассет, и каждую сцену нужно перепрошить один раз (миграция).
    ///
    /// Открывает сцены по очереди (OpenScene Single), поэтому запускать вне Play Mode
    /// и НЕ во время работы над несохранённой сценой без явного намерения её потерять —
    /// первым шагом скрипт предложит сохранить несохранённые изменения (Cancel = выход).
    /// </summary>
    public static class WireInputConfigEditor
    {
        [MenuItem("FlatOutLike/Setup/Wire Input Config (All Scenes)")]
        public static void WireInputConfig()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log("[Editor] Wire Input Config: отменено пользователем.");
                return;
            }

            string originalScenePath = SceneManager.GetActiveScene().path;

            var config = CreateDefaultInputConfigEditor.EnsureAsset();

            var scenes = EditorBuildSettings.scenes;
            int wired = 0;
            int skipped = 0;

            foreach (var buildScene in scenes)
            {
                if (!buildScene.enabled)
                    continue;

                // Build Settings может хранить enabled-запись с удалённым файлом сцены —
                // OpenScene на битом пути бросил бы исключение и оборвал миграцию.
                if (string.IsNullOrEmpty(buildScene.path) || !System.IO.File.Exists(buildScene.path))
                {
                    Debug.LogWarning("[Editor] Wire Input Config: файл сцены не найден — " +
                        buildScene.path + ", пропуск.");
                    skipped++;
                    continue;
                }

                var scene = EditorSceneManager.OpenScene(buildScene.path, OpenSceneMode.Single);

                var scope = Object.FindAnyObjectByType<GameLifetimeScope>();
                if (scope == null)
                {
                    Debug.Log("[Editor] " + buildScene.path + ": GameLifetimeScope не найден, пропуск.");
                    skipped++;
                    continue;
                }

                var so = new SerializedObject(scope);
                var prop = so.FindProperty("_inputConfig");
                if (prop == null)
                {
                    Debug.LogWarning("[Editor] " + buildScene.path +
                        ": поле GameLifetimeScope._inputConfig не найдено (рантайм ещё не скомпилирован?).");
                    skipped++;
                    continue;
                }

                // Сохраняем сцену только при фактической правке — повторный прогон миграции
                // не должен пере-сохранять неизменённые сцены (лишний git-diff).
                if (prop.objectReferenceValue != (Object)config)
                {
                    prop.objectReferenceValue = config;
                    so.ApplyModifiedProperties();
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    Debug.Log("[Editor] " + buildScene.path + ": GameLifetimeScope._inputConfig прошит.");
                }
                else
                {
                    Debug.Log("[Editor] " + buildScene.path + ": _inputConfig уже прошит, без изменений.");
                }

                wired++;
            }

            RestoreOriginalScene(originalScenePath);

            Debug.Log("[Editor] Wire Input Config (All Scenes): прошито " + wired +
                ", пропущено " + skipped + " из " + scenes.Length + " enabled-сцен.");
        }

        private static void RestoreOriginalScene(string originalScenePath)
        {
            if (string.IsNullOrEmpty(originalScenePath))
                return;

            if (SceneManager.GetActiveScene().path == originalScenePath)
                return;

            EditorSceneManager.OpenScene(originalScenePath, OpenSceneMode.Single);
            Debug.Log("[Editor] Возврат в исходную сцену: " + originalScenePath);
        }
    }
}
#endif
