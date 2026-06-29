#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using FlatOutLike.Data.Configs.Car;

namespace FlatOutLike.Editor.Setup
{
    public static class FixCarStatsConfigEditor
    {
        [MenuItem("FlatOutLike/Setup/Fix CarStatsConfig NitroDrainRate")]
        public static void FixNitroDrainRate()
        {
            string[] guids = AssetDatabase.FindAssets("t:CarStatsConfig");
            int count = 0;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                CarStatsConfig asset = AssetDatabase.LoadAssetAtPath<CarStatsConfig>(path);

                if (asset == null)
                {
                    Debug.LogWarning($"[Editor] Не удалось загрузить CarStatsConfig по пути: {path}");
                    continue;
                }

                SerializedObject so = new SerializedObject(asset);
                SerializedProperty prop = so.FindProperty("_nitroDrainRate");

                if (prop == null)
                {
                    Debug.LogWarning($"[Editor] CarStatsConfig '{asset.name}': property _nitroDrainRate not found (may need reimport).");
                    continue;
                }

                if (prop.floatValue < 1f)
                {
                    prop.floatValue = 25f;
                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(asset);
                    Debug.Log($"[Editor] CarStatsConfig '{asset.name}': _nitroDrainRate set to 25.");
                }
                else
                {
                    Debug.Log($"[Editor] CarStatsConfig '{asset.name}': _nitroDrainRate уже равен {prop.floatValue}, пропускаем.");
                }

                count++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "Fix NitroDrainRate",
                $"Done. Processed {count} CarStatsConfig assets.",
                "OK"
            );
        }
    }
}
#endif
