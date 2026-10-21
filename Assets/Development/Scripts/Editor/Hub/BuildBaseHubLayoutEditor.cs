#if UNITY_EDITOR
using System.IO;
using TMPro;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using FlatOutLike.Models;
using FlatOutLike.Editor.Tools;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using FlatOutLike.Controllers.Camera;
using FlatOutLike.Controllers.Hub;
using FlatOutLike.Controllers.Mobile;
using FlatOutLike.Data.Configs.Camera;
using FlatOutLike.Data.Configs.Hub;
using FlatOutLike.Data.Configs.Input;
using FlatOutLike.Data.Configs.Progression;
using FlatOutLike.Data.Configs.Vfx;
using FlatOutLike.Editor.Configs;
using FlatOutLike.Editor.Prefabs;
using FlatOutLike.Editor.Scenes;
using FlatOutLike.Editor.Setup;
using FlatOutLike.Editor.UI;
using FlatOutLike.Installers;
using FlatOutLike.Views.UI.Hub;
using FlatOutLike.Views.UI.Overlay;
using Object = UnityEngine.Object;

namespace FlatOutLike.Editor.Hub
{
    /// <summary>
    /// Шаг 11a: собирает сцену BaseHub (Assets/Development/Scenes/BaseHub.unity).
    ///
    /// Состав сцены:
    ///   • Directional Light, Environment/Ground (Plane 60x60, материал Grass из Arcade Idle Engine);
    ///   • Buildings/&lt;BuildingId&gt; — корень на Position/YRotation из BuildingCatalogConfig (каталог —
    ///     единственный источник раскладки, синхронизируется каждый прогон), внутри инстанс VisualPrefab
    ///     (автомасштаб под VisualFootprint) и world-space TextMeshPro NameLabel (DisplayName;
    ///     для Enabled=false — ещё и «Coming soon»);
    ///   • HubCharacter (инстанс префаба в точке спавна из каталога);
    ///   • Main Camera (CinemachineBrain + единственный AudioListener) и CM_HubCamera
    ///     (CinemachineCamera + Follow + RotationComposer + CinemachineHubCameraBehaviour, Priority=Active);
    ///   • GameLifetimeScope + GameBootstrap, UICanvas + EventSystem;
    ///   • (11b/12a) Buildings/&lt;Id&gt;/Zone (триггер + kinematic Rigidbody + Marker; LaunchZoneController при
    ///     HasLaunchZone, InvestZoneController при StageCount &gt; 0) у каждого Enabled-здания с зоной;
    ///     (12a) Buildings/&lt;Id&gt;/Stages/Stage_0..Stage_N (пустырь -> заглушки -> финальный визуал),
    ///     BuildSiteController на корне, Panel (world-space BuildingPanelView) у достраиваемых зданий;
    ///     UICanvas/HubHud (CoinsPanel + PlayButton);
    ///     LaunchConfirmCanvas (world-space круг подтверждения запуска);
    ///   • connected-инстансы префабов MobileInputCanvas / PauseOverlayCanvas / AudioPlayer;
    ///   • прошивка полей GameLifetimeScope; Build Settings (Boot первой, BaseHub второй).
    ///
    /// Идемпотентность: повторный прогон = миграция без дублей (поиск по компонентам и именам,
    /// устаревший визуал здания заменяется при смене префаба в каталоге). Все ассеты читаются
    /// ПОСЛЕ OpenScene (OpenScene выгружает ассеты — managed-ссылка становится fake-null).
    ///
    /// Запускать ПОСЛЕ:
    ///   FlatOutLike/UI/Build Mobile Input Canvas  (в любой игровой сцене — чтобы в префабе был HubGroup/HubStick)
    /// Недостающие SO-ассеты и префаб персонажа скрипт доделывает сам
    /// (Create Building SO Set / Build Hub Character Prefab — только если их ещё нет).
    /// </summary>
    public static class BuildBaseHubLayoutEditor
    {
        private static readonly string ScenePath = ScenePaths.For(SceneKeys.BaseHub);
        private const string GrassMaterialPath = "Assets/ThirdParty/Arcade Idle Engine/Materials/Grass.mat";

        // ── Шаг 11b: зоны запуска, круг подтверждения, HUD ───────────────────
        private const string ArcadeTextures   = "Assets/ThirdParty/Arcade Idle Engine/Textures/";
        private const string CircleEmptyPath  = ArcadeTextures + "CircleEmpty.png";
        private const string CircleFilledPath = ArcadeTextures + "circle.png";
        private const string MoneyIconPath   = ArcadeTextures + "MoneyIcon.png";
        private const string PlayButtonSprite = UIBuildHelpers.UiPackRoot + "/Buttons/png/button1.png";

        private const string MarkerMaterialPath = "Assets/Development/Art/Materials/HubLaunchZoneMarker.mat";
        private const string UrpUnlitShader     = "Universal Render Pipeline/Unlit";

        private const string ZoneName          = "Zone";
        private const string LegacyZoneName    = "LaunchZone"; // имя зоны до 12a — переименовывается в Zone
        private const string MarkerName        = "Marker";
        private const float  MarkerHeight      = 0.02f; // над землёй, чтобы не было z-fighting

        // Цвет маркера зоны запуска на земле.
        private static readonly Color ZoneColor = new Color(1f, 0.85f, 0.2f, 0.9f);

        private const string ConfirmCanvasName = "LaunchConfirmCanvas";
        private const float  ConfirmCanvasPixels = 200f;  // сторона канваса в «пикселях»
        private const float  ConfirmCanvasScale  = 0.01f; // 200 px * 0.01 = круг ~2 м
        // Подложка — непрозрачное белое кольцо (контраст с травой), внутри — сплошной диск цвета
        // зоны, заполняемый по Radial360 (первый вариант 11b, выбран пользователем по плейтесту
        // 2026-10-07: полупрозрачное кольцо сливалось с маркером, зелёная заливка — отклонена).
        private static readonly Color ConfirmBackgroundColor = Color.white;
        private static readonly Color ConfirmFillColor = ZoneColor;
        private const float ConfirmFillInset = 30f; // отступ диска внутрь кольца, «пиксели» канваса

        private const string HubHudName      = "HubHud";
        private const string CoinsPanelName  = "CoinsPanel";
        private const string CoinsLabelName  = "CoinsLabel";
        private const string PlayButtonName  = "PlayButton";
        private static readonly Color CoinsPanelColor = new Color(0f, 0f, 0f, 0.45f);

        private const string LightName       = "Directional Light";
        private const string EnvironmentName = "Environment";
        private const string GroundName      = "Ground";
        private const string BuildingsName   = "Buildings";
        private const string LegacyLabelName = "ComingSoonLabel"; // старая метка 11a — мигрируется в NameLabel
        private const string NameLabelName   = "NameLabel";
        private const string HubCameraName   = "CM_HubCamera";

        // Plane = 10x10 м, масштаб 6 → земля 60x60 м.
        private const float GroundScale = 6f;

        // Подпись площадки (NameLabel): над габаритом визуала; повёрнута к камере хаба тем же способом,
        // что LaunchConfirmCanvas. При камере (0,20,-15)/FOV45 кегль 9 даёт читаемую подпись ~6 м шириной.
        private const float LabelMargin        = 1.5f; // отступ над верхом визуала, м
        private const float LabelDefaultHeight = 3f;   // высота, если у площадки нет визуала, м
        // Потолок высоты подписи над землёй (12b): над высоким HighRamp подпись уезжала за кадр top-down камеры.
        private const float MaxLabelHeight     = 6f;
        private const float LabelFallbackPitchDeg = 50f; // поворот, если HubCameraConfig не найден
        private const float LabelFontSize      = 9f;

        // ── Шаг 12a: стадии стройки и панель площадки ───────────────────────
        private const string StagesName        = "Stages";
        private const string StageNamePrefix   = "Stage_";
        private const string PlaceholderName   = "Placeholder";
        private const string PanelName         = "Panel";

        private const string StageMaterialsRoot   = "Assets/Development/Art/Materials/";
        private const string VacantMaterialPath   = StageMaterialsRoot + "HubStageVacant.mat";
        private const string PlaceholderMaterialPrefix = StageMaterialsRoot + "HubStagePlaceholder_";
        private const string UrpLitShader         = "Universal Render Pipeline/Lit";

        private const float  DefaultPlaceholderFootprint = 6f;  // габарит заглушки, если VisualFootprint = 0, м
        private const float  VacantHeight         = 0.1f;       // плоский пустырь, м
        private const float  PlaceholderHeightK   = 0.25f;      // высота заглушки стадии k из N = footprint * K * k / N
        private static readonly Color VacantColor = new Color(0.16f, 0.14f, 0.12f, 1f);
        // Оттенки серого заглушек по индексу стадии (k-1): чем выше стадия, тем светлее.
        private static readonly float[] PlaceholderShades = { 0.42f, 0.52f, 0.62f, 0.72f };

        private const float  PanelPixelsW      = 300f;  // размер world-space канваса панели («пиксели»)
        private const float  PanelPixelsH      = 120f;
        private const float  PanelScale        = 0.01f; // 300 px * 0.01 = 3 м шириной
        private const float  PanelAboveLabel   = 1.8f;  // подъём центра панели над центром подписи, м (фоллбэк без зоны)
        private const float  PanelHeightAboveZone = 2.5f; // высота центра панели над землёй в центре зоны здания, м
        private static readonly Color PanelTrackColor = new Color(0f, 0f, 0f, 0.6f);
        private static readonly Color PanelFillColor  = new Color(1f, 0.78f, 0.2f, 1f);
        private static readonly Color PanelRateColor  = new Color(0.55f, 1f, 0.55f, 1f);
        private const float  PanelFillInset    = 5f;
        private const string BuiltinSpritePath = "UI/Skin/UISprite.psd";

        [MenuItem("FlatOutLike/Hub/Build Base Hub Layout")]
        public static void BuildBaseHubLayout()
        {
            // Даём сохранить/выбросить правки текущей сцены ДО OpenScene(Single); Cancel = выход.
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log("[Editor] Build Base Hub Layout отменён пользователем (несохранённая сцена).");
                return;
            }

            EnsurePrerequisiteAssets();

            var scene = OpenOrCreateScene();

