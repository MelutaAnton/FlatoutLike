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

        // Компактная раскладка (после плейтеста 2026-10-07): площадки в радиусе ~14-18 м от спавна.
        private const float StubRowZ = -18f;

        private static readonly BuildingDef[] Defaults =
        {
            new BuildingDef(BuildingId.DerbyArena,    new Vector3(0f, 0f, 14f),   180f, true,  OfficeRoundPrefab),
            new BuildingDef(BuildingId.Garage,        new Vector3(-15f, 0f, 4f),   90f, true,  OfficeOldSmallPrefab),
            new BuildingDef(BuildingId.HighJumpTower, new Vector3(15f, 0f, 4f),   -90f, true,  HighRampPrefab),
            new BuildingDef(BuildingId.TicketBooth,   new Vector3(-15f, 0f, -8f),  90f, true,  ConcreteFloorPrefab),
            new BuildingDef(BuildingId.Tribunes,      new Vector3(15f, 0f, -8f),  -90f, true,  ConcreteFloorPrefab),
            new BuildingDef(BuildingId.LongJump,      new Vector3(-16f, 0f, StubRowZ), 0f, false, ConcreteFloorPrefab),
            new BuildingDef(BuildingId.FireRings,     new Vector3(-8f, 0f, StubRowZ),  0f, false, ConcreteFloorPrefab),
            new BuildingDef(BuildingId.Bowling,       new Vector3(0f, 0f, StubRowZ),   0f, false, ConcreteFloorPrefab),
            new BuildingDef(BuildingId.FieldGoal,     new Vector3(8f, 0f, StubRowZ),   0f, false, ConcreteFloorPrefab),
            new BuildingDef(BuildingId.HighCard,      new Vector3(16f, 0f, StubRowZ),  0f, false, ConcreteFloorPrefab),
        };

        // Единая таблица «подпись площадки + целевой габарит визуала» (читает MigrateBuildingPresentation).
        private readonly struct PresentationDef
        {
            public readonly BuildingId Id;
            public readonly string DisplayName;
            public readonly float VisualFootprint;

            public PresentationDef(BuildingId id, string displayName, float visualFootprint)
            {
                Id = id;
                DisplayName = displayName;
                VisualFootprint = visualFootprint;
            }
        }

        private static readonly PresentationDef[] PresentationDefaults =
        {
            new PresentationDef(BuildingId.DerbyArena,    "DERBY",        10f),
            new PresentationDef(BuildingId.Garage,        "GARAGE",        8f),
            new PresentationDef(BuildingId.HighJumpTower, "HIGH JUMP",     8f),
            new PresentationDef(BuildingId.TicketBooth,   "TICKET BOOTH",  7f),
            new PresentationDef(BuildingId.Tribunes,      "TRIBUNES",      7f),
            new PresentationDef(BuildingId.LongJump,      "LONG JUMP",     6f),
            new PresentationDef(BuildingId.FireRings,     "FIRE RINGS",    6f),
            new PresentationDef(BuildingId.Bowling,       "BOWLING",       6f),
            new PresentationDef(BuildingId.FieldGoal,     "FIELD GOAL",    6f),
            new PresentationDef(BuildingId.HighCard,      "HIGH CARD",     6f),
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

            // Шаг 11b: зоны запуска — и для вновь созданного каталога, и миграцией существующего.
            MigrateLaunchZones();
            // Подписи площадок и целевые габариты визуала — тем же способом.
            MigrateBuildingPresentation();

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

            // Дефолт подписи «COMING SOON» живёт в инициализаторе SO — здесь второго значения не подставляем.
            var label = so.FindProperty("_comingSoonLabel");
            if (label != null && string.IsNullOrEmpty(label.stringValue))
                Debug.LogWarning("[Editor] BuildingCatalogConfig._comingSoonLabel пуст — подпись заглушек будет пустой " +
                                 "(задай значение в ассете).");

            // Подписи площадок и габариты — сразу при создании; затем флаг одноразовой миграции = true,
            // чтобы MigrateBuildingPresentation не трогал осознанно очищенные значения.
            FillPresentation(entries, out _);
            SetPresentationAppliedFlag(so);

            so.ApplyModifiedProperties();
            Debug.Log("[Editor] BuildingCatalogConfig_Default заполнен: " + Defaults.Length + " зданий, spawn (0,0,0), " +
                      "подписи/габариты залиты, флаг _presentationDefaultsApplied = true.");
        }

        private const string PresentationFlagField = "_presentationDefaultsApplied";

        private static void SetPresentationAppliedFlag(SerializedObject so)
        {
            var flag = so.FindProperty(PresentationFlagField);
            if (flag == null)
            {
                Debug.LogWarning("[Editor] BuildingCatalogConfig." + PresentationFlagField +
                                 " не найдено (рантайм не скомпилирован?) — миграция подписей будет повторяться.");
                return;
            }
            flag.boolValue = true;
        }

        /// <summary>
        /// Заливает DisplayName/VisualFootprint из PresentationDefaults в entry, где они ещё пусты (&lt;= 0).
        /// Возвращает false, если у BuildingEntry нет нужных полей (рантайм не скомпилирован).
        /// </summary>
        private static bool FillPresentation(SerializedProperty entries, out bool changed)
        {
            changed = false;
            foreach (var def in PresentationDefaults)
            {
                SerializedProperty entry = FindEntry(entries, def.Id);
                if (entry == null)
                {
                    Debug.LogWarning("[Editor] Presentation: entry " + def.Id + " не найден в каталоге.");
                    continue;
                }

                var nameProp = entry.FindPropertyRelative("DisplayName");
                var footprintProp = entry.FindPropertyRelative("VisualFootprint");
                if (nameProp == null || footprintProp == null)
                {
                    Debug.LogWarning("[Editor] Presentation: у BuildingEntry нет полей " +
                                     "DisplayName/VisualFootprint (рантайм не скомпилирован?).");
                    return false;
                }

                if (string.IsNullOrEmpty(nameProp.stringValue))
                {
                    nameProp.stringValue = def.DisplayName;
                    changed = true;
                    Debug.Log("[Editor] " + def.Id + ": DisplayName = '" + def.DisplayName + "'.");
                }

                if (footprintProp.floatValue <= 0f)
                {
                    footprintProp.floatValue = def.VisualFootprint;
                    changed = true;
                    Debug.Log("[Editor] " + def.Id + ": VisualFootprint = " + def.VisualFootprint + " м.");
                }
            }
            return true;
        }

        // ── Шаг 11b: зоны запуска режимов ───────────────────────────────────────

        private readonly struct LaunchDef
        {
            public readonly BuildingId Id;
            public readonly GameState State;

            public LaunchDef(BuildingId id, GameState state)
            {
                Id = id;
                State = state;
            }
        }

        // Здания расставлены лицом к центру хаба (YRotation ведёт локальную +Z к (0,0,0):
        // DerbyArena 180° → +Z = мировая -Z, Garage 90° → +X, HighJumpTower -90° → -X),
        // поэтому «перед зданием в сторону центра» = локальный (0,0,forward).
        // Смещение зоны считается от ЦЕНТРА корня здания (визуал центрируется на pivot), поэтому оно
        // обязано выходить за полугабарит визуала: footprint/2 + половина глубины зоны + зазор.
        private const float ZoneClearance = 0.5f;
        private static readonly Vector3 LaunchZoneSizeDefault = new Vector3(6f, 3f, 6f);

        /// <summary>Смещение зоны вперёд по локальной +Z: VisualFootprint/2 + LaunchZoneSize.z/2 + ZoneClearance.</summary>
        private static float ComputeLaunchZoneForward(BuildingId id)
        {
            float footprint = 0f;
            foreach (var p in PresentationDefaults)
            {
                if (p.Id == id)
                {
                    footprint = p.VisualFootprint;
                    break;
                }
            }

            if (footprint <= 0f)
                Debug.LogWarning("[Editor] " + id + ": в PresentationDefaults нет габарита — смещение зоны считается без него.");

            return footprint * 0.5f + LaunchZoneSizeDefault.z * 0.5f + ZoneClearance;
        }

        private static readonly LaunchDef[] LaunchDefaults =
        {
            new LaunchDef(BuildingId.DerbyArena,    GameState.DerbyMatch),
            new LaunchDef(BuildingId.Garage,        GameState.GarageMeta),
            new LaunchDef(BuildingId.HighJumpTower, GameState.StuntsHighJumpDriving),
        };

        /// <summary>
        /// Миграция каталога без затирания тюнинга: зона запуска выставляется ТОЛЬКО у entry,
        /// которая ещё не инициализирована (HasLaunchZone == false и LaunchState == BaseHub (0) —
        /// для зоны запуска «возврат в хаб» бессмысленен). Уже настроенные entry не трогаются.
        /// Идемпотентно; публично — чтобы Build Base Hub Layout мог гарантировать миграцию.
        /// </summary>
        public static void MigrateLaunchZones()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<BuildingCatalogConfig>(CatalogPath);
            if (catalog == null)
            {
                Debug.LogWarning("[Editor] MigrateLaunchZones: каталог не найден: " + CatalogPath);
                return;
            }

            var so = new SerializedObject(catalog);
            var entries = so.FindProperty("_entries");
            if (entries == null)
            {
                Debug.LogWarning("[Editor] BuildingCatalogConfig._entries не найдено (рантайм не скомпилирован?).");
                return;
            }

            bool changed = false;
            foreach (var def in LaunchDefaults)
            {
                SerializedProperty entry = FindEntry(entries, def.Id);
                if (entry == null)
                {
                    Debug.LogWarning("[Editor] MigrateLaunchZones: entry " + def.Id + " не найден в каталоге.");
                    continue;
                }

                var hasZone = entry.FindPropertyRelative("HasLaunchZone");
                var state   = entry.FindPropertyRelative("LaunchState");
                var offset  = entry.FindPropertyRelative("LaunchZoneOffset");
                var size    = entry.FindPropertyRelative("LaunchZoneSize");
                if (hasZone == null || state == null || offset == null || size == null)
                {
                    Debug.LogWarning("[Editor] MigrateLaunchZones: у BuildingEntry нет полей зоны запуска " +
                                     "(рантайм 11b не скомпилирован?).");
                    return;
                }

                bool initialized = hasZone.boolValue || state.intValue != (int)GameState.BaseHub;
                if (initialized)
                {
                    Debug.Log("[Editor] " + def.Id + ": зона запуска уже настроена — не трогаем.");
                    continue;
                }

                hasZone.boolValue = true;
                state.intValue = (int)def.State; // GameState с явными значениями — только intValue
                float forward = ComputeLaunchZoneForward(def.Id);
                offset.vector3Value = new Vector3(0f, 0f, forward);
                size.vector3Value = LaunchZoneSizeDefault;
                changed = true;
                Debug.Log("[Editor] " + def.Id + ": зона запуска -> " + def.State + ", offset (0,0," +
                          forward.ToString("F2") + "), size " + LaunchZoneSizeDefault + ".");
            }

            if (changed)
            {
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(catalog);
            }
        }

        /// <summary>
        /// Одноразовая миграция каталога (доработка 11b по плейтесту): подписи площадок (DisplayName) и
        /// целевые габариты (VisualFootprint) заливаются ТОЛЬКО пока флаг _presentationDefaultsApplied == false;
        /// после прохода флаг = true. Дальше пустой DisplayName / VisualFootprint = 0 — осознанное «выключено»,
        /// миграция их не трогает. Внутри самого прохода уже заполненные значения тоже не затираются.
        /// Идемпотентно; публично — чтобы Build Base Hub Layout мог гарантировать миграцию.
        /// </summary>
        public static void MigrateBuildingPresentation()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<BuildingCatalogConfig>(CatalogPath);
            if (catalog == null)
            {
                Debug.LogWarning("[Editor] MigrateBuildingPresentation: каталог не найден: " + CatalogPath);
                return;
            }

            var so = new SerializedObject(catalog);
            var entries = so.FindProperty("_entries");
            var flag = so.FindProperty(PresentationFlagField);
            if (entries == null || flag == null)
            {
                Debug.LogWarning("[Editor] BuildingCatalogConfig._entries/" + PresentationFlagField +
                                 " не найдены (рантайм не скомпилирован?) — миграция подписей пропущена.");
                return;
            }

            if (flag.boolValue)
            {
                Debug.Log("[Editor] MigrateBuildingPresentation: подписи/габариты уже залиты (флаг) — не трогаем.");
                return;
            }

            if (!FillPresentation(entries, out _))
                return; // поля не найдены — флаг не ставим, повторим после компиляции

            flag.boolValue = true;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log("[Editor] MigrateBuildingPresentation: подписи/габариты залиты, флаг _presentationDefaultsApplied = true.");
        }

        private static SerializedProperty FindEntry(SerializedProperty entries, BuildingId id)
        {
            for (int i = 0; i < entries.arraySize; i++)
            {
                var e = entries.GetArrayElementAtIndex(i);
                var idProp = e.FindPropertyRelative("Id");
                if (idProp != null && idProp.intValue == (int)id)
                    return e;
            }
            return null;
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
