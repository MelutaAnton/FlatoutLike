#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using FlatOutLike.Data.Configs.Car;

namespace FlatOutLike.Editor.Configs
{
    public static class CreateDefaultCarDamageVisualConfigEditor
    {
        private const string AssetPath     = "Assets/Development/Data/Configs/Car/CarDamageVisualConfig.asset";
        private const string FolderData    = "Assets/Development/Data";
        private const string FolderCfg     = "Assets/Development/Data/Configs";
        private const string FolderCar     = "Assets/Development/Data/Configs/Car";
        private const string CarPrefabPath = "Assets/Development/Prefabs/Vehicles/Car_Sport_09.prefab";

        [MenuItem("FlatOutLike/Configs/Create Default CarDamageVisualConfig")]
        public static void CreateDefaultCarDamageVisualConfig()
        {
            // ── Проверка идемпотентности ──────────────────────────────────────
            if (AssetDatabase.LoadAssetAtPath<CarDamageVisualConfig>(AssetPath) != null)
            {
                Debug.Log("[Editor] CarDamageVisualConfig already exists at " + AssetPath);
                return;
            }

            // ── Убеждаемся, что иерархия папок существует ─────────────────────
            EnsureFolder("Assets/Development", "Data");
            EnsureFolder(FolderData,           "Configs");
            EnsureFolder(FolderCfg,            "Car");

            // ── Создание ассета ───────────────────────────────────────────────
            var config = ScriptableObject.CreateInstance<CarDamageVisualConfig>();
            AssetDatabase.CreateAsset(config, AssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // ── Заполнение 3 стадий через SerializedObject ────────────────────
            var so = new SerializedObject(config);
            var stagesProp = so.FindProperty("_stages");
            stagesProp.arraySize = 3;

            // ── Стадия 0: Pristine (hpThreshold = 1.0) ───────────────────────
            // Materials берутся с рендерера Car_Sport_09 (базовая линия Strategy A).
            // damagedMesh / blendShapeWeights / vfxToActivate оставлены пустыми (только strategy A).
            var pristine = stagesProp.GetArrayElementAtIndex(0);
            pristine.FindPropertyRelative("hpThreshold").floatValue       = 1.0f;
            pristine.FindPropertyRelative("enableDetachableParts").boolValue = false;
            pristine.FindPropertyRelative("decalPrefabsToSpawn").arraySize = 0;
            pristine.FindPropertyRelative("blendShapeWeights").arraySize   = 0;
            pristine.FindPropertyRelative("vfxToActivate").arraySize       = 0;

            var materialsProp = pristine.FindPropertyRelative("materials");
            var carPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CarPrefabPath);
            if (carPrefab != null)
            {
                var renderer = carPrefab.GetComponentInChildren<Renderer>();
                if (renderer != null)
                {
                    var mats = renderer.sharedMaterials;
                    materialsProp.arraySize = mats.Length;
                    for (int i = 0; i < mats.Length; i++)
                        materialsProp.GetArrayElementAtIndex(i).objectReferenceValue = mats[i];
                    Debug.Log("[Editor] Pristine stage: copied " + mats.Length +
                              " material(s) from renderer '" + renderer.name + "' on " + CarPrefabPath);
                }
                else
                {
                    materialsProp.arraySize = 0;
                    Debug.LogWarning("[Editor] No Renderer found in " + CarPrefabPath +
                                     ". Pristine stage materials left empty.");
                }
            }
            else
            {
                materialsProp.arraySize = 0;
                Debug.LogWarning("[Editor] Car prefab not found at " + CarPrefabPath +
                                 ". Pristine stage materials left empty. " +
                                 "Run 'FlatOutLike/Setup/Build Car Prefab From Selected FBX' first.");
            }

            // ── Стадия 1: Dented (hpThreshold = 0.66) ────────────────────────
            // Materials, decals, VFX оставлены пустыми — заполнить вручную или на следующем шаге.
            var dented = stagesProp.GetArrayElementAtIndex(1);
            dented.FindPropertyRelative("hpThreshold").floatValue          = 0.66f;
            dented.FindPropertyRelative("enableDetachableParts").boolValue  = false;
            dented.FindPropertyRelative("materials").arraySize              = 0;
            dented.FindPropertyRelative("decalPrefabsToSpawn").arraySize    = 0;
            dented.FindPropertyRelative("blendShapeWeights").arraySize      = 0;
            dented.FindPropertyRelative("vfxToActivate").arraySize          = 0;

            // ── Стадия 2: Wrecked (hpThreshold = 0.33) ───────────────────────
            // enableDetachableParts = true — joints получают конечный break force на этой стадии.
            var wrecked = stagesProp.GetArrayElementAtIndex(2);
            wrecked.FindPropertyRelative("hpThreshold").floatValue          = 0.33f;
            wrecked.FindPropertyRelative("enableDetachableParts").boolValue  = true;
            wrecked.FindPropertyRelative("materials").arraySize              = 0;
            wrecked.FindPropertyRelative("decalPrefabsToSpawn").arraySize    = 0;
            wrecked.FindPropertyRelative("blendShapeWeights").arraySize      = 0;
            wrecked.FindPropertyRelative("vfxToActivate").arraySize          = 0;

            so.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();

            Undo.RegisterCreatedObjectUndo(config, "Create CarDamageVisualConfig");

            Selection.activeObject      = config;
            EditorGUIUtility.PingObject(config);

            Debug.Log("[Editor] Created CarDamageVisualConfig at " + AssetPath +
                      " with 3 stages: Pristine(1.0) / Dented(0.66) / Wrecked(0.33, detachable=true).");
        }

        // ─────────────────────────────────────────────────────────────────────
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