            // ── Ассеты — ТОЛЬКО после OpenScene ─────────────────────────────────
            var catalog = AssetDatabase.LoadAssetAtPath<BuildingCatalogConfig>(CreateBuildingSoSetEditor.CatalogPath);
            if (catalog == null)
            {
                Debug.LogError("[Editor] BuildingCatalogConfig_Default не найден: " +
                               CreateBuildingSoSetEditor.CatalogPath + " — сцена не собрана.");
                return;
            }

            Undo.SetCurrentGroupName("Build Base Hub Layout");
            int undoGroup = Undo.GetCurrentGroup();

            EnsureDirectionalLight();
            EnsureGround(scene);
            EnsureBuildings(scene, catalog);

            var hubCharacter = EnsureHubCharacter(scene, catalog);

            SetupCinemachineCameraRigEditor.EnsureBrain();
            EnsureHubCamera(hubCharacter);

            // Composition Root, UI-каркас.
            BuildMetaScenesEditor.CreateGameLifetimeScope();
            BuildMetaScenesEditor.CreateGameBootstrap();
            var uiCanvas = UIBuildHelpers.EnsureCanvasAndEventSystem();

            var hubHudView  = EnsureHubHud(uiCanvas);
            var confirmView = EnsureLaunchConfirmCanvas();

            var mobileCanvas = EnsureMobileInputCanvas(scene);
            var pauseView    = EnsurePauseOverlay(scene);

            WireScope(scene, hubCharacter, mobileCanvas, pauseView, hubHudView, confirmView);

            BuildMetaScenesEditor.RegisterAllScenesInBuildSettings();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Undo.CollapseUndoOperations(undoGroup);

            Debug.Log("[Editor] Build Base Hub Layout завершён, сцена сохранена: " + ScenePath);
        }

        // ── Подготовка ──────────────────────────────────────────────────────────

        /// <summary>Недостающие ассеты доделываем существующими меню (они ничего не перезаписывают).</summary>
        private static void EnsurePrerequisiteAssets()
        {
            bool setMissing =
                AssetDatabase.LoadAssetAtPath<BuildingCatalogConfig>(CreateBuildingSoSetEditor.CatalogPath) == null ||
                AssetDatabase.LoadAssetAtPath<HubCharacterConfig>(CreateBuildingSoSetEditor.CharacterConfigPath) == null ||
                AssetDatabase.LoadAssetAtPath<HubCameraConfig>(CreateBuildingSoSetEditor.CameraConfigPath) == null;
            if (setMissing)
            {
                Debug.Log("[Editor] Не все SO хаба существуют — запускаем Create Building SO Set.");
                CreateBuildingSoSetEditor.CreateBuildingSoSet();
            }

            if (AssetDatabase.LoadAssetAtPath<GameObject>(BuildHubCharacterPrefabEditor.PrefabPath) == null)
            {
                Debug.Log("[Editor] HubCharacter.prefab отсутствует — запускаем Build Hub Character Prefab.");
                BuildHubCharacterPrefabEditor.BuildHubCharacterPrefab();
            }

            // 11b: миграция каталога (зоны запуска у уже существующего ассета) и префаба мобильного ввода.
            // Оба вызова идемпотентны и не затирают тюнинг; выполняются ДО OpenScene, каталог дальше читается заново.
            CreateBuildingSoSetEditor.MigrateLaunchZones();
            CreateBuildingSoSetEditor.MigrateBuildingPresentation();
            // 12a: стадии стройки (цены/доход) и зоны вложения — одноразово по флагу в каталоге.
            CreateBuildingSoSetEditor.MigrateBuildStages();
            // 12b: префабы промежуточных стадий и финальные визуалы (не затирает ручной выбор).
            CreateBuildingSoSetEditor.MigrateStagePrefabs();
            AssetDatabase.SaveAssets();
            BuildMobileInputCanvasEditor.EnsureHubGroupInactiveInPrefab();
        }

        private static Scene OpenOrCreateScene()
        {
            if (File.Exists(Path.Combine(Directory.GetCurrentDirectory(), ScenePath)))
            {
                var existing = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                Debug.Log("[Editor] Открыта сцена " + ScenePath + ", дособираем недостающее.");
                return existing;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("[Editor] Создана новая сцена " + ScenePath + ".");
            return scene;
        }

        // ── Свет и земля ────────────────────────────────────────────────────────

        private static void EnsureDirectionalLight()
        {
            // Идемпотентность по КОМПОНЕНТУ: любой directional Light в сцене подходит.
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (l.type == LightType.Directional)
                {
                    Debug.Log("[Editor] Directional Light уже есть (" + l.name + ") — пропускаем.");
                    return;
                }
            }

            var go = new GameObject(LightName);
            Undo.RegisterCreatedObjectUndo(go, "Create Directional Light");
            var light = Undo.AddComponent<Light>(go);
            light.type = LightType.Directional;
            light.intensity = 1f;
            light.shadows = LightShadows.Soft;
            go.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            Debug.Log("[Editor] Создан Directional Light.");
        }

        private static void EnsureGround(Scene scene)
        {
            var env = GetOrCreateRoot(scene, EnvironmentName);

            GameObject ground = null;
            var groundT = env.transform.Find(GroundName);
            if (groundT != null && groundT.GetComponent<MeshFilter>() != null)
                ground = groundT.gameObject;

            if (ground == null)
            {
                ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
                ground.name = GroundName;
                Undo.RegisterCreatedObjectUndo(ground, "Create Ground");
                ground.transform.SetParent(env.transform, false);
                Debug.Log("[Editor] Создан Environment/Ground (Plane).");
            }
            else
            {
                Debug.Log("[Editor] Environment/Ground уже есть — обновляем масштаб/материал.");
            }

            Undo.RecordObject(ground.transform, "Setup Ground");
            ground.transform.localPosition = Vector3.zero;
            ground.transform.localRotation = Quaternion.identity;
            ground.transform.localScale = new Vector3(GroundScale, 1f, GroundScale);

            var grass = AssetDatabase.LoadAssetAtPath<Material>(GrassMaterialPath);
            var meshRenderer = ground.GetComponent<MeshRenderer>();
            if (grass == null)
            {
                Debug.LogWarning("[Editor] Материал Grass не найден: " + GrassMaterialPath + " — материал земли не тронут.");
            }
            else if (meshRenderer != null)
            {
                Undo.RecordObject(meshRenderer, "Assign Grass");
                meshRenderer.sharedMaterial = grass;
                Debug.Log("[Editor] Ground: материал Grass назначен (ссылкой).");
            }
        }

        // ── Здания ──────────────────────────────────────────────────────────────

        private static void EnsureBuildings(Scene scene, BuildingCatalogConfig catalog)
        {
            var root = GetOrCreateRoot(scene, BuildingsName);
            int processed = 0;

            // Материал маркера — один на все зоны, создаётся лениво (только если есть зоны).
            Material markerMaterial = null;

            // Поворот подписей к камере хаба — из конфига камеры, свежей ссылкой (после OpenScene).
            Quaternion labelRotation = Quaternion.Euler(LabelFallbackPitchDeg, 0f, 0f);
            bool hasViewRotation = TryGetHubViewRotation(out Quaternion viewRotation);
            if (hasViewRotation)
                labelRotation = viewRotation;
            else
                Debug.LogWarning("[Editor] HubCameraConfig не найден — подписи площадок повёрнуты по запасному наклону " +
                                 LabelFallbackPitchDeg + "°.");

            RemoveOrphanBuildings(root.transform, catalog);

            // Пустырь стадии 0 — общий для всех площадок (может быть пуст -> примитив).
            GameObject vacantPrefab = ReadVacantStagePrefab(catalog);

            foreach (var entry in catalog.Entries)
            {
                processed++;
                var go = FindOrCreateChild(root.transform, entry.Id.ToString());

                // Каталог — единственный источник раскладки: корень синхронизируется каждый прогон.
                Undo.RecordObject(go.transform, "Place Building");
                go.transform.localPosition = entry.Position;
                go.transform.localRotation = Quaternion.Euler(0f, entry.YRotation, 0f);
                go.transform.localScale = Vector3.one;

                // Стадии: Stages/Stage_0..Stage_N (визуал здания переезжает в последнюю стадию) +
                // BuildSiteController с прошитым списком стадий. Возвращает позицию подписи над контентом.
                var labelLocalPos = SyncStages(go, entry, vacantPrefab);
                SyncNameLabel(go, entry, catalog.ComingSoonLabel, labelLocalPos, labelRotation);

                if (HasZone(entry) && markerMaterial == null)
                    markerMaterial = EnsureMarkerMaterial();
                SyncZone(go, entry, markerMaterial);

                SyncPanel(go, entry, labelLocalPos, labelRotation);
            }

            Debug.Log("[Editor] Buildings: обработано зданий — " + processed + ".");

            CheckLaunchZoneOverlaps(catalog);
        }

        /// <summary>У Enabled-здания есть зона (запуска и/или вложения): ненулевой ZoneSize.</summary>
        private static bool HasZone(BuildingCatalogConfig.BuildingEntry entry) =>
            entry.Enabled && entry.ZoneSize.sqrMagnitude > 0.0001f;

