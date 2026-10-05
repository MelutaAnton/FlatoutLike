#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using FlatOutLike.Data.Configs.Camera;
using FlatOutLike.Data.Configs.Hub;
using FlatOutLike.Data.Configs.Progression;
using FlatOutLike.Models;

namespace FlatOutLike.Editor.Configs
{
    /// <summary>
    /// Шаг 11a: создаёт три SO-ассета BaseHub, если их ещё нет:
    ///   • BuildingCatalogConfig_Default — 10 зданий (расстановка + визуал-заглушки) и точка спавна персонажа;
    ///   • HubCharacterConfig_Default    — параметры движения персонажа (дефолты из кода);
    ///   • HubCameraConfig_Default       — параметры камеры хаба (дефолты из кода).
    ///
    /// Существующие ассеты НЕ перезаписываются (тюнинг пользователя) — лог «уже существует».
    /// Отсутствующий префаб-визуал → LogWarning, поле VisualPrefab остаётся пустым.
    /// Запускать ПОСЛЕ компиляции runtime-конфигов шага 11a.
    /// </summary>
    public static class CreateBuildingSoSetEditor
    {
        public const string CatalogPath =
            "Assets/Development/Data/Configs/Progression/BuildingCatalogConfig_Default.asset";
        public const string CharacterConfigPath =
            "Assets/Development/Data/Configs/Hub/HubCharacterConfig_Default.asset";
        public const string CameraConfigPath =
            "Assets/Development/Data/Configs/Camera/HubCameraConfig_Default.asset";

        // Временные визуалы (Synty) и префаб HighRamp — пути держим здесь, единственное место.
        private const string OfficeRoundPrefab =
            "Assets/ThirdParty/Synty/PolygonCity/Prefabs/Buildings/SM_Bld_OfficeRound_01.prefab";
        private const string OfficeOldSmallPrefab =
            "Assets/ThirdParty/Synty/PolygonCity/Prefabs/Buildings/SM_Bld_OfficeOld_Small_01.prefab";
        private const string HighRampPrefab =
            "Assets/Development/Prefabs/Environment/HighRamp.prefab";
        private const string ConcreteFloorPrefab =
            "Assets/ThirdParty/Synty/PolygonConstruction/Prefabs/Buildings/SM_Bld_Concrete_Floor_01.prefab";

        private const string DefaultComingSoonLabel = "Coming soon";

        private readonly struct BuildingDef
        {
            public readonly BuildingId Id;
            public readonly Vector3 Position;
            public readonly float YRotation;
            public readonly bool Enabled;
            public readonly string VisualPath;

            public BuildingDef(BuildingId id, Vector3 position, float yRotation, bool enabled, string visualPath)
            {
                Id = id;
                Position = position;
                YRotation = yRotation;
                Enabled = enabled;
                VisualPath = visualPath;
            }
        }

        private const float StubRowZ = -26f;

        private static readonly BuildingDef[] Defaults =
        {
            new BuildingDef(BuildingId.DerbyArena,    new Vector3(0f, 0f, 26f),  180f, true,  OfficeRoundPrefab),
            new BuildingDef(BuildingId.Garage,        new Vector3(-22f, 0f, 10f), 90f, true,  OfficeOldSmallPrefab),
            new BuildingDef(BuildingId.HighJumpTower, new Vector3(22f, 0f, 10f), -90f, true,  HighRampPrefab),
            new BuildingDef(BuildingId.TicketBooth,   new Vector3(-22f, 0f, -8f), 90f, true,  ConcreteFloorPrefab),
            new BuildingDef(BuildingId.Tribunes,      new Vector3(22f, 0f, -8f), -90f, true,  ConcreteFloorPrefab),
            new BuildingDef(BuildingId.LongJump,      new Vector3(-24f, 0f, StubRowZ), 0f, false, ConcreteFloorPrefab),
            new BuildingDef(BuildingId.FireRings,     new Vector3(-12f, 0f, StubRowZ), 0f, false, ConcreteFloorPrefab),
            new BuildingDef(BuildingId.Bowling,       new Vector3(0f, 0f, StubRowZ),   0f, false, ConcreteFloorPrefab),
            new BuildingDef(BuildingId.FieldGoal,     new Vector3(12f, 0f, StubRowZ),  0f, false, ConcreteFloorPrefab),
            new BuildingDef(BuildingId.HighCard,      new Vector3(24f, 0f, StubRowZ),  0f, false, ConcreteFloorPrefab),
        };

