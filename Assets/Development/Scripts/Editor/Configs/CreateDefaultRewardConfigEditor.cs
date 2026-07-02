#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using FlatOutLike.Data.Configs.Rewards;
using FlatOutLike.Installers;

namespace FlatOutLike.Editor.Configs
{
    /// <summary>
    /// Создаёт или обновляет Assets/Development/Data/Configs/Rewards/RewardConfig.asset
    /// и привязывает его к GameLifetimeScope на сцене Derby_Arena_01.
    ///
    /// Шаг 1 — RewardConfig.asset:
    ///   _coinsPerKill = 10  (монеты за убийство врага)
    ///   _winBonus     = 50  (бонус монет за победу в матче)
    ///
    /// Шаг 2 — Derby_Arena_01.unity:
    ///   GameLifetimeScope._rewardConfig = созданный RewardConfig.asset
    ///
    /// Идемпотентность: если ассет уже существует — поля перезаписываются, GUID сохраняется.
    ///
    /// Запускать ПОСЛЕ:
    ///   Сборки арены (GameLifetimeScope уже присутствует на сцене Derby_Arena_01)
    ///   Компиляции рантайма Step 5e (поле GameLifetimeScope._rewardConfig должно существовать)
    /// </summary>
    public static class CreateDefaultRewardConfigEditor
    {
        private const string AssetPath  = "Assets/Development/Data/Configs/Rewards/RewardConfig.asset";
        private const string FolderRoot = "Assets/Development/Data/Configs";
        private const string FolderDest = "Assets/Development/Data/Configs/Rewards";

        private const string ArenaScenePath = "Assets/Development/Scenes/Derby_Arena_01.unity";

        [MenuItem("FlatOutLike/Configs/Create Default RewardConfig")]
        public static void CreateDefaultRewardConfig()
        {
            Undo.SetCurrentGroupName("Create Default RewardConfig");

            // ── 1. Папки ─────────────────────────────────────────────────────
            EnsureFolder("Assets/Development/Data", "Configs");
            EnsureFolder(FolderRoot, "Rewards");

            // ── 2. Создаём или загружаем ассет (идемпотентно по пути) ─────────
            var config = AssetDatabase.LoadAssetAtPath<RewardConfig>(AssetPath);
            bool isNew = config == null;

            if (isNew)
            {
                config = ScriptableObject.CreateInstance<RewardConfig>();
                AssetDatabase.CreateAsset(config, AssetPath);
                AssetDatabase.SaveAssets();
                Undo.RegisterCreatedObjectUndo(config, "Create RewardConfig");
                Debug.Log("[Editor] Создан RewardConfig.asset: " + AssetPath);
            }
            else
            {
                Debug.Log("[Editor] RewardConfig.asset уже существует — обновляем поля.");
            }

            // ── 3. Заполняем поля через SerializedObject ──────────────────────
            var so = new SerializedObject(config);
            so.FindProperty("_coinsPerKill").intValue = 10;
            so.FindProperty("_winBonus").intValue      = 50;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();

            Debug.Log("[Editor] RewardConfig заполнен: _coinsPerKill=10, _winBonus=50.");

            // ── 4. Привязываем конфиг к GameLifetimeScope арены ───────────────
            WireConfigToArenaScope(config);
        }

        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Открывает Derby_Arena_01.unity (если она ещё не активна), находит
        /// GameLifetimeScope и заполняет _rewardConfig через SerializedObject.
        /// </summary>
        private static void WireConfigToArenaScope(RewardConfig config)
        {
            bool sceneExists = File.Exists(
                Path.Combine(Directory.GetCurrentDirectory(), ArenaScenePath));

            if (!sceneExists)
            {
                Debug.LogWarning("[Editor] Сцена не найдена по " + ArenaScenePath +
                                 ". RewardConfig._rewardConfig не будет привязан — " +
                                 "сначала собери арену.");
                return;
            }

            var activeScene = SceneManager.GetActiveScene();
            bool alreadyOpen = activeScene.IsValid() && activeScene.path == ArenaScenePath;

            if (!alreadyOpen)
            {
                // Открываем арену как единственную сцену; если есть несохранённые
                // изменения — EditorSceneManager сам предложит сохранить их.
                EditorSceneManager.OpenScene(ArenaScenePath, OpenSceneMode.Single);
                Debug.Log("[Editor] Открыта сцена " + ArenaScenePath + " для привязки RewardConfig.");
            }

            var scope = Object.FindAnyObjectByType<GameLifetimeScope>();
            if (scope == null)
            {
                Debug.LogWarning("[Editor] GameLifetimeScope не найден на арене, запусти сборку арены сначала.");
                return;
            }

            var scopeSo = new SerializedObject(scope);
            var rewardProp = scopeSo.FindProperty("_rewardConfig");
            if (rewardProp == null)
            {
                Debug.LogWarning("[Editor] Поле GameLifetimeScope._rewardConfig не найдено. " +
                                 "Возможно, рантайм Step 5e ещё не скомпилировался — привязка пропущена.");
                return;
            }

            rewardProp.objectReferenceValue = config;
            scopeSo.ApplyModifiedProperties();

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());

            Debug.Log("[Editor] GameLifetimeScope._rewardConfig → RewardConfig привязан и сцена сохранена.");
        }

        // ─────────────────────────────────────────────────────────────────────

        private static void EnsureFolder(string parent, string folderName)
        {
            string full = parent + "/" + folderName;
            if (!AssetDatabase.IsValidFolder(full))
            {
                AssetDatabase.CreateFolder(parent, folderName);
                Debug.Log("[Editor] Создана папка: " + full);
            }
        }
    }
}
#endif