        /// <summary>
        /// Проверка XZ-перекрытия зон всех Enabled-зданий (запуска и вложения). Перекрытия нежелательны:
        /// персонаж, стоя на стыке, может активировать соседнюю зону. Зона — прямоугольник
        /// ZoneSize (x/z) вокруг GetZoneWorldCenter(), повёрнутый на YRotation здания
        /// (триггер наследует поворот корня). Для ±90/270° габарит x/z меняется местами, для произвольного
        /// угла берётся AABB повёрнутого прямоугольника — консервативная оценка (возможны ложные Warning,
        /// пропусков нет). Только диагностика: каталог не правится.
        /// </summary>
        private static void CheckLaunchZoneOverlaps(BuildingCatalogConfig catalog)
        {
            const float Epsilon = 0.001f; // касание границами — не перекрытие

            var ids = new System.Collections.Generic.List<BuildingId>();
            var centers = new System.Collections.Generic.List<Vector2>();
            var halves = new System.Collections.Generic.List<Vector2>();

            foreach (var entry in catalog.Entries)
            {
                if (!HasZone(entry))
                    continue;

                Vector3 c = entry.GetZoneWorldCenter();
                float rad = entry.YRotation * Mathf.Deg2Rad;
                float cos = Mathf.Abs(Mathf.Cos(rad));
                float sin = Mathf.Abs(Mathf.Sin(rad));
                float hx = entry.ZoneSize.x * 0.5f;
                float hz = entry.ZoneSize.z * 0.5f;

                // AABB повёрнутого прямоугольника; при 90/270° даёт ровно перестановку (hx <-> hz).
                // Плюс радиус капсулы персонажа: триггер срабатывает при касании краем капсулы,
                // поэтому зазор меньше диаметра капсулы — тоже «стоит в двух зонах сразу».
                float pad = BuildHubCharacterPrefabEditor.CapsuleRadius;
                ids.Add(entry.Id);
                centers.Add(new Vector2(c.x, c.z));
                halves.Add(new Vector2(cos * hx + sin * hz + pad, sin * hx + cos * hz + pad));
            }

            int overlaps = 0;
            for (int i = 0; i < ids.Count; i++)
            {
                for (int j = i + 1; j < ids.Count; j++)
                {
                    bool overlapX = Mathf.Abs(centers[i].x - centers[j].x) < halves[i].x + halves[j].x - Epsilon;
                    bool overlapZ = Mathf.Abs(centers[i].y - centers[j].y) < halves[i].y + halves[j].y - Epsilon;
                    if (!overlapX || !overlapZ)
                        continue;

                    overlaps++;
                    Debug.LogWarning("[Editor] Зоны " + ids[i] + " и " + ids[j] +
                                     " перекрываются — раздвинь Position/ZoneOffset/ZoneSize в каталоге.");
                }
            }

            if (overlaps == 0)
                Debug.Log("[Editor] Зоны: перекрытий нет (проверено зон — " + ids.Count + ").");
        }

        /// <summary>
        /// Удаляет корни Buildings/&lt;Name&gt;, которым нет entry в каталоге: имя не парсится в BuildingId
        /// либо такого Id нет в каталоге (постройку убрали/переименовали). Каталог — единственный источник раскладки.
        /// </summary>
        private static void RemoveOrphanBuildings(Transform buildingsRoot, BuildingCatalogConfig catalog)
        {
            for (int i = buildingsRoot.childCount - 1; i >= 0; i--)
            {
                var child = buildingsRoot.GetChild(i).gameObject;

                // IsDefined + сверка имени отсекают числовые строки ("5"), которые Enum.TryParse принял бы за значение.
                bool known = System.Enum.TryParse(child.name, false, out BuildingId id)
                             && System.Enum.IsDefined(typeof(BuildingId), id)
                             && id.ToString() == child.name
                             && catalog.TryGetEntry(id, out _);
                if (known)
                    continue;

                Debug.Log("[Editor] Buildings/" + child.name + ": нет entry в каталоге — объект удалён.");
                Undo.DestroyObjectImmediate(child);
            }
        }

        // ── Стадии стройки (12a) ────────────────────────────────────────────────

        /// <summary>
        /// Buildings/&lt;Id&gt;/Stages/Stage_0..Stage_N (контейнеры с единичным трансформом, контент — ребёнок):
        ///   • !Enabled (заглушка): один Stage_0 = пустырь;
        ///   • Enabled, StageCount == 0 (построено со старта): один Stage_0 = визуал здания;
        ///   • Enabled, StageCount = N &gt; 0: Stage_0 = пустырь, Stage_k (1..N-1) = Stages[k-1].StagePrefab или
        ///     заглушка, Stage_N = Stages[N-1].StagePrefab, иначе визуал здания (существующий инстанс
        ///     VisualPrefab переезжает из корня здания — не дублируется).
        /// Включён только Stage_0 — остальные выключает/включает рантайм. Также прошивает BuildSiteController.
        /// Возвращает локальную позицию подписи (над контентом ПОСЛЕДНЕЙ стадии) — считается, пока все
        /// стадии ещё активны, чтобы bounds не зависели от activeSelf.
        /// </summary>
        private static Vector3 SyncStages(GameObject building, BuildingCatalogConfig.BuildingEntry entry,
            GameObject vacantPrefab)
        {
            var stagesGo = FindOrCreateChild(building.transform, StagesName);
            ResetLocal(stagesGo.transform);

            bool isStub = !entry.Enabled;
            int stageCount = isStub ? 0 : entry.StageCount;
            int total = stageCount > 0 ? stageCount + 1 : 1;

            float footprint = entry.VisualFootprint > 0f ? entry.VisualFootprint : DefaultPlaceholderFootprint;
            // Заглушки наследуют «твёрдость» текущего визуала: есть коллайдеры у VisualPrefab — оставляем BoxCollider.
            bool keepPlaceholderCollider = entry.VisualPrefab != null &&
                                           entry.VisualPrefab.GetComponentInChildren<Collider>(true) != null;

            var roots = new GameObject[total];
            GameObject lastContent = null;

            for (int i = 0; i < total; i++)
            {
                var container = FindOrCreateChild(stagesGo.transform, StageNamePrefix + i);
                ResetLocal(container.transform);
                if (!container.activeSelf)
                {
                    Undo.RecordObject(container, "Activate Stage While Building");
                    container.SetActive(true); // на время сборки — потом выставим итоговое состояние
                }
                roots[i] = container;

                GameObject content;
                if (stageCount == 0)
                {
                    content = isStub
                        ? SyncVacantContent(container.transform, building.transform, entry, vacantPrefab, footprint)
                        : SyncPrefabContent(container.transform, building.transform, entry, entry.VisualPrefab);
                }
                else if (i == 0)
                {
                    content = SyncVacantContent(container.transform, building.transform, entry, vacantPrefab, footprint);
                }
                else
                {
                    GameObject stagePrefab = GetStagePrefab(entry, i - 1);
                    if (stagePrefab == null && i == total - 1)
                        stagePrefab = entry.VisualPrefab; // финальная стадия без своего префаба = визуал здания

                    if (stagePrefab != null)
                    {
                        content = SyncPrefabContent(container.transform, building.transform, entry, stagePrefab);
                    }
                    else
                    {
                        float t = (float)i / stageCount;
                        float width = footprint * Mathf.Lerp(0.55f, 0.9f, t);
                        float height = footprint * PlaceholderHeightK * t;
                        int shade = Mathf.Clamp(i - 1, 0, PlaceholderShades.Length - 1);
                        var mat = EnsureStageMaterial(PlaceholderMaterialPrefix + shade + ".mat",
                            new Color(PlaceholderShades[shade], PlaceholderShades[shade], PlaceholderShades[shade], 1f));
                        content = SyncPlaceholderContent(container.transform, building.transform, entry,
                            new Vector3(width, height, width), mat, keepPlaceholderCollider);
                    }
                }

                if (content != null && PrefabUtility.IsAnyPrefabInstanceRoot(content))
                {
                    // 12b: промежуточные стадии со своим префабом (Stage_1..Stage_N-1) уже собраны в метрах под
                    // габарит — масштаб/сдвиг не применяем, а прежний FitVisual (если был) сбрасываем.
                    // Пустырь (Stage_0), финальная стадия и здания «построено со старта» — по-старому.
                    bool intermediatePrefab = stageCount > 0 && i >= 1 && i < total - 1;
                    if (intermediatePrefab)
                    {
                        ResetLocal(content.transform);
                        // Префаб стадии собран под вписанный след финала: если габарит финала с тех пор
                        // менялся (VisualFootprint / финальный префаб) — подсказать пересборку.
                        BuildHubStagePrefabsEditor.WarnIfStageStale(entry,
                            PrefabUtility.GetCorrespondingObjectFromSource(content));
                    }
                    else if (entry.VisualFootprint > 0f)
                        FitVisual(building, content, entry);
                }

                if (i == total - 1)
                    lastContent = content;
            }

            // Удаляем лишние Stage_* (стадий стало меньше / здание отключили).
            for (int i = stagesGo.transform.childCount - 1; i >= 0; i--)
            {
                var child = stagesGo.transform.GetChild(i).gameObject;
                bool expected = false;
                for (int k = 0; k < total; k++)
                {
                    if (child.name == StageNamePrefix + k)
                    {
                        expected = true;
                        break;
                    }
                }

                if (expected)
                    continue;
                Debug.Log("[Editor] " + entry.Id + ": лишняя стадия '" + child.name + "' удалена.");
                Undo.DestroyObjectImmediate(child);
            }

            // Устаревшие визуалы, оставшиеся прямыми детьми корня (смена префаба/раскладки стадий).
            RemoveLegacyVisuals(building.transform, entry);

            Vector3 labelPos = ComputeLabelLocalPosition(building, lastContent);

            // Итоговая активность: включён только Stage_0.
            for (int i = 0; i < total; i++)
            {
                bool want = i == 0;
                if (roots[i].activeSelf == want)
                    continue;
                Undo.RecordObject(roots[i], "Set Stage Active");
                roots[i].SetActive(want);
            }

            SyncBuildSite(building, entry, roots);
            Debug.Log("[Editor] " + entry.Id + ": стадий в сцене — " + total + (isStub ? " (заглушка)." : "."));
            return labelPos;
        }

        private static GameObject GetStagePrefab(BuildingCatalogConfig.BuildingEntry entry, int index)
        {
            if (entry.Stages == null || index < 0 || index >= entry.Stages.Length || entry.Stages[index] == null)
                return null;
            return entry.Stages[index].StagePrefab;
        }

        /// <summary>_vacantStagePrefab каталога (может быть пуст). Читается через SerializedObject, как прочие служебные поля.</summary>
        private static GameObject ReadVacantStagePrefab(BuildingCatalogConfig catalog)
        {
            var so = new SerializedObject(catalog);
            var prop = so.FindProperty("_vacantStagePrefab");
            if (prop == null)
            {
                Debug.LogWarning("[Editor] BuildingCatalogConfig._vacantStagePrefab не найдено (рантайм 12a не скомпилирован?) — " +
                                 "пустырь будет примитивом.");
                return null;
            }
            return prop.objectReferenceValue as GameObject;
        }

        private static GameObject SyncVacantContent(Transform container, Transform building,
            BuildingCatalogConfig.BuildingEntry entry, GameObject vacantPrefab, float footprint)
        {
            if (vacantPrefab != null)
                return SyncPrefabContent(container, building, entry, vacantPrefab);

            var mat = EnsureStageMaterial(VacantMaterialPath, VacantColor);
            // Плоский пустырь без коллайдера: персонаж не должен цепляться за низкую плиту.
            return SyncPlaceholderContent(container, building, entry,
                new Vector3(footprint, VacantHeight, footprint), mat, false);
        }

