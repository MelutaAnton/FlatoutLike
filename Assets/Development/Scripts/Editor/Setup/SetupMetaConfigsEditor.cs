#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using FlatOutLike.Data.Configs.Car;
using FlatOutLike.Data.Configs.Upgrades;
using FlatOutLike.Installers;

namespace FlatOutLike.Editor.Setup
{
    /// <summary>
    /// Прошивает GameLifetimeScope в открытой сцене ссылками на Meta-конфиги
    /// Step 8a: _carCatalogConfig / _upgradeCatalogConfig.
    ///
    /// У этих конфигов НЕТ вариантов по сценам (в отличие от eject/orbit-конфигов) —
    /// один и тот же CarCatalogConfig_Default / UpgradeCatalogConfig_Default
    /// прошивается независимо от активной сцены.
    ///
    /// Идемпотентность: null НЕ прошиваем — если ассет не найден, оставляем
    /// существующую ссылку в поле нетронутой (иначе повторный прогон в сцене без
    /// собранных конфигов затёр бы рабочую проводку пустотой).
    ///
    /// Запускать ПОСЛЕ:
    ///   FlatOutLike/Configs/Create Default CarCatalogConfig
    ///   FlatOutLike/Configs/Create Default UpgradeCatalogConfig
    /// </summary>
    public static class SetupMetaConfigsEditor
    {
        private const string CarCatalogConfigPath     = "Assets/Development/Data/Configs/Car/CarCatalogConfig_Default.asset";
        private const string UpgradeCatalogConfigPath = "Assets/Development/Data/Configs/Upgrades/UpgradeCatalogConfig_Default.asset";

        [MenuItem("FlatOutLike/Setup/Setup Meta Configs (Step 8a)")]
        public static void SetupMetaConfigs()
        {
            Undo.SetCurrentGroupName("Setup Meta Configs (Step 8a)");
            int undoGroup = Undo.GetCurrentGroup();

            var scope = Object.FindAnyObjectByType<GameLifetimeScope>();
            if (scope == null)
            {
                Debug.LogWarning("[Editor] GameLifetimeScope не найден в сцене. Ссылки " +
                                  "_carCatalogConfig / _upgradeCatalogConfig не заполнены.");
                Undo.CollapseUndoOperations(undoGroup);
                return;
            }

            var carCatalogConfig = AssetDatabase.LoadAssetAtPath<CarCatalogConfig>(CarCatalogConfigPath);
            if (carCatalogConfig == null)
                Debug.LogWarning("[Editor] CarCatalogConfig_Default не найден по " + CarCatalogConfigPath +
                                  ". Запусти 'FlatOutLike/Configs/Create Default CarCatalogConfig' сначала.");

            var upgradeCatalogConfig = AssetDatabase.LoadAssetAtPath<UpgradeCatalogConfig>(UpgradeCatalogConfigPath);
            if (upgradeCatalogConfig == null)
                Debug.LogWarning("[Editor] UpgradeCatalogConfig_Default не найден по " + UpgradeCatalogConfigPath +
                                  ". Запусти 'FlatOutLike/Configs/Create Default UpgradeCatalogConfig' сначала.");

            var scopeSo = new SerializedObject(scope);

            // null НЕ прошиваем: если ассет не найден, оставляем существующую ссылку —
            // иначе повторный прогон затирал бы рабочую проводку сцены пустотой.
            var carCatalogProp = scopeSo.FindProperty("_carCatalogConfig");
            if (carCatalogProp != null)
            {
                if (carCatalogConfig != null)
                    carCatalogProp.objectReferenceValue = carCatalogConfig;
            }
            else
                Debug.LogWarning("[Editor] Поле GameLifetimeScope._carCatalogConfig не найдено. " +
                                  "Возможно, рантайм Step 8a ещё не скомпилировался.");

            var upgradeCatalogProp = scopeSo.FindProperty("_upgradeCatalogConfig");
            if (upgradeCatalogProp != null)
            {
                if (upgradeCatalogConfig != null)
                    upgradeCatalogProp.objectReferenceValue = upgradeCatalogConfig;
            }
            else
                Debug.LogWarning("[Editor] Поле GameLifetimeScope._upgradeCatalogConfig не найдено. " +
                                  "Возможно, рантайм Step 8a ещё не скомпилировался.");

            scopeSo.ApplyModifiedProperties();
            Debug.Log("[Editor] GameLifetimeScope: _carCatalogConfig / _upgradeCatalogConfig заполнены " +
                      "(там, где поля существуют и ассеты найдены).");

            var activeScene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);

            Undo.CollapseUndoOperations(undoGroup);

            Debug.Log("[Editor] Setup Meta Configs (Step 8a) завершён и сцена сохранена.");
        }
    }
}
#endif
