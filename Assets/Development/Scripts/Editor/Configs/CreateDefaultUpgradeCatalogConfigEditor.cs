#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using FlatOutLike.Data.Configs.Upgrades;
using FlatOutLike.Models;

namespace FlatOutLike.Editor.Configs
{
    /// <summary>
    /// Создаёт UpgradeCatalogConfig_Default.asset (Step 8a: Meta-экономика) с тремя
    /// категориями прокачки (Armor / Power / Nitro), каждая по 3 уровня:
    /// level 1: +10%/100, level 2: +20%/250, level 3: +30%/500.
    ///
    /// Идемпотентность: существующие категории (по _category) и их существующие уровни
    /// не пересоздаются — только дозаполняются недостающие.
    /// </summary>
    public static class CreateDefaultUpgradeCatalogConfigEditor
    {
        private const string AssetPath      = "Assets/Development/Data/Configs/Upgrades/UpgradeCatalogConfig_Default.asset";
        private const string FolderData     = "Assets/Development/Data";
        private const string FolderCfg      = "Assets/Development/Data/Configs";
        private const string FolderUpgrades = "Assets/Development/Data/Configs/Upgrades";

        private struct LevelDef
        {
            public int Level;
            public float EffectValue;
            public int Price;
        }

        private static readonly LevelDef[] Levels =
        {
            new LevelDef { Level = 1, EffectValue = 10f, Price = 100 },
            new LevelDef { Level = 2, EffectValue = 20f, Price = 250 },
            new LevelDef { Level = 3, EffectValue = 30f, Price = 500 },
        };

        private static readonly UpgradeCategory[] Categories =
        {
            UpgradeCategory.Armor,
            UpgradeCategory.Power,
            UpgradeCategory.Nitro,
        };

        [MenuItem("FlatOutLike/Configs/Create Default UpgradeCatalogConfig")]
        public static void CreateDefaultUpgradeCatalogConfig()
        {
            Undo.SetCurrentGroupName("Create Default UpgradeCatalogConfig");
            int undoGroup = Undo.GetCurrentGroup();

            EnsureFolder("Assets/Development", "Data");
            EnsureFolder(FolderData,           "Configs");
            EnsureFolder(FolderCfg,            "Upgrades");

            var config = AssetDatabase.LoadAssetAtPath<UpgradeCatalogConfig>(AssetPath);
            bool isNewAsset = config == null;
            if (isNewAsset)
            {
                config = ScriptableObject.CreateInstance<UpgradeCatalogConfig>();
                AssetDatabase.CreateAsset(config, AssetPath);
                Undo.RegisterCreatedObjectUndo(config, "Create UpgradeCatalogConfig_Default");
                Debug.Log("[Editor] Создан UpgradeCatalogConfig_Default по " + AssetPath);
            }
            else
            {
                Debug.Log("[Editor] UpgradeCatalogConfig_Default уже существует — дозаполняем недостающее.");
            }

            var so = new SerializedObject(config);
            var categoriesProp = so.FindProperty("_categories");
            if (categoriesProp == null)
            {
                Debug.LogWarning("[Editor] Поле UpgradeCatalogConfig._categories не найдено. " +
                                  "Возможно, рантайм Step 8a ещё не скомпилировался.");
                Undo.CollapseUndoOperations(undoGroup);
                return;
            }

            foreach (var category in Categories)
            {
                int categoryIndex = FindCategoryIndex(categoriesProp, category);
                if (categoryIndex < 0)
                {
                    categoryIndex = categoriesProp.arraySize;
                    categoriesProp.arraySize++;
                    var newCategoryEntry = categoriesProp.GetArrayElementAtIndex(categoryIndex);
                    newCategoryEntry.FindPropertyRelative("_category").enumValueIndex = (int)category;
                    Debug.Log("[Editor] Добавлена категория '" + category + "'.");
                }

                var categoryEntry = categoriesProp.GetArrayElementAtIndex(categoryIndex);
                var levelsProp = categoryEntry.FindPropertyRelative("_levels");
                if (levelsProp == null)
                {
                    Debug.LogWarning("[Editor] Поле _levels не найдено у категории '" + category + "'.");
                    continue;
                }

                foreach (var levelDef in Levels)
                {
                    int levelIndex = FindLevelIndex(levelsProp, levelDef.Level);
                    if (levelIndex >= 0)
                    {
                        Debug.Log("[Editor] " + category + " level " + levelDef.Level + " уже существует — пропускаем.");
                        continue;
                    }

                    int newLevelIndex = levelsProp.arraySize;
                    levelsProp.arraySize++;
                    var levelEntry = levelsProp.GetArrayElementAtIndex(newLevelIndex);
                    levelEntry.FindPropertyRelative("_level").intValue         = levelDef.Level;
                    levelEntry.FindPropertyRelative("_effectValue").floatValue = levelDef.EffectValue;
                    levelEntry.FindPropertyRelative("_price").intValue         = levelDef.Price;

                    Debug.Log("[Editor] " + category + " level " + levelDef.Level +
                              " добавлен (effect=+" + levelDef.EffectValue + "%, price=" + levelDef.Price + ").");
                }
            }

            so.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = config;
            EditorGUIUtility.PingObject(config);

            Undo.CollapseUndoOperations(undoGroup);

            Debug.Log("[Editor] Create Default UpgradeCatalogConfig завершён.");
        }

        private static int FindCategoryIndex(SerializedProperty categoriesProp, UpgradeCategory category)
        {
            for (int i = 0; i < categoriesProp.arraySize; i++)
            {
                var categoryProp = categoriesProp.GetArrayElementAtIndex(i).FindPropertyRelative("_category");
                if (categoryProp != null && categoryProp.enumValueIndex == (int)category)
                    return i;
            }
            return -1;
        }

        private static int FindLevelIndex(SerializedProperty levelsProp, int level)
        {
            for (int i = 0; i < levelsProp.arraySize; i++)
            {
                var levelProp = levelsProp.GetArrayElementAtIndex(i).FindPropertyRelative("_level");
                if (levelProp != null && levelProp.intValue == level)
                    return i;
            }
            return -1;
        }

        private static void EnsureFolder(string parent, string folderName)
        {
            string full = parent + "/" + folderName;
            if (!AssetDatabase.IsValidFolder(full))
            {
                AssetDatabase.CreateFolder(parent, folderName);
                Debug.Log("[Editor] Created folder " + full);
            }
        }
    }
}
#endif