        /// <summary>
        /// Ровно один инстанс <paramref name="prefab"/> внутри контейнера стадии. Старый визуал, лежавший
        /// прямым ребёнком корня здания (до 12a), переезжает в контейнер, а не пересоздаётся.
        /// Всё остальное содержимое контейнера (заглушка, инстансы других префабов) удаляется.
        /// prefab == null — контейнер очищается, возвращает null.
        /// </summary>
        private static GameObject SyncPrefabContent(Transform container, Transform building,
            BuildingCatalogConfig.BuildingEntry entry, GameObject prefab)
        {
            GameObject found = null;
            for (int i = container.childCount - 1; i >= 0; i--)
            {
                var child = container.GetChild(i).gameObject;
                if (prefab != null && found == null && PrefabUtility.IsAnyPrefabInstanceRoot(child) &&
                    PrefabUtility.GetCorrespondingObjectFromSource(child) == prefab)
                {
                    found = child;
                    continue;
                }

                Debug.Log("[Editor] " + entry.Id + ": убран устаревший контент '" + child.name + "' из " + container.name + ".");
                Undo.DestroyObjectImmediate(child);
            }

            if (prefab == null)
            {
                Debug.Log("[Editor] " + entry.Id + ": префаб для " + container.name + " не задан — контент не создаётся.");
                return null;
            }

            if (found != null)
                return found;

            // Миграция: визуал из корня здания (раскладка 11a/11b) переезжает под контейнер.
            for (int i = building.childCount - 1; i >= 0; i--)
            {
                var child = building.GetChild(i).gameObject;
                if (!PrefabUtility.IsAnyPrefabInstanceRoot(child) ||
                    PrefabUtility.GetCorrespondingObjectFromSource(child) != prefab)
                    continue;

                Undo.SetTransformParent(child.transform, container, "Move Visual To Stage");
                Debug.Log("[Editor] " + entry.Id + ": визуал '" + child.name + "' перенесён в " + container.name + ".");
                return child;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, container);
            Undo.RegisterCreatedObjectUndo(instance, "Create Stage Content");
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            Debug.Log("[Editor] " + entry.Id + ": " + container.name + " <- '" + prefab.name + "'.");
            return instance;
        }

        /// <summary>
        /// Примитив-заглушка (Cube «Placeholder») внутри контейнера: низ на земле, размер size, наш материал.
        /// Коллайдер — BoxCollider при keepCollider, иначе убирается. Прочий контент контейнера удаляется.
        /// </summary>
        private static GameObject SyncPlaceholderContent(Transform container, Transform building,
            BuildingCatalogConfig.BuildingEntry entry, Vector3 size, Material material, bool keepCollider)
        {
            GameObject placeholder = null;
            for (int i = container.childCount - 1; i >= 0; i--)
            {
                var child = container.GetChild(i).gameObject;
                if (placeholder == null && child.name == PlaceholderName && child.GetComponent<MeshFilter>() != null)
                {
                    placeholder = child;
                    continue;
                }

                Debug.Log("[Editor] " + entry.Id + ": убран устаревший контент '" + child.name + "' из " + container.name + ".");
                Undo.DestroyObjectImmediate(child);
            }

            if (placeholder == null)
            {
                placeholder = GameObject.CreatePrimitive(PrimitiveType.Cube);
                placeholder.name = PlaceholderName;
                Undo.RegisterCreatedObjectUndo(placeholder, "Create Stage Placeholder");
                placeholder.transform.SetParent(container, false);
                Debug.Log("[Editor] " + entry.Id + ": " + container.name + " <- заглушка " + size.ToString("F2") + ".");
            }

            Undo.RecordObject(placeholder.transform, "Place Stage Placeholder");
            placeholder.transform.localPosition = new Vector3(0f, size.y * 0.5f, 0f);
            placeholder.transform.localRotation = Quaternion.identity;
            placeholder.transform.localScale = size;

            var col = placeholder.GetComponent<Collider>();
            if (keepCollider && col == null)
            {
                Undo.AddComponent<BoxCollider>(placeholder);
            }
            else if (!keepCollider && col != null)
            {
                Undo.DestroyObjectImmediate(col);
            }

            var mr = placeholder.GetComponent<MeshRenderer>();
            if (mr != null && material != null)
            {
                Undo.RecordObject(mr, "Setup Stage Placeholder");
                mr.sharedMaterial = material;
            }
            else if (material == null)
            {
                Debug.LogWarning("[Editor] " + entry.Id + ": материал заглушки не создан — " + container.name + " без материала.");
            }

            return placeholder;
        }

        /// <summary>Удаляет прямые префаб-инстансы корня здания (визуалы до 12a, не нашедшие места в стадиях).</summary>
        private static void RemoveLegacyVisuals(Transform building, BuildingCatalogConfig.BuildingEntry entry)
        {
            for (int i = building.childCount - 1; i >= 0; i--)
            {
                var child = building.GetChild(i).gameObject;
                if (!PrefabUtility.IsAnyPrefabInstanceRoot(child))
                    continue;

                Debug.Log("[Editor] " + entry.Id + ": убран устаревший визуал '" + child.name + "'.");
                Undo.DestroyObjectImmediate(child);
            }
        }

