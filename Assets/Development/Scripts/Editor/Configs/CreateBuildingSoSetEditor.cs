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
        // Публично: плита стадий (Build Hub Stage Prefabs) и проверка «заглушка ли это» берут путь отсюда.
        public const string ConcreteFloorPrefab =
            "Assets/ThirdParty/Synty/PolygonConstruction/Prefabs/Buildings/SM_Bld_Concrete_Floor_01.prefab";

        // Шаг 12b: финальные визуалы (Synty / Battle Stadium) и префабы промежуточных стадий.
        private const string TicketBoothFinalPrefab =
            "Assets/ThirdParty/Synty/PolygonConstruction/Prefabs/Buildings/SM_Bld_Portable_Office_01.prefab";
        private const string TribunesFinalPrefab =
            "Assets/ThirdParty/Battle_Stadium/Prefabs/Stadium/stand_001.prefab";
        private const string HubStagePrefabsRoot = "Assets/Development/Prefabs/Hub/";

        /// <summary>Пути стадий и финала достраиваемого здания. Единственное место путей (читает и Build Hub Stage Prefabs).</summary>
        public readonly struct HubStagePaths
        {
            public readonly BuildingId Id;
            public readonly string Stage1;
            public readonly string Stage2;
            public readonly string Final;
            // true — VisualPrefab заменяется на Final, если он пуст или равен заглушке ConcreteFloorPrefab.
            public readonly bool ReplaceStubVisual;

            public HubStagePaths(BuildingId id, string stage1, string stage2, string final, bool replaceStubVisual)
            {
                Id = id;
                Stage1 = stage1;
                Stage2 = stage2;
                Final = final;
                ReplaceStubVisual = replaceStubVisual;
            }
        }

        public static readonly HubStagePaths[] HubStagePathsTable =
        {
            new HubStagePaths(BuildingId.HighJumpTower,
                HubStagePrefabsRoot + "HighJumpTower/HighJumpTower_Stage1.prefab",
                HubStagePrefabsRoot + "HighJumpTower/HighJumpTower_Stage2.prefab",
                HighRampPrefab, false),
            new HubStagePaths(BuildingId.TicketBooth,
                HubStagePrefabsRoot + "TicketBooth/TicketBooth_Stage1.prefab",
                HubStagePrefabsRoot + "TicketBooth/TicketBooth_Stage2.prefab",
                TicketBoothFinalPrefab, true),
            new HubStagePaths(BuildingId.Tribunes,
                HubStagePrefabsRoot + "Tribunes/Tribunes_Stage1.prefab",
                HubStagePrefabsRoot + "Tribunes/Tribunes_Stage2.prefab",
                TribunesFinalPrefab, true),
        };

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
            new BuildingDef(BuildingId.TicketBooth,   new Vector3(-15f, 0f, -8f),  90f, true,  TicketBoothFinalPrefab),
            new BuildingDef(BuildingId.Tribunes,      new Vector3(15f, 0f, -8f),  -90f, true,  TribunesFinalPrefab),
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
            new PresentationDef(BuildingId.Tribunes,      "TRIBUNES",     12f), // stand_001 — сектор стадиона: при 7 м финал ниже каркаса (решение 2026-10-22)
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
            // Шаг 12a: стадии стройки (цены/доход) и зоны вложения — одноразово по флагу.
            MigrateBuildStages();
            // Шаг 12b: префабы промежуточных стадий и финальные визуалы TicketBooth/Tribunes.
            MigrateStagePrefabs();

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
                // 12a: LaunchZoneOffset/LaunchZoneSize переименованы в ZoneOffset/ZoneSize (FormerlySerializedAs).
                var offset  = entry.FindPropertyRelative("ZoneOffset");
                var size    = entry.FindPropertyRelative("ZoneSize");
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

        // ── Шаг 12a: стадии стройки ─────────────────────────────────────────────

        private const string StagesFlagField = "_stagesDefaultsApplied";

        private readonly struct StageDef
        {
            public readonly BuildingId Id;
            public readonly int[] Costs;
            public readonly int[] Incomes;

            public StageDef(BuildingId id, int[] costs, int[] incomes)
            {
                Id = id;
                Costs = costs;
                Incomes = incomes;
            }
        }

        // Дерби и Гараж построены со старта (Stages пустые), заглушки трюков отключены (Stages пустые) —
        // здесь только достраиваемые здания.
        private static readonly StageDef[] StageDefaults =
        {
            new StageDef(BuildingId.HighJumpTower, new[] { 25, 40, 60 },    new[] { 0, 0, 0 }),
            new StageDef(BuildingId.TicketBooth,   new[] { 60, 100, 140 },  new[] { 5, 8, 12 }),
            new StageDef(BuildingId.Tribunes,      new[] { 100, 150, 200 }, new[] { 8, 12, 18 }),
        };

        // Enabled-здания без зоны запуска, но с зоной вложения: ей тоже нужны ZoneOffset/ZoneSize.
        private static readonly BuildingId[] InvestZoneDefaults =
        {
            BuildingId.TicketBooth,
            BuildingId.Tribunes,
        };

        /// <summary>
        /// Одноразовая миграция каталога (шаг 12a) по флагу _stagesDefaultsApplied: заливает Stages
        /// (Cost/IncomePerMinute, StagePrefab = null) для HighJumpTower/TicketBooth/Tribunes и заполняет
        /// зону (ZoneOffset/ZoneSize) у Enabled-зданий без зоны запуска, если она не задана. После прохода
        /// флаг = true — значения дизайнера дальше не трогаются. Поля не найдены (рантайм не скомпилирован) —
        /// ничего не пишем и флаг не ставим. Идемпотентно; публично — для Build Base Hub Layout.
        /// </summary>
        public static void MigrateBuildStages()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<BuildingCatalogConfig>(CatalogPath);
            if (catalog == null)
            {
                Debug.LogWarning("[Editor] MigrateBuildStages: каталог не найден: " + CatalogPath);
                return;
            }

            var so = new SerializedObject(catalog);
            var entries = so.FindProperty("_entries");
            var flag = so.FindProperty(StagesFlagField);
            if (entries == null || flag == null)
            {
                Debug.LogWarning("[Editor] BuildingCatalogConfig._entries/" + StagesFlagField +
                                 " не найдены (рантайм 12a не скомпилирован?) — миграция стадий пропущена.");
                return;
            }

            if (flag.boolValue)
            {
                Debug.Log("[Editor] MigrateBuildStages: стадии уже залиты (флаг) — не трогаем.");
                return;
            }

            // Стадии. Запись идёт только в SerializedObject; при любой нехватке полей выходим до Apply —
            // тогда ничего не сохраняется и флаг остаётся false.
            foreach (var def in StageDefaults)
            {
                SerializedProperty entry = FindEntry(entries, def.Id);
                if (entry == null)
                {
                    Debug.LogWarning("[Editor] MigrateBuildStages: entry " + def.Id + " не найден в каталоге.");
                    continue;
                }

                var stages = entry.FindPropertyRelative("Stages");
                if (stages == null)
                {
                    Debug.LogWarning("[Editor] MigrateBuildStages: у BuildingEntry нет поля Stages " +
                                     "(рантайм 12a не скомпилирован?) — миграция пропущена.");
                    return;
                }

                stages.arraySize = def.Costs.Length;
                for (int i = 0; i < def.Costs.Length; i++)
                {
                    // arraySize++ зануляет инициализаторы — заполняем КАЖДОЕ поле элемента явно.
                    var stage = stages.GetArrayElementAtIndex(i);
                    var cost = stage.FindPropertyRelative("Cost");
                    var prefab = stage.FindPropertyRelative("StagePrefab");
                    var income = stage.FindPropertyRelative("IncomePerMinute");
                    if (cost == null || prefab == null || income == null)
                    {
                        Debug.LogWarning("[Editor] MigrateBuildStages: у BuildStageEntry нет полей " +
                                         "Cost/StagePrefab/IncomePerMinute — миграция пропущена.");
                        return;
                    }

                    cost.intValue = def.Costs[i];
                    prefab.objectReferenceValue = null;
                    income.intValue = def.Incomes[i];
                }

                Debug.Log("[Editor] " + def.Id + ": " + def.Costs.Length + " стадий стройки (цены " +
                          string.Join("/", def.Costs) + ", доход " + string.Join("/", def.Incomes) + " в мин).");
            }

            // Зоны вложения у зданий без зоны запуска.
            foreach (var id in InvestZoneDefaults)
            {
                SerializedProperty entry = FindEntry(entries, id);
                if (entry == null)
                {
                    Debug.LogWarning("[Editor] MigrateBuildStages: entry " + id + " не найден в каталоге.");
                    continue;
                }

                var offset = entry.FindPropertyRelative("ZoneOffset");
                var size = entry.FindPropertyRelative("ZoneSize");
                if (offset == null || size == null)
                {
                    Debug.LogWarning("[Editor] MigrateBuildStages: у BuildingEntry нет полей ZoneOffset/ZoneSize " +
                                     "(рантайм 12a не скомпилирован?) — миграция пропущена.");
                    return;
                }

                bool sizeEmpty = size.vector3Value.sqrMagnitude < 0.0001f;
                bool offsetEmpty = offset.vector3Value.sqrMagnitude < 0.0001f;
                if (!sizeEmpty && !offsetEmpty)
                {
                    Debug.Log("[Editor] " + id + ": зона уже задана — не трогаем.");
                    continue;
                }

                // Нулевой размер -> дефолт; нулевое смещение -> «вперёд» от визуала (как у зон запуска):
                // зона с центром в pivot здания лежала бы внутри модели.
                if (sizeEmpty)
                    size.vector3Value = LaunchZoneSizeDefault;
                if (offsetEmpty)
                    offset.vector3Value = new Vector3(0f, 0f, ComputeLaunchZoneForward(id));
                Debug.Log("[Editor] " + id + ": зона -> offset " + offset.vector3Value.ToString("F2") +
                          ", size " + size.vector3Value.ToString("F2") + ".");
            }

            flag.boolValue = true;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log("[Editor] MigrateBuildStages: стадии/зоны залиты, флаг " + StagesFlagField + " = true.");
        }

        // ── Шаг 12b: префабы промежуточных стадий и финалы ──────────────────────

        /// <summary>
        /// Миграция каталога без затирания ручного выбора:
        ///   • Stages[0].StagePrefab = Stage1, Stages[1].StagePrefab = Stage2 — только если поле пустое
        ///     (Stages[2] не трогаем: финальная стадия = VisualPrefab);
        ///   • VisualPrefab TicketBooth/Tribunes -> финальный визуал — только если он пуст или равен заглушке
        ///     ConcreteFloorPrefab.
        /// Префабы грузятся через LoadAssetAtPath; не найден — LogWarning (сначала Build Hub Stage Prefabs),
        /// поле не трогаем. Идемпотентно; публично — чтобы Build Base Hub Layout мог гарантировать миграцию.
        /// </summary>
        public static void MigrateStagePrefabs()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<BuildingCatalogConfig>(CatalogPath);
            if (catalog == null)
            {
                Debug.LogWarning("[Editor] MigrateStagePrefabs: каталог не найден: " + CatalogPath);
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
            foreach (var def in HubStagePathsTable)
            {
                SerializedProperty entry = FindEntry(entries, def.Id);
                if (entry == null)
                {
                    Debug.LogWarning("[Editor] MigrateStagePrefabs: entry " + def.Id + " не найден в каталоге.");
                    continue;
                }

                // Префабы стадий 1 и 2 -> Stages[0] и Stages[1].
                var stages = entry.FindPropertyRelative("Stages");
                if (stages == null)
                {
                    // break, а не return: правки предыдущих зданий применятся общим ApplyModifiedProperties ниже.
                    Debug.LogWarning("[Editor] MigrateStagePrefabs: у BuildingEntry нет поля Stages (рантайм 12a не скомпилирован?).");
                    break;
                }

                if (stages.arraySize < 2)
                {
                    Debug.LogWarning("[Editor] " + def.Id + ": в каталоге меньше 2 стадий (" + stages.arraySize +
                                     ") — префабы стадий не прошиты (проверь MigrateBuildStages).");
                }
                else
                {
                    changed |= AssignStagePrefab(stages, 0, def.Stage1, def.Id);
                    changed |= AssignStagePrefab(stages, 1, def.Stage2, def.Id);
                }

                // Финальный визуал вместо заглушки-плиты.
                if (def.ReplaceStubVisual)
                    changed |= ReplaceStubVisual(entry, def);
            }

            if (changed)
            {
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(catalog);
                AssetDatabase.SaveAssets();
                Debug.Log("[Editor] MigrateStagePrefabs: каталог обновлён.");
            }
            else
            {
                Debug.Log("[Editor] MigrateStagePrefabs: изменений нет.");
            }
        }

        private static bool AssignStagePrefab(SerializedProperty stages, int index, string path, BuildingId id)
        {
            var prefabProp = stages.GetArrayElementAtIndex(index).FindPropertyRelative("StagePrefab");
            if (prefabProp == null)
            {
                Debug.LogWarning("[Editor] У BuildStageEntry нет поля StagePrefab.");
                return false;
            }

            if (prefabProp.objectReferenceValue != null)
            {
                Debug.Log("[Editor] " + id + ": Stages[" + index + "].StagePrefab уже задан — не трогаем.");
                return false;
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogWarning("[Editor] " + id + ": префаб стадии не найден: " + path +
                                 " — сначала FlatOutLike/Prefabs/Build Hub Stage Prefabs. Поле не тронуто.");
                return false;
            }

            prefabProp.objectReferenceValue = prefab;
            Debug.Log("[Editor] " + id + ": Stages[" + index + "].StagePrefab = " + prefab.name + ".");
            return true;
        }

        private static bool ReplaceStubVisual(SerializedProperty entry, HubStagePaths def)
        {
            var visualProp = entry.FindPropertyRelative("VisualPrefab");
            if (visualProp == null)
            {
                Debug.LogWarning("[Editor] BuildingEntry.VisualPrefab не найдено.");
                return false;
            }

            var current = visualProp.objectReferenceValue;
            bool isEmpty = current == null;
            bool isStub = !isEmpty && AssetDatabase.GetAssetPath(current) == ConcreteFloorPrefab;
            if (!isEmpty && !isStub)
            {
                Debug.Log("[Editor] " + def.Id + ": VisualPrefab выбран вручную (" + current.name + ") — не трогаем.");
                return false;
            }

            var final = AssetDatabase.LoadAssetAtPath<GameObject>(def.Final);
            if (final == null)
            {
                Debug.LogWarning("[Editor] " + def.Id + ": финальный визуал не найден: " + def.Final + " — VisualPrefab не тронут.");
                return false;
            }

            visualProp.objectReferenceValue = final;
            Debug.Log("[Editor] " + def.Id + ": VisualPrefab = " + final.name + " (вместо " + (isEmpty ? "пусто" : "заглушки") + ").");
            return true;
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