        [MenuItem("FlatOutLike/Configs/Create Building SO Set")]
        public static void CreateBuildingSoSet()
        {
            EnsureFolder("Assets/Development", "Data");
            EnsureFolder("Assets/Development/Data", "Configs");
            EnsureFolder("Assets/Development/Data/Configs", "Progression");
            EnsureFolder("Assets/Development/Data/Configs", "Hub");
            EnsureFolder("Assets/Development/Data/Configs", "Camera");

            CreateIfMissing<BuildingCatalogConfig>(CatalogPath, FillCatalog);
            CreateIfMissing<HubCharacterConfig>(CharacterConfigPath, null);
            CreateIfMissing<HubCameraConfig>(CameraConfigPath, null);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Editor] Create Building SO Set завершён.");
        }

        /// <summary>Создаёт ассет, только если его нет; существующий не трогает.</summary>
        private static void CreateIfMissing<T>(string path, System.Action<T> fill) where T : ScriptableObject
        {
            if (AssetDatabase.LoadAssetAtPath<T>(path) != null)
            {
                Debug.Log("[Editor] " + typeof(T).Name + " уже существует: " + path + " — не перезаписываем.");
                return;
            }

            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            // Без Undo.RegisterCreatedObjectUndo: на ассетах Undo некорректен (см. editor-files.md).
            if (fill != null)
                fill(asset);

            EditorUtility.SetDirty(asset);
            Debug.Log("[Editor] Создан " + typeof(T).Name + ": " + path);
        }

        private static void FillCatalog(BuildingCatalogConfig catalog)
        {
            var so = new SerializedObject(catalog);

            var entries = so.FindProperty("_entries");
            if (entries == null)
            {
                Debug.LogWarning("[Editor] BuildingCatalogConfig._entries не найдено (рантайм не скомпилирован?).");
                return;
            }

            entries.arraySize = Defaults.Length;
            for (int i = 0; i < Defaults.Length; i++)
            {
                var def = Defaults[i];
                var e = entries.GetArrayElementAtIndex(i);

                // enum — ЧИСЛОВОЕ значение (BuildingId объявлен с явными значениями).
                SetInt(e, "Id", (int)def.Id);
                SetVector3(e, "Position", def.Position);
                SetFloat(e, "YRotation", def.YRotation);
                SetBool(e, "Enabled", def.Enabled);

                var visualProp = e.FindPropertyRelative("VisualPrefab");
                var visual = AssetDatabase.LoadAssetAtPath<GameObject>(def.VisualPath);
                if (visualProp == null)
                {
                    Debug.LogWarning("[Editor] BuildingEntry.VisualPrefab не найдено.");
                }
                else if (visual != null)
                {
                    visualProp.objectReferenceValue = visual;
                }
                else
                {
                    visualProp.objectReferenceValue = null;
                    Debug.LogWarning("[Editor] Визуал для " + def.Id + " не найден: " + def.VisualPath +
                                     " — VisualPrefab оставлен пустым.");
                }
            }

            var spawnPos = so.FindProperty("_characterSpawnPosition");
            if (spawnPos != null) spawnPos.vector3Value = Vector3.zero;
            var spawnYaw = so.FindProperty("_characterSpawnYRotation");
            if (spawnYaw != null) spawnYaw.floatValue = 0f;

            var label = so.FindProperty("_comingSoonLabel");
            if (label != null && string.IsNullOrEmpty(label.stringValue))
                label.stringValue = DefaultComingSoonLabel;

            so.ApplyModifiedProperties();
            Debug.Log("[Editor] BuildingCatalogConfig_Default заполнен: " + Defaults.Length + " зданий, spawn (0,0,0).");
        }

        private static void SetInt(SerializedProperty parent, string name, int value)
        {
            var p = parent.FindPropertyRelative(name);
            if (p != null) p.intValue = value;
            else Debug.LogWarning("[Editor] BuildingEntry." + name + " не найдено.");
        }

        private static void SetFloat(SerializedProperty parent, string name, float value)
        {
            var p = parent.FindPropertyRelative(name);
            if (p != null) p.floatValue = value;
            else Debug.LogWarning("[Editor] BuildingEntry." + name + " не найдено.");
        }

        private static void SetBool(SerializedProperty parent, string name, bool value)
        {
            var p = parent.FindPropertyRelative(name);
            if (p != null) p.boolValue = value;
            else Debug.LogWarning("[Editor] BuildingEntry." + name + " не найдено.");
        }

        private static void SetVector3(SerializedProperty parent, string name, Vector3 value)
        {
            var p = parent.FindPropertyRelative(name);
            if (p != null) p.vector3Value = value;
            else Debug.LogWarning("[Editor] BuildingEntry." + name + " не найдено.");
        }

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