        /// <summary>Наш материал заглушки (URP Lit, Development/Art/Materials). Цвет задаётся только при создании.</summary>
        private static Material EnsureStageMaterial(string path, Color color)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null)
                return mat;

            var shader = Shader.Find(UrpLitShader);
            if (shader == null)
            {
                Debug.LogError("[Editor] Шейдер " + UrpLitShader + " не найден — материал " + path + " не создан.");
                return null;
            }

            mat = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.1f);
            AssetDatabase.CreateAsset(mat, path);
            Debug.Log("[Editor] Создан материал стадии: " + path);
            return mat;
        }

        private static void ResetLocal(Transform t)
        {
            if (t.localPosition == Vector3.zero && t.localRotation == Quaternion.identity && t.localScale == Vector3.one)
                return;
            Undo.RecordObject(t, "Reset Local Transform");
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one;
        }

        /// <summary>BuildSiteController на корне здания: _buildingId и _stageVisuals = Stage_0..Stage_N.</summary>
        private static void SyncBuildSite(GameObject building, BuildingCatalogConfig.BuildingEntry entry, GameObject[] stageRoots)
        {
            var site = building.GetComponent<BuildSiteController>();
            if (site == null)
            {
                site = Undo.AddComponent<BuildSiteController>(building);
                Debug.Log("[Editor] " + entry.Id + ": добавлен BuildSiteController.");
            }

            var so = new SerializedObject(site);
            var idProp = so.FindProperty("_buildingId");
            var stagesProp = so.FindProperty("_stageVisuals");
            if (idProp == null || stagesProp == null)
            {
                Debug.LogWarning("[Editor] BuildSiteController._buildingId/_stageVisuals не найдены " +
                                 "(рантайм 12a не скомпилирован?) — " + entry.Id + " не прошит.");
                return;
            }

            idProp.intValue = (int)entry.Id; // enum — числовое значение
            stagesProp.arraySize = stageRoots.Length;
            for (int i = 0; i < stageRoots.Length; i++)
                stagesProp.GetArrayElementAtIndex(i).objectReferenceValue = stageRoots[i]; // каждый элемент явно
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(site);
        }

        /// <summary>
        /// Автомасштаб визуала под entry.VisualFootprint (большая горизонтальная сторона, м) и посадка
        /// низа на y=0. Каждый прогон начинает со сброса scale/позиции, поэтому идемпотентно.
        /// </summary>
        private static void FitVisual(GameObject building, GameObject visual, BuildingCatalogConfig.BuildingEntry entry)
        {
            Undo.RecordObject(visual.transform, "Fit Building Visual");
            visual.transform.localScale = Vector3.one;
            visual.transform.localPosition = Vector3.zero;

            if (!TryGetLocalBounds(building.transform, visual, out Bounds original))
            {
                Debug.LogWarning("[Editor] " + entry.Id + ": у визуала '" + visual.name +
                                 "' нет подходящих Renderer'ов — автомасштаб пропущен.");
                return;
            }

            float maxSide = Mathf.Max(original.size.x, original.size.z);
            if (maxSide < 0.0001f)
            {
                Debug.LogWarning("[Editor] " + entry.Id + ": нулевой горизонтальный габарит визуала — автомасштаб пропущен.");
                return;
            }

            float scale = entry.VisualFootprint / maxSide;
            visual.transform.localScale = Vector3.one * scale;

            // Центр габарита — на pivot корня здания (по XZ), низ — на землю (y=0). Тогда смещение зоны запуска
            // от центра корня корректно выходит за полугабарит визуала. Bounds посчитаны при localPosition=0,
            // поэтому сдвиг визуала = минус центр/минимум этих bounds.
            Vector3 offset = Vector3.zero;
            if (TryGetLocalBounds(building.transform, visual, out Bounds scaled))
            {
                offset = new Vector3(-scaled.center.x, -scaled.min.y, -scaled.center.z);
                visual.transform.localPosition = offset;
            }

            EditorUtility.SetDirty(visual.transform);
            Debug.Log("[Editor] " + entry.Id + ": визуал '" + visual.name + "' габарит " + maxSide.ToString("F2") +
                      " м -> " + entry.VisualFootprint.ToString("F2") + " м, scale " + scale.ToString("F3") +
                      ", смещение " + offset.ToString("F2") + ".");
        }

        /// <summary>
        /// Объединённые bounds всех Renderer'ов визуала в локальных осях <paramref name="space"/>
        /// (не раздувается поворотом здания). ParticleSystemRenderer и TMP-тексты (метки HighRamp) не учитываются.
        /// </summary>
        private static bool TryGetLocalBounds(Transform space, GameObject root, out Bounds bounds)
        {
            bounds = default;
            bool has = false;
            Matrix4x4 worldToSpace = space.worldToLocalMatrix;

            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer || r.GetComponent<TMP_Text>() != null)
                    continue;

                // localBounds SkinnedMeshRenderer — относительно корневой кости, поэтому для него берём мировые bounds.
                Matrix4x4 toSpace;
                Bounds source;
                if (r is SkinnedMeshRenderer)
                {
                    toSpace = worldToSpace;
                    source = r.bounds;
                }
                else
                {
                    toSpace = worldToSpace * r.transform.localToWorldMatrix;
                    source = r.localBounds;
                }

                Vector3 c = source.center;
                Vector3 e = source.extents;
                for (int i = 0; i < 8; i++)
                {
                    var corner = c + new Vector3((i & 1) == 0 ? -e.x : e.x,
                                                 (i & 2) == 0 ? -e.y : e.y,
                                                 (i & 4) == 0 ? -e.z : e.z);
                    Vector3 p = toSpace.MultiplyPoint3x4(corner);
                    if (!has)
                    {
                        bounds = new Bounds(p, Vector3.zero);
                        has = true;
                    }
                    else
                    {
                        bounds.Encapsulate(p);
                    }
                }
            }

            return has;
        }

        /// <summary>
        /// Локальная позиция подписи/панели: над bounds контента (в осях здания), чтобы надпись не пряталась
        /// внутри модели; контента нет — на LabelDefaultHeight.
        /// </summary>
        private static Vector3 ComputeLabelLocalPosition(GameObject building, GameObject content)
        {
            if (content != null && TryGetLocalBounds(building.transform, content, out Bounds vb))
                return new Vector3(vb.center.x, Mathf.Min(Mathf.Max(vb.max.y, 0f) + LabelMargin, MaxLabelHeight), vb.center.z);
            return new Vector3(0f, LabelDefaultHeight, 0f);
        }

        /// <summary>Мировой поворот «лицом к камере хаба»: LookRotation(-FollowOffset.normalized), как у LaunchConfirmCanvas.</summary>
        private static bool TryGetHubViewRotation(out Quaternion rotation)
        {
            rotation = Quaternion.identity;
            var camConfig = AssetDatabase.LoadAssetAtPath<HubCameraConfig>(CreateBuildingSoSetEditor.CameraConfigPath);
            if (camConfig == null || camConfig.FollowOffset.sqrMagnitude < 0.0001f)
                return false;

            Vector3 viewDir = -camConfig.FollowOffset.normalized;
            Vector3 up = Mathf.Abs(Vector3.Dot(viewDir, Vector3.up)) > 0.999f ? Vector3.forward : Vector3.up;
            rotation = Quaternion.LookRotation(viewDir, up);
            return true;
        }

        /// <summary>
        /// Подпись площадки: world-space TextMeshPro (3D, не UGUI) Buildings/&lt;Id&gt;/NameLabel над
        /// визуалом. Текст — DisplayName; для Enabled=false — DisplayName + «\n» + ComingSoonLabel
        /// (без DisplayName — только ComingSoonLabel). Пустой итоговый текст = подписи нет (существующая
        /// удаляется). Старая метка 11a ComingSoonLabel удаляется (миграция, не плодим две надписи).
        /// Текст/позиция/поворот берутся из каталога каждый прогон; стиль (шрифт, кегль, цвет) задаётся
        /// только при создании объекта; raycastTarget/maskable = false — на каждом прогоне.
        /// </summary>
        private static void SyncNameLabel(GameObject building, BuildingCatalogConfig.BuildingEntry entry,
            string comingSoonText, Vector3 localPos, Quaternion worldRotation)
        {
            var legacy = building.transform.Find(LegacyLabelName);
            if (legacy != null)
            {
                Undo.DestroyObjectImmediate(legacy.gameObject);
                Debug.Log("[Editor] " + entry.Id + ": старая метка " + LegacyLabelName + " удалена (миграция в " + NameLabelName + ").");
            }

            string text = entry.DisplayName;
            if (!entry.Enabled)
                text = string.IsNullOrEmpty(text) ? comingSoonText : text + "\n" + comingSoonText;

            var labelT = building.transform.Find(NameLabelName);
            if (string.IsNullOrEmpty(text))
            {
                if (labelT != null)
                {
                    Undo.DestroyObjectImmediate(labelT.gameObject);
                    Debug.Log("[Editor] " + entry.Id + ": подпись пуста в каталоге — " + NameLabelName + " удалён.");
                }
                return;
            }

            GameObject labelGo;
            bool created = labelT == null;
            if (created)
            {
                labelGo = new GameObject(NameLabelName);
                Undo.RegisterCreatedObjectUndo(labelGo, "Create " + NameLabelName);
                labelGo.transform.SetParent(building.transform, false);
                Debug.Log("[Editor] " + entry.Id + ": создана подпись " + NameLabelName + ".");
            }
            else
            {
                labelGo = labelT.gameObject;
            }

            // Сначала компонент: Undo.AddComponent<TextMeshPro> заменяет Transform на RectTransform,
            // поэтому запись Undo и позиционирование — только ПОСЛЕ него (иначе Undo на уничтоженный Transform).
            var tmp = labelGo.GetComponent<TextMeshPro>();
            if (tmp == null)
            {
                tmp = Undo.AddComponent<TextMeshPro>(labelGo);
                created = true;
            }
            else
            {
                Undo.RecordObject(tmp, "Update " + NameLabelName);
            }

            // Позиция (над bounds контента, в осях здания) посчитана заранее — ComputeLabelLocalPosition.
            Undo.RecordObject(labelGo.transform, "Place " + NameLabelName);
            labelGo.transform.localPosition = localPos;
            // Мировой поворот — не зависит от yaw здания: подпись всегда обращена к камере хаба.
            labelGo.transform.rotation = worldRotation;
            labelGo.transform.localScale = Vector3.one;

            // Служебные флаги Graphic — на каждом прогоне (миграция подписей, собранных старой версией).
            tmp.raycastTarget = false; // рейкаст для world-space подписи не нужен
            tmp.maskable = false;

            if (created)
            {
                tmp.fontSize = LabelFontSize;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.color = Color.white;
                tmp.textWrappingMode = TextWrappingModes.NoWrap;

                var font = UIBuildHelpers.LoadFont(UIBuildHelpers.DisplayFontPath);
                if (font != null)
                    tmp.font = font;
            }

            tmp.text = text;
            EditorUtility.SetDirty(tmp);
            Debug.Log("[Editor] " + entry.Id + ": подпись '" + text.Replace("\n", " / ") + "' на высоте " +
                      localPos.y.ToString("F2") + " м.");
        }

        // ── Персонаж и камера ───────────────────────────────────────────────────

        private static HubCharacterController EnsureHubCharacter(Scene scene, BuildingCatalogConfig catalog)
        {
            var existing = Object.FindAnyObjectByType<HubCharacterController>(FindObjectsInactive.Include);
            if (existing != null)
            {
                Debug.Log("[Editor] HubCharacter уже в сцене — переиспользуем (позицию не трогаем).");
                return existing;
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BuildHubCharacterPrefabEditor.PrefabPath);
            if (prefab == null)
            {
                Debug.LogError("[Editor] HubCharacter.prefab не найден: " + BuildHubCharacterPrefabEditor.PrefabPath);
                return null;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            Undo.RegisterCreatedObjectUndo(instance, "Create HubCharacter");
            instance.transform.position = catalog.CharacterSpawnPosition;
            instance.transform.rotation = Quaternion.Euler(0f, catalog.CharacterSpawnYRotation, 0f);
            Debug.Log("[Editor] Создан HubCharacter в точке спавна " + catalog.CharacterSpawnPosition + ".");

            var controller = instance.GetComponent<HubCharacterController>();
            if (controller == null)
                Debug.LogWarning("[Editor] На HubCharacter нет HubCharacterController — пересобери префаб.");
            return controller;
        }

        private static void EnsureHubCamera(HubCharacterController hubCharacter)
        {
            // Идемпотентность по компоненту; имя — только запасной поиск.
            GameObject go;
            var behaviourExisting = Object.FindAnyObjectByType<CinemachineHubCameraBehaviour>(FindObjectsInactive.Include);
            if (behaviourExisting != null)
            {
                go = behaviourExisting.gameObject;
            }
            else
            {
                go = GameObject.Find(HubCameraName);
                if (go == null)
                {
                    go = new GameObject(HubCameraName);
                    Undo.RegisterCreatedObjectUndo(go, "Create " + HubCameraName);
                    Debug.Log("[Editor] Создан " + HubCameraName + ".");
                }
            }

            var cmCam = go.GetComponent<CinemachineCamera>();
            if (cmCam == null)
                cmCam = Undo.AddComponent<CinemachineCamera>(go);

            if (go.GetComponent<CinemachineFollow>() == null)
                Undo.AddComponent<CinemachineFollow>(go);

            if (go.GetComponent<CinemachineRotationComposer>() == null)
                Undo.AddComponent<CinemachineRotationComposer>(go);

            var behaviour = go.GetComponent<CinemachineHubCameraBehaviour>();
            if (behaviour == null)
            {
                behaviour = Undo.AddComponent<CinemachineHubCameraBehaviour>(go);
                Debug.Log("[Editor] Добавлен CinemachineHubCameraBehaviour на " + go.name + ".");
            }

            Undo.RecordObject(cmCam, "Setup Hub Camera");
            cmCam.Priority = CameraPriorities.Active;

            if (hubCharacter != null)
            {
                cmCam.Follow = hubCharacter.transform;
                cmCam.LookAt = hubCharacter.transform;
                Debug.Log("[Editor] " + go.name + ": Follow/LookAt = HubCharacter.");
            }
            else
            {
                Debug.LogWarning("[Editor] HubCharacter не найден — Follow/LookAt камеры хаба не заданы.");
            }
            EditorUtility.SetDirty(cmCam);

            var so = new SerializedObject(behaviour);
            SetRef(so, "_camera", cmCam, "CinemachineCamera");

            // Конфиг — свежей ссылкой (после OpenScene).
            var config = AssetDatabase.LoadAssetAtPath<HubCameraConfig>(CreateBuildingSoSetEditor.CameraConfigPath);
            SetRef(so, "_config", config, "HubCameraConfig_Default");
            so.ApplyModifiedProperties();
        }

        // ── Зоны запуска (11b) ──────────────────────────────────────────────────

        /// <summary>
        /// Material маркера зоны: URP Unlit, прозрачный, текстура — кольцо CircleEmpty. Свой ассет в
        /// Development/Art (ThirdParty не трогаем; текстура берётся из локальной копии спрайта пака).
        /// Свойства переприменяются на каждом прогоне — материал служебный, ручного тюнинга не несёт.
        /// </summary>
        private static Material EnsureMarkerMaterial()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MarkerMaterialPath);
            if (mat == null)
            {
                var shader = Shader.Find(UrpUnlitShader);
                if (shader == null)
                {
                    Debug.LogError("[Editor] Шейдер " + UrpUnlitShader + " не найден — маркер зон без материала.");
                    return null;
                }

                mat = new Material(shader) { name = "HubLaunchZoneMarker" };
                AssetDatabase.CreateAsset(mat, MarkerMaterialPath);
                Debug.Log("[Editor] Создан материал маркера зон: " + MarkerMaterialPath);
            }

            var sprite = UIBuildHelpers.LoadSprite(CircleEmptyPath);
            if (sprite != null && mat.HasProperty("_BaseMap"))
                mat.SetTexture("_BaseMap", sprite.texture);

            // Прозрачный режим URP Unlit вручную (ShaderGUI-хелперы internal): alpha blend, без ZWrite.
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
            if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f);
            if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (mat.HasProperty("_SrcBlendAlpha")) mat.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            if (mat.HasProperty("_DstBlendAlpha")) mat.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
            if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", 0f); // двусторонний — видно с любого ракурса
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", ZoneColor);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            mat.SetShaderPassEnabled("ShadowCaster", false);

            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            return mat;
        }

        /// <summary>
        /// Buildings/&lt;Id&gt;/Zone у каждого Enabled-здания с ненулевым ZoneSize (зона запуска и/или вложения).
        /// Компоненты по каталогу: LaunchZoneController — при HasLaunchZone, InvestZoneController — при
        /// StageCount &gt; 0; лишний компонент удаляется. Объект старой раскладки LaunchZone переименовывается
        /// в Zone (второй не плодим). Зданиям без зоны Zone/LaunchZone удаляются. Идемпотентно по компонентам.
        /// </summary>
        private static void SyncZone(GameObject building, BuildingCatalogConfig.BuildingEntry entry, Material markerMaterial)
        {
            var zoneT = building.transform.Find(ZoneName);
            var legacyT = building.transform.Find(LegacyZoneName);

            if (!HasZone(entry))
            {
                if (zoneT != null)
                {
                    Undo.DestroyObjectImmediate(zoneT.gameObject);
                    Debug.Log("[Editor] " + entry.Id + ": зона не нужна — " + ZoneName + " удалён.");
                }
                if (legacyT != null)
                {
                    Undo.DestroyObjectImmediate(legacyT.gameObject);
                    Debug.Log("[Editor] " + entry.Id + ": зона не нужна — " + LegacyZoneName + " удалён.");
                }
                return;
            }

            GameObject zone;
            if (zoneT != null)
            {
                zone = zoneT.gameObject;
                // Остался и старый объект — он лишний.
                if (legacyT != null)
                {
                    Undo.DestroyObjectImmediate(legacyT.gameObject);
                    Debug.Log("[Editor] " + entry.Id + ": дубль " + LegacyZoneName + " удалён (есть " + ZoneName + ").");
                }
            }
            else if (legacyT != null)
            {
                zone = legacyT.gameObject;
                Undo.RecordObject(zone, "Rename LaunchZone To Zone");
                zone.name = ZoneName;
                Debug.Log("[Editor] " + entry.Id + ": " + LegacyZoneName + " переименован в " + ZoneName + ".");
            }
            else
            {
                zone = new GameObject(ZoneName);
                Undo.RegisterCreatedObjectUndo(zone, "Create Zone");
                zone.transform.SetParent(building.transform, false);
                Debug.Log("[Editor] " + entry.Id + ": создан " + ZoneName + ".");
            }

            Undo.RecordObject(zone.transform, "Place Zone");
            zone.transform.localPosition = entry.ZoneOffset;
            zone.transform.localRotation = Quaternion.identity;
            zone.transform.localScale = Vector3.one;

            // Триггер: центр поднят на половину высоты — зона стоит на земле.
            var box = zone.GetComponent<BoxCollider>();
            if (box == null)
                box = Undo.AddComponent<BoxCollider>(zone);
            else
                Undo.RecordObject(box, "Update Zone Collider");
            box.isTrigger = true;
            box.size = entry.ZoneSize;
            box.center = new Vector3(0f, entry.ZoneSize.y * 0.5f, 0f);

            // Триггер без Rigidbody не срабатывает (CLAUDE.md) — kinematic на зоне.
            var rb = zone.GetComponent<Rigidbody>();
            if (rb == null)
                rb = Undo.AddComponent<Rigidbody>(zone);
            else
                Undo.RecordObject(rb, "Update Zone Rigidbody");
            rb.isKinematic = true;
            rb.useGravity = false;

            // Контроллер зоны запуска — только у зданий с запуском режима.
            var launch = zone.GetComponent<LaunchZoneController>();
            if (entry.HasLaunchZone)
            {
                if (launch == null)
                {
                    launch = Undo.AddComponent<LaunchZoneController>(zone);
                    Debug.Log("[Editor] " + entry.Id + ": добавлен LaunchZoneController.");
                }
                SetBuildingId(launch, entry.Id);
            }
            else if (launch != null)
            {
                Undo.DestroyObjectImmediate(launch);
                Debug.Log("[Editor] " + entry.Id + ": лишний LaunchZoneController удалён.");
            }

            // Контроллер зоны вложения — только у достраиваемых зданий.
            var invest = zone.GetComponent<InvestZoneController>();
            if (entry.StageCount > 0)
            {
                if (invest == null)
                {
                    invest = Undo.AddComponent<InvestZoneController>(zone);
                    Debug.Log("[Editor] " + entry.Id + ": добавлен InvestZoneController.");
                }
                SetBuildingId(invest, entry.Id);
            }
            else if (invest != null)
            {
                Undo.DestroyObjectImmediate(invest);
                Debug.Log("[Editor] " + entry.Id + ": лишний InvestZoneController удалён.");
            }

            SyncMarker(zone, entry, markerMaterial);
            EditorUtility.SetDirty(zone);
        }

        /// <summary>Прошивает _buildingId (enum — числовое значение) на компоненте; нет поля — LogWarning.</summary>
        private static void SetBuildingId(Component component, BuildingId id)
        {
            var so = new SerializedObject(component);
            var idProp = so.FindProperty("_buildingId");
            if (idProp == null)
            {
                Debug.LogWarning("[Editor] " + component.GetType().Name + "._buildingId не найдено — id " + id + " не прошит.");
                return;
            }

            if (idProp.intValue == (int)id)
                return;
            idProp.intValue = (int)id;
            so.ApplyModifiedProperties();
        }

        // ── Панель площадки (12a) ───────────────────────────────────────────────

        /// <summary>
        /// Buildings/&lt;Id&gt;/Panel — world-space Canvas над подписью для достраиваемых зданий
        /// (Enabled и StageCount &gt; 0); остальным панель удаляется. Содержимое: ProgressGroup (Track + Fill
        /// Filled/Horizontal/Left + RemainingText) и RateGroup (RateText). Панель повёрнута к камере хаба
        /// (камера не вращается по yaw), без GraphicRaycaster — EventSystem не нужен. Цвета, шрифты и
        /// тексты существующих элементов не затираются; раскладка и флаги Graphic выставляются на каждом прогоне.
        /// </summary>
        private static void SyncPanel(GameObject building, BuildingCatalogConfig.BuildingEntry entry,
            Vector3 labelLocalPos, Quaternion worldRotation)
        {
            var panelT = building.transform.Find(PanelName);

            if (!(entry.Enabled && entry.StageCount > 0))
            {
                if (panelT != null)
                {
                    Undo.DestroyObjectImmediate(panelT.gameObject);
                    Debug.Log("[Editor] " + entry.Id + ": панель не нужна — " + PanelName + " удалён.");
                }
                return;
            }

            // Панель без RectTransform (посторонний объект с таким именем) — пересоздаём.
            if (panelT != null && !(panelT is RectTransform))
            {
                Undo.DestroyObjectImmediate(panelT.gameObject);
                panelT = null;
            }

            RectTransform rt;
            if (panelT == null)
            {
                var go = new GameObject(PanelName, typeof(RectTransform));
                Undo.RegisterCreatedObjectUndo(go, "Create " + PanelName);
                go.transform.SetParent(building.transform, false);
                rt = (RectTransform)go.transform;
                Debug.Log("[Editor] " + entry.Id + ": создана панель " + PanelName + ".");
            }
            else
            {
                rt = (RectTransform)panelT;
            }

            var canvas = rt.GetComponent<Canvas>();
            if (canvas == null)
                canvas = Undo.AddComponent<Canvas>(rt.gameObject);
            canvas.renderMode = RenderMode.WorldSpace;

            Undo.RecordObject(rt, "Place " + PanelName);
            rt.sizeDelta = new Vector2(PanelPixelsW, PanelPixelsH);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.localScale = Vector3.one * PanelScale;
            // Ценник — про зону, где стоит игрок: над центром зоны на фиксированной высоте (не зависит
            // от высоты контента последней стадии). Зона лежит в локальных осях корня здания на
            // entry.ZoneOffset (та же формула, что у объекта Zone); локальный y=0 корня — уровень земли.
            // Нет зоны — прежняя логика: над подписью.
            rt.localPosition = HasZone(entry)
                ? entry.ZoneOffset + new Vector3(0f, PanelHeightAboveZone, 0f)
                : labelLocalPos + new Vector3(0f, PanelAboveLabel, 0f);
            // Мировой поворот — не зависит от yaw здания: панель всегда обращена к камере хаба.
            rt.rotation = worldRotation;

            // ── ProgressGroup: верхняя половина ─────────────────────────────
            var progressRt = UIBuildHelpers.FindOrCreateChild(rt, "ProgressGroup");
            progressRt.anchorMin = new Vector2(0f, 0.5f);
            progressRt.anchorMax = Vector2.one;
            progressRt.pivot = new Vector2(0.5f, 0.5f);
            progressRt.offsetMin = Vector2.zero;
            progressRt.offsetMax = Vector2.zero;

            bool trackNew = progressRt.Find("Track") == null;
            var track = UIBuildHelpers.CreateImage(progressRt, "Track", null);
            UIBuildHelpers.Stretch((RectTransform)track.transform);
            if (trackNew)
            {
                track.sprite = null;
                track.color = PanelTrackColor;
            }
            track.raycastTarget = false;
            track.maskable = false;

            bool fillNew = progressRt.Find("Fill") == null;
            var fill = UIBuildHelpers.CreateImage(progressRt, "Fill", null);
            var fillRt = (RectTransform)fill.transform;
            UIBuildHelpers.Stretch(fillRt);
            fillRt.offsetMin = new Vector2(PanelFillInset, PanelFillInset);
            fillRt.offsetMax = new Vector2(-PanelFillInset, -PanelFillInset);
            if (fill.sprite == null)
            {
                // Filled без спрайта не заполняется — берём встроенный UISprite Unity.
                fill.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>(BuiltinSpritePath);
                if (fill.sprite == null)
                    Debug.LogWarning("[Editor] Встроенный спрайт " + BuiltinSpritePath + " не найден — Fill без спрайта.");
            }
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 0f;
            if (fillNew)
                fill.color = PanelFillColor;
            fill.raycastTarget = false;
            fill.maskable = false;

            var remaining = EnsureLabel(progressRt, "RemainingText", "0 LEFT", UIBuildHelpers.BodyFontPath,
                40f, Color.white, TextAlignmentOptions.Center);
            UIBuildHelpers.Stretch((RectTransform)remaining.transform);

            // ── RateGroup: нижняя половина ──────────────────────────────────
            var rateRt = UIBuildHelpers.FindOrCreateChild(rt, "RateGroup");
            rateRt.anchorMin = Vector2.zero;
            rateRt.anchorMax = new Vector2(1f, 0.5f);
            rateRt.pivot = new Vector2(0.5f, 0.5f);
            rateRt.offsetMin = Vector2.zero;
            rateRt.offsetMax = Vector2.zero;

            var rateText = EnsureLabel(rateRt, "RateText", "+0/min", UIBuildHelpers.BodyFontPath,
                44f, PanelRateColor, TextAlignmentOptions.Center);
            UIBuildHelpers.Stretch((RectTransform)rateText.transform);

            // ── BuildingPanelView на корне панели ───────────────────────────
            var view = rt.GetComponent<BuildingPanelView>();
            if (view == null)
            {
                view = Undo.AddComponent<BuildingPanelView>(rt.gameObject);
                Debug.Log("[Editor] " + entry.Id + ": добавлен BuildingPanelView.");
            }

            var so = new SerializedObject(view);
            var idProp = so.FindProperty("_buildingId");
            if (idProp == null)
                Debug.LogWarning("[Editor] BuildingPanelView._buildingId не найдено — id панели " + entry.Id + " не прошит.");
            else
                idProp.intValue = (int)entry.Id; // enum — числовое значение
            SetRef(so, "_progressGroup", progressRt.gameObject, "ProgressGroup");
            SetRef(so, "_progressFill", fill, "Fill");
            SetRef(so, "_remainingText", remaining, "RemainingText");
            SetRef(so, "_rateGroup", rateRt.gameObject, "RateGroup");
            SetRef(so, "_rateText", rateText, "RateText");
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(rt.gameObject);
        }

        /// <summary>Плоский Quad-маркер на земле (без коллайдера и теней), размер = размер зоны по XZ.</summary>
        private static void SyncMarker(GameObject zone, BuildingCatalogConfig.BuildingEntry entry, Material markerMaterial)
        {
            GameObject marker;
            var markerT = zone.transform.Find(MarkerName);
            if (markerT == null || markerT.GetComponent<MeshFilter>() == null)
            {
                if (markerT != null)
                    Undo.DestroyObjectImmediate(markerT.gameObject);

                marker = GameObject.CreatePrimitive(PrimitiveType.Quad);
                marker.name = MarkerName;
                // Коллайдер примитива убираем до регистрации в Undo — маркер чисто визуальный.
                Object.DestroyImmediate(marker.GetComponent<Collider>());
                Undo.RegisterCreatedObjectUndo(marker, "Create LaunchZone Marker");
                marker.transform.SetParent(zone.transform, false);
                Debug.Log("[Editor] " + entry.Id + ": создан Marker.");
            }
            else
            {
                marker = markerT.gameObject;
                var stray = marker.GetComponent<Collider>();
                if (stray != null)
                    Undo.DestroyObjectImmediate(stray);
            }

            // Мировая высота маркера = MarkerHeight независимо от Position.y/offset.y здания.
            Undo.RecordObject(marker.transform, "Place LaunchZone Marker");
            marker.transform.localPosition = new Vector3(0f, MarkerHeight - entry.Position.y - entry.ZoneOffset.y, 0f);
            marker.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // Quad смотрит лицом вверх
            marker.transform.localScale = new Vector3(entry.ZoneSize.x, entry.ZoneSize.z, 1f);

            var mr = marker.GetComponent<MeshRenderer>();
            if (mr == null)
                return;

            Undo.RecordObject(mr, "Setup LaunchZone Marker Renderer");
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            if (markerMaterial != null)
                mr.sharedMaterial = markerMaterial;
            else
                Debug.LogWarning("[Editor] Материал маркера не создан — Marker " + entry.Id + " без материала.");
        }

        // ── HUD хаба и круг подтверждения (11b) ─────────────────────────────────

        /// <summary>
        /// UICanvas/HubHud: CoinsPanel (верх-лево: иконка + баланс) и PlayButton (низ-право).
        /// Левый низ занят HubStick, правый верх — паузой мобильного канваса, поэтому HUD там не рисуем.
        /// Тексты и цвета существующих элементов при повторном прогоне не затираются.
        /// </summary>
        private static HubHudView EnsureHubHud(Canvas canvas)
        {
            var hudRt = UIBuildHelpers.FindOrCreateChild(canvas.transform, HubHudName);
            UIBuildHelpers.Stretch(hudRt);

            var view = hudRt.GetComponent<HubHudView>();
            if (view == null)
            {
                view = Undo.AddComponent<HubHudView>(hudRt.gameObject);
                Debug.Log("[Editor] Добавлен HubHudView на " + HubHudName + ".");
            }

            // ── CoinsPanel ──────────────────────────────────────────────────
            var panelRt = UIBuildHelpers.FindOrCreateChild(hudRt, CoinsPanelName);
            UIBuildHelpers.Anchor(panelRt, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(60f, -60f), new Vector2(340f, 90f));

            var panelImg = panelRt.GetComponent<Image>();
            if (panelImg == null)
            {
                panelImg = Undo.AddComponent<Image>(panelRt.gameObject);
                panelImg.sprite = null;
                panelImg.color = CoinsPanelColor; // только при создании — цвет дизайнера не затираем
                Debug.Log("[Editor] Создана плашка CoinsPanel.");
            }
            panelImg.raycastTarget = false;
            panelImg.maskable = false;

            var iconImg = UIBuildHelpers.CreateImage(panelRt, "CoinIcon", MoneyIconPath);
            UIBuildHelpers.Anchor((RectTransform)iconImg.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Vector2(64f, 64f));
            iconImg.type = Image.Type.Simple;
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;
            iconImg.maskable = false;

            var coinsLabel = EnsureLabel(panelRt, CoinsLabelName, "0", UIBuildHelpers.BodyFontPath,
                44f, Color.white, TextAlignmentOptions.MidlineLeft);
            var labelRt = (RectTransform)coinsLabel.transform;
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = new Vector2(92f, 0f);
            labelRt.offsetMax = new Vector2(-14f, 0f);

            // ── PlayButton ──────────────────────────────────────────────────
            var existingBtn = hudRt.Find(PlayButtonName);
            bool hasLabel = existingBtn != null && existingBtn.Find(UIBuildHelpers.ButtonLabelName) != null;

            // Подпись передаём только при первой сборке: CreateButton перезаписывает текст Label.
            var playBtn = UIBuildHelpers.CreateButton(hudRt, PlayButtonName, PlayButtonSprite,
                hasLabel ? null : "PLAY", UIBuildHelpers.DisplayFontPath, 56f, UIBuildHelpers.DarkTextColor);
            UIBuildHelpers.Anchor((RectTransform)playBtn.transform, new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(1f, 0f), new Vector2(-60f, 60f), new Vector2(380f, 130f));

            var btnImg = playBtn.GetComponent<Image>();
            btnImg.raycastTarget = true; // кнопка ловит тап
            btnImg.maskable = false;
            UIBuildHelpers.ConfigureTapButton(playBtn);

            var btnLabelT = playBtn.transform.Find(UIBuildHelpers.ButtonLabelName);
            if (btnLabelT != null)
            {
                var btnLabel = btnLabelT.GetComponent<TextMeshProUGUI>();
                if (btnLabel != null)
                {
                    btnLabel.raycastTarget = false;
                    btnLabel.maskable = false;
                }
            }

            var so = new SerializedObject(view);
            SetRef(so, "_coinsLabel", coinsLabel, "CoinsLabel");
            SetRef(so, "_playButton", playBtn, "PlayButton");
            so.ApplyModifiedProperties();

            Debug.Log("[Editor] HubHud собран: CoinsPanel + PlayButton.");
            return view;
        }

        /// <summary>
        /// Существующий TMP-текст возвращает как есть (текст/шрифт/цвет дизайнера не затираются),
        /// отсутствующий — создаёт через CreateLabel. В обоих случаях гасит raycastTarget/maskable.
        /// </summary>
        private static TMP_Text EnsureLabel(Transform parent, string name, string text, string fontPath,
            float fontSize, Color color, TextAlignmentOptions alignment)
        {
            TMP_Text label = null;
            var existing = parent.Find(name);
            if (existing != null)
                label = existing.GetComponent<TextMeshProUGUI>();

            if (label == null)
                label = UIBuildHelpers.CreateLabel(parent, name, text, fontPath, fontSize, color, alignment);

            label.raycastTarget = false;
            label.maskable = false;
            return label;
        }

        /// <summary>
        /// Корневой world-space канвас LaunchConfirmCanvas (отдельный канвас обоснован: world-space,
        /// круг висит над зоной). LaunchConfirmView — на корне (никогда не гасится), гасится дочерний Root.
        /// Позицию задаёт рантайм (ShowAt); здесь — размер, масштаб и поворот к камере хаба.
        ///
        /// Стиль круга — единственный источник здесь (константы Confirm*: цвета, отступ заливки,
        /// preserveAspect Background/Fill): он задаётся скриптом на КАЖДОМ прогоне, ручные правки
        /// в сцене перезаписываются. Осознанное решение пользователя по плейтесту; менять стиль — константами.
        /// </summary>
        private static LaunchConfirmView EnsureLaunchConfirmCanvas()
        {
            var view = Object.FindAnyObjectByType<LaunchConfirmView>(FindObjectsInactive.Include);
            GameObject go;
            if (view != null)
            {
                go = view.gameObject;
            }
            else
            {
                go = new GameObject(ConfirmCanvasName, typeof(RectTransform), typeof(Canvas));
                Undo.RegisterCreatedObjectUndo(go, "Create " + ConfirmCanvasName);
                Debug.Log("[Editor] Создан " + ConfirmCanvasName + ".");
            }

            var canvas = go.GetComponent<Canvas>();
            if (canvas == null)
                canvas = Undo.AddComponent<Canvas>(go);
            canvas.renderMode = RenderMode.WorldSpace;

            var rt = (RectTransform)go.transform;
            Undo.RecordObject(rt, "Setup LaunchConfirmCanvas");
            rt.sizeDelta = new Vector2(ConfirmCanvasPixels, ConfirmCanvasPixels);
            rt.localScale = Vector3.one * ConfirmCanvasScale;

            // Плоскость канваса смотрит на камеру: forward канваса = направление взгляда камеры
            // (от камеры к персонажу), т.е. -FollowOffset. Расчёт — общий TryGetHubViewRotation (свежий конфиг).
            if (TryGetHubViewRotation(out Quaternion viewRotation))
            {
                rt.rotation = viewRotation;
                Debug.Log("[Editor] " + ConfirmCanvasName + ": повёрнут к камере хаба.");
            }
            else
            {
                Debug.LogWarning("[Editor] HubCameraConfig не найден — поворот " + ConfirmCanvasName + " не тронут.");
            }

            // Root (гасится) → Background (кольцо) + Fill (радиальное заполнение).
            var rootRt = UIBuildHelpers.FindOrCreateChild(go.transform, "Root");
            UIBuildHelpers.Stretch(rootRt);

            var bg = UIBuildHelpers.CreateImage(rootRt, "Background", CircleEmptyPath);
            UIBuildHelpers.Stretch((RectTransform)bg.transform);
            bg.type = Image.Type.Simple;
            bg.preserveAspect = true;
            bg.color = ConfirmBackgroundColor; // белое кольцо-подложка
            bg.raycastTarget = false;
            bg.maskable = false;

            // Заливка — сплошной диск circle.png внутри кольца (отступ ConfirmFillInset), Radial360.
            var fill = UIBuildHelpers.CreateImage(rootRt, "Fill", CircleFilledPath);
            var fillRt = (RectTransform)fill.transform;
            UIBuildHelpers.Stretch(fillRt);
            fillRt.offsetMin = new Vector2(ConfirmFillInset, ConfirmFillInset);
            fillRt.offsetMax = new Vector2(-ConfirmFillInset, -ConfirmFillInset);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Radial360;
            fill.fillOrigin = (int)Image.Origin360.Top;
            fill.fillClockwise = true;
            fill.fillAmount = 0f;
            fill.color = ConfirmFillColor; // цвет зоны — тот же, что маркер на земле
            fill.preserveAspect = true;
            fill.raycastTarget = false;
            fill.maskable = false;

            // По умолчанию круг скрыт — показывает рантайм (LaunchConfirmPresenter).
            if (rootRt.gameObject.activeSelf)
            {
                Undo.RecordObject(rootRt.gameObject, "Hide Launch Confirm Root");
                rootRt.gameObject.SetActive(false);
            }

            if (view == null)
            {
                view = go.GetComponent<LaunchConfirmView>();
                if (view == null)
                {
                    view = Undo.AddComponent<LaunchConfirmView>(go);
                    Debug.Log("[Editor] Добавлен LaunchConfirmView на " + ConfirmCanvasName + ".");
                }
            }

            var so = new SerializedObject(view);
            SetRef(so, "_root", rootRt.gameObject, "LaunchConfirm Root");
            SetRef(so, "_fill", fill, "LaunchConfirm Fill");
            so.ApplyModifiedProperties();

            return view;
        }

        // ── Канвасы-префабы ─────────────────────────────────────────────────────

        private static MobileInputCanvas EnsureMobileInputCanvas(Scene scene)
        {
            var mic = Object.FindAnyObjectByType<MobileInputCanvas>(FindObjectsInactive.Include);
            if (mic == null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BuildMobileInputCanvasEditor.PrefabPath);
                if (prefab == null)
                {
                    Debug.LogWarning("[Editor] " + BuildMobileInputCanvasEditor.PrefabPath + " не найден — " +
                                     "запусти 'FlatOutLike/UI/Build Mobile Input Canvas' в игровой сцене и повтори.");
                    return null;
                }

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                Undo.RegisterCreatedObjectUndo(instance, "Create MobileInputCanvas");
                mic = instance.GetComponent<MobileInputCanvas>();
                Debug.Log("[Editor] Создан инстанс MobileInputCanvas.");
            }
            else
            {
                Debug.Log("[Editor] MobileInputCanvas уже в сцене — переиспользуем.");
            }

            if (mic != null)
            {
                var so = new SerializedObject(mic);
                var hubGroup = so.FindProperty("_hubGroup");
                if (hubGroup == null || hubGroup.objectReferenceValue == null)
                    Debug.LogWarning("[Editor] В MobileInputCanvas не прошит _hubGroup — префаб собран до шага 11a. " +
                                     "Запусти 'FlatOutLike/UI/Build Mobile Input Canvas' и повтори.");
            }

            return mic;
        }

        private static PauseOverlayView EnsurePauseOverlay(Scene scene)
        {
            var view = Object.FindAnyObjectByType<PauseOverlayView>(FindObjectsInactive.Include);
            if (view != null)
            {
                Debug.Log("[Editor] PauseOverlayCanvas уже в сцене — переиспользуем.");
                return view;
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BuildPauseOverlayEditor.PrefabPath);
            if (prefab == null)
            {
                Debug.LogWarning("[Editor] " + BuildPauseOverlayEditor.PrefabPath + " не найден — " +
                                 "запусти 'FlatOutLike/UI/Build Pause Overlay' в игровой сцене и повтори.");
                return null;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            Undo.RegisterCreatedObjectUndo(instance, "Create PauseOverlayCanvas");
            Debug.Log("[Editor] Создан инстанс PauseOverlayCanvas.");
            return instance.GetComponent<PauseOverlayView>();
        }

        // ── Прошивка Composition Root ───────────────────────────────────────────

        private static void WireScope(Scene scene, HubCharacterController hubCharacter,
            MobileInputCanvas mobileCanvas, PauseOverlayView pauseView,
            HubHudView hubHudView, LaunchConfirmView confirmView)
        {
            var scope = Object.FindAnyObjectByType<GameLifetimeScope>(FindObjectsInactive.Include);
            if (scope == null)
            {
                Debug.LogWarning("[Editor] GameLifetimeScope не найден — прошивка пропущена.");
                return;
            }

            // Конфиги — свежими ссылками (после OpenScene).
            var charConfig = AssetDatabase.LoadAssetAtPath<HubCharacterConfig>(CreateBuildingSoSetEditor.CharacterConfigPath);
            var catalog    = AssetDatabase.LoadAssetAtPath<BuildingCatalogConfig>(CreateBuildingSoSetEditor.CatalogPath);
            InputConfig inputConfig = CreateDefaultInputConfigEditor.EnsureAsset();

            var so = new SerializedObject(scope);
            SetRef(so, "_hubCharacterConfig", charConfig, "HubCharacterConfig_Default");
            SetRef(so, "_hubCharacter", hubCharacter, "HubCharacterController");
            SetRef(so, "_inputConfig", inputConfig, "InputConfig_Default");

            // 11b: каталог зданий (зоны запуска, параметры подтверждения) и view'ы хаба.
            SetRef(so, "_buildingCatalogConfig", catalog, "BuildingCatalogConfig_Default");
            SetRef(so, "_hubHudView", hubHudView, "HubHudView");
            SetRef(so, "_launchConfirmView", confirmView, "LaunchConfirmView");

            // 12b: VFX стройки — отдельный конфиг хаба (создаёт меню Build Hub Build VFX).
            var hubVfxConfig = AssetDatabase.LoadAssetAtPath<VfxConfig>(BuildHubBuildVfxEditor.HubVfxConfigPath);
            SetRef(so, "_vfxConfig", hubVfxConfig, "VfxConfig_Hub");

            so.ApplyModifiedProperties();

            // Каталоги машин/апгрейдов — как на мета-сценах (метод сам сохраняет активную сцену).
            SetupMetaConfigsEditor.SetupMetaConfigs();

            // Аудио: инстанс AudioPlayer, единственный AudioListener, _audioConfig/_audioPlayerController/_uiClickSoundRelay.
            SetupAudioEditor.WireOpenedScene(scene, scope);

            // Мобильный ввод и пауза — единые точки прошивки проекта.
            if (mobileCanvas != null)
                BuildMobileInputCanvasEditor.BindMobileInputCanvas(mobileCanvas);
            if (pauseView != null)
                BindMetaUIRefsEditor.BindPauseOverlayView(pauseView);
        }

        // ── Утилиты ─────────────────────────────────────────────────────────────

        private static GameObject GetOrCreateRoot(Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == name)
                    return root;

            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Create " + name);
            Debug.Log("[Editor] Создан корневой объект " + name + ".");
            return go;
        }

        private static GameObject FindOrCreateChild(Transform parent, string name)
        {
            var existing = parent.Find(name);
            if (existing != null)
                return existing.gameObject;

            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Create " + name);
            go.transform.SetParent(parent, false);
            Debug.Log("[Editor] Создан " + parent.name + "/" + name + ".");
            return go;
        }

        /// <summary>Прошивает ссылку, не присваивая null: при отсутствии объекта/поля — LogWarning, значение не тронуто.</summary>
        private static void SetRef(SerializedObject so, string field, Object value, string humanName)
        {
            var prop = so.FindProperty(field);
            string owner = so.targetObject.GetType().Name;

            if (prop == null)
            {
                Debug.LogWarning("[Editor] Поле " + owner + "." + field + " не найдено (рантайм ещё не скомпилирован?).");
                return;
            }

            if (value == null)
            {
                Debug.LogWarning("[Editor] " + humanName + " не найден — поле " + owner + "." + field + " не тронуто.");
                return;
            }

            if (prop.objectReferenceValue == value)
                return;

            prop.objectReferenceValue = value;
            Debug.Log("[Editor] " + owner + "." + field + " прошит.");
        }
    }
}
#endif
