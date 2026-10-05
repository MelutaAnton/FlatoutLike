#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using FlatOutLike.Data.Configs.Stunts;
using FlatOutLike.Editor.UI;
using FlatOutLike.Models;

namespace FlatOutLike.Editor.Configs
{
    /// <summary>
    /// Шаг 8c: создаёт StuntsCatalogConfig_Default.asset — единственный источник правды
    /// о трюках (заголовок, флаг доступности, превью). Только HighJump включён;
    /// остальные пять — "Coming soon" (enabled=false). Ключа сцены у записи больше нет
    /// (запуск режимов — зоны BaseHub, шаг 11b).
    ///
    /// Идемпотентно по _type (enum StuntType — уникальный технический ключ трюка):
    /// существующие entries не пересоздаются.
    /// </summary>
    public static class CreateDefaultStuntsCatalogConfigEditor
    {
        private const string FolderData  = "Assets/Development/Data";
        private const string FolderCfg   = "Assets/Development/Data/Configs";
        private const string FolderStunt = "Assets/Development/Data/Configs/Stunts";
        private const string AssetPath   = FolderStunt + "/StuntsCatalogConfig_Default.asset";

        private const string StuntIconsFolder = "Assets/Development/Art/UI/StuntsIcons";

        private struct EntryDef
        {
            public StuntType Type;
            public string Title;
            public bool Enabled;
            public string PreviewPath;
        }

        [MenuItem("FlatOutLike/Configs/Create Default StuntsCatalogConfig")]
        public static void CreateDefaultStuntsCatalogConfig()
        {
            Undo.SetCurrentGroupName("Create Default StuntsCatalogConfig (Step 8c)");
            int undoGroup = Undo.GetCurrentGroup();

            EnsureFolder("Assets/Development", "Data");
            EnsureFolder(FolderData, "Configs");
            EnsureFolder(FolderCfg, "Stunts");

            var config = AssetDatabase.LoadAssetAtPath<StuntsCatalogConfig>(AssetPath);
            bool isNewAsset = config == null;
            if (isNewAsset)
            {
                config = ScriptableObject.CreateInstance<StuntsCatalogConfig>();
                AssetDatabase.CreateAsset(config, AssetPath);
                Undo.RegisterCreatedObjectUndo(config, "Create StuntsCatalogConfig_Default");
                Debug.Log("[Editor] Создан StuntsCatalogConfig_Default по " + AssetPath);
            }
            else
            {
                Debug.Log("[Editor] StuntsCatalogConfig_Default уже существует — дозаполняем недостающие entries.");
            }

            var entries = new[]
            {
                new EntryDef { Type = StuntType.HighJump,  Title = "High Jump",  Enabled = true,  PreviewPath = StuntIconsFolder + "/stunt_1.png" },
                new EntryDef { Type = StuntType.LongJump,  Title = "Long Jump",  Enabled = false, PreviewPath = StuntIconsFolder + "/stunt_2.png" },
                new EntryDef { Type = StuntType.FireRings, Title = "Fire Rings", Enabled = false, PreviewPath = StuntIconsFolder + "/stunt_3.png" },
                new EntryDef { Type = StuntType.Bowling,   Title = "Bowling",    Enabled = false, PreviewPath = StuntIconsFolder + "/stunt_4.png" },
                new EntryDef { Type = StuntType.FieldGoal, Title = "Field Goal", Enabled = false, PreviewPath = StuntIconsFolder + "/stunt_5.png" },
                new EntryDef { Type = StuntType.HighCard,  Title = "High Card",  Enabled = false, PreviewPath = StuntIconsFolder + "/stunt_6.png" },
            };

            var so = new SerializedObject(config);
            var entriesProp = so.FindProperty("_entries");
            if (entriesProp == null)
            {
                Debug.LogWarning("[Editor] Поле StuntsCatalogConfig._entries не найдено. " +
                                  "Возможно, рантайм ещё не скомпилировался.");
            }
            else
            {
                foreach (var def in entries)
                {
                    int existingIndex = FindEntryIndexByType(entriesProp, def.Type);
                    if (existingIndex >= 0)
                    {
                        Debug.Log("[Editor] Entry '" + def.Type + "' уже существует — пропускаем.");
                        continue;
                    }

                    int newIndex = entriesProp.arraySize;
                    entriesProp.arraySize++;
                    var entry = entriesProp.GetArrayElementAtIndex(newIndex);

                    entry.FindPropertyRelative("_type").enumValueIndex = (int)def.Type;
                    entry.FindPropertyRelative("_title").stringValue = def.Title;
                    entry.FindPropertyRelative("_enabled").boolValue = def.Enabled;

                    var preview = UIBuildHelpers.LoadSprite(def.PreviewPath);
                    if (preview != null)
                        entry.FindPropertyRelative("_preview").objectReferenceValue = preview;
                    else
                        Debug.LogWarning("[Editor] Превью не найдено по " + def.PreviewPath +
                                          " для '" + def.Type + "'. Поле _preview оставлено пустым.");

                    Debug.Log("[Editor] Добавлена entry '" + def.Type + "' (enabled=" + def.Enabled + ").");
                }

                so.ApplyModifiedProperties();
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = config;
            EditorGUIUtility.PingObject(config);

            Undo.CollapseUndoOperations(undoGroup);
            Debug.Log("[Editor] Create Default StuntsCatalogConfig (Step 8c) завершён.");
        }

        private static int FindEntryIndexByType(SerializedProperty entriesProp, StuntType type)
        {
            for (int i = 0; i < entriesProp.arraySize; i++)
            {
                var typeProp = entriesProp.GetArrayElementAtIndex(i).FindPropertyRelative("_type");
                if (typeProp != null && typeProp.enumValueIndex == (int)type)
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
