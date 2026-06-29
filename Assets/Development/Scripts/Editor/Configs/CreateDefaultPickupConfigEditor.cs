#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using FlatOutLike.Data.Configs.Pickups;

namespace FlatOutLike.Editor.Configs
{
    /// <summary>
    /// Создаёт или обновляет Assets/Development/Data/Configs/Pickups/PickupConfig.asset.
    ///
    /// Значения по умолчанию:
    ///   _repairFraction    = 0.4   (40% восстановления HP)
    ///   _nitroFillFraction = 1.0   (полная зарядка нитро)
    ///   _respawnInterval   = 8     (секунды до повторного появления)
    ///   _entries[2]:
    ///     [0] type=Repair  (enumValueIndex=0), prefab=Pickup_Repair, poolSize=2
    ///     [1] type=Nitro   (enumValueIndex=1), prefab=Pickup_Nitro,  poolSize=2
    ///
    /// Идемпотентность: если ассет уже существует — поля перезаписываются, GUID сохраняется.
    ///
    /// Запускать ПОСЛЕ:
    ///   FlatOutLike/Setup/Build Pickup Prefabs  (нужны Pickup_Repair/Nitro.prefab)
    /// Запускать ДО:
    ///   FlatOutLike/Configs/Create Default DerbyMatchConfig  (проводит _pickupConfig в Scope)
    /// </summary>
    public static class CreateDefaultPickupConfigEditor
    {
        private const string AssetPath      = "Assets/Development/Data/Configs/Pickups/PickupConfig.asset";
        private const string FolderConfigs  = "Assets/Development/Data/Configs";
        private const string FolderPickups  = "Assets/Development/Data/Configs/Pickups";

        private const string RepairPrefabPath = "Assets/Development/Prefabs/Pickups/Pickup_Repair.prefab";
        private const string NitroPrefabPath  = "Assets/Development/Prefabs/Pickups/Pickup_Nitro.prefab";

        [MenuItem("FlatOutLike/Configs/Create Default PickupConfig")]
        public static void CreateDefaultPickupConfig()
        {
            Undo.SetCurrentGroupName("Create Default PickupConfig");

            // ── Папка ────────────────────────────────────────────────────────
            EnsureFolder("Assets/Development/Data", "Configs");
            EnsureFolder(FolderConfigs, "Pickups");

            // ── Создаём или загружаем ассет (идемпотентно по пути) ───────────
            var config = AssetDatabase.LoadAssetAtPath<PickupConfig>(AssetPath);
            bool isNew = config == null;

            if (isNew)
            {
                config = ScriptableObject.CreateInstance<PickupConfig>();
                AssetDatabase.CreateAsset(config, AssetPath);
                AssetDatabase.SaveAssets();
                Undo.RegisterCreatedObjectUndo(config, "Create PickupConfig");
                Debug.Log("[Editor] Создан PickupConfig.asset: " + AssetPath);
            }
            else
            {
                Debug.Log("[Editor] PickupConfig.asset уже существует — обновляем поля.");
            }

            // ── Загружаем ссылки на префабы ─────────────────────────────────
            var repairPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RepairPrefabPath);
            if (repairPrefab == null)
                Debug.LogWarning("[Editor] Pickup_Repair.prefab не найден по " + RepairPrefabPath +
                                 ". Запусти 'FlatOutLike/Setup/Build Pickup Prefabs' сначала.");

            var nitroPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(NitroPrefabPath);
            if (nitroPrefab == null)
                Debug.LogWarning("[Editor] Pickup_Nitro.prefab не найден по " + NitroPrefabPath +
                                 ". Запусти 'FlatOutLike/Setup/Build Pickup Prefabs' сначала.");

            // ── Заполняем поля через SerializedObject ─────────────────────────
            var so = new SerializedObject(config);

            so.FindProperty("_repairFraction").floatValue    = 0.4f;
            so.FindProperty("_nitroFillFraction").floatValue = 1.0f;
            // _respawnInterval: если поле float — floatValue корректно;
            // если int — замени floatValue на intValue (Unity не конвертирует автоматически)
            so.FindProperty("_respawnInterval").floatValue   = 8f;

            // _entries: массив PickupEntry { PickupType type; GameObject prefab; int poolSize }
            var entriesProp = so.FindProperty("_entries");
            entriesProp.arraySize = 2;

            // entry[0] — Repair
            var e0 = entriesProp.GetArrayElementAtIndex(0);
            e0.FindPropertyRelative("type").enumValueIndex         = 0; // PickupType.Repair
            e0.FindPropertyRelative("prefab").objectReferenceValue = repairPrefab;
            e0.FindPropertyRelative("poolSize").intValue           = 2;

            // entry[1] — Nitro
            var e1 = entriesProp.GetArrayElementAtIndex(1);
            e1.FindPropertyRelative("type").enumValueIndex         = 1; // PickupType.Nitro
            e1.FindPropertyRelative("prefab").objectReferenceValue = nitroPrefab;
            e1.FindPropertyRelative("poolSize").intValue           = 2;

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();

            Debug.Log("[Editor] PickupConfig заполнен: " +
                      "_repairFraction=0.4, _nitroFillFraction=1.0, _respawnInterval=8, " +
                      "_entries[2]: Repair=" + (repairPrefab != null ? repairPrefab.name : "NULL") +
                      " Nitro=" + (nitroPrefab != null ? nitroPrefab.name : "NULL") +
                      " poolSize=2.");
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
