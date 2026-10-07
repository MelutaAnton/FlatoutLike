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
using FlatOutLike.Editor.Configs;
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
    ///   • (11b) Buildings/&lt;Id&gt;/LaunchZone (триггер + kinematic Rigidbody + LaunchZoneController + Marker)
    ///     для entry с HasLaunchZone; UICanvas/HubHud (CoinsPanel + PlayButton);
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

        private const string LaunchZoneName    = "LaunchZone";
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
        private const float LabelFallbackPitchDeg = 50f; // поворот, если HubCameraConfig не найден
        private const float LabelFontSize      = 9f;

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

            foreach (var entry in catalog.Entries)
            {
                processed++;
                var go = FindOrCreateChild(root.transform, entry.Id.ToString());

                // Каталог — единственный источник раскладки: корень синхронизируется каждый прогон.
                Undo.RecordObject(go.transform, "Place Building");
                go.transform.localPosition = entry.Position;
                go.transform.localRotation = Quaternion.Euler(0f, entry.YRotation, 0f);
                go.transform.localScale = Vector3.one;

                var visual = SyncVisual(go, entry);
                if (visual != null && entry.VisualFootprint > 0f)
                    FitVisual(go, visual, entry);

                SyncNameLabel(go, entry, catalog.ComingSoonLabel, visual, labelRotation);

                if (entry.HasLaunchZone && entry.Enabled && markerMaterial == null)
                    markerMaterial = EnsureMarkerMaterial();
                SyncLaunchZone(go, entry, markerMaterial);
            }

            Debug.Log("[Editor] Buildings: обработано зданий — " + processed + ".");

            CheckLaunchZoneOverlaps(catalog);
        }

        /// <summary>
        /// Проверка XZ-перекрытия зон запуска всех entry с HasLaunchZone &amp;&amp; Enabled. Перекрытия нежелательны:
        /// персонаж, стоя на стыке, может активировать соседнюю зону. Зона — прямоугольник
        /// LaunchZoneSize (x/z) вокруг GetLaunchZoneWorldCenter(), повёрнутый на YRotation здания
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
                if (!(entry.HasLaunchZone && entry.Enabled))
                    continue;

                Vector3 c = entry.GetLaunchZoneWorldCenter();
                float rad = entry.YRotation * Mathf.Deg2Rad;
                float cos = Mathf.Abs(Mathf.Cos(rad));
                float sin = Mathf.Abs(Mathf.Sin(rad));
                float hx = entry.LaunchZoneSize.x * 0.5f;
                float hz = entry.LaunchZoneSize.z * 0.5f;

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
                    Debug.LogWarning("[Editor] Зоны запуска " + ids[i] + " и " + ids[j] +
                                     " перекрываются — раздвинь Position/LaunchZoneOffset/LaunchZoneSize в каталоге.");
                }
            }

            if (overlaps == 0)
                Debug.Log("[Editor] Зоны запуска: перекрытий нет (проверено зон — " + ids.Count + ").");
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

        /// <summary>
        /// Гарантирует ровно один инстанс entry.VisualPrefab внутри здания; инстансы другого
        /// префаба (после смены визуала в каталоге) заменяются.
        /// </summary>
        private static GameObject SyncVisual(GameObject building, BuildingCatalogConfig.BuildingEntry entry)
        {
            if (entry.VisualPrefab == null)
            {
                Debug.Log("[Editor] " + entry.Id + ": VisualPrefab в каталоге не задан — визуал не создаётся.");
                return null;
            }

            GameObject found = null;
            for (int i = building.transform.childCount - 1; i >= 0; i--)
            {
                var child = building.transform.GetChild(i).gameObject;
                if (!PrefabUtility.IsAnyPrefabInstanceRoot(child))
                    continue;

                var source = PrefabUtility.GetCorrespondingObjectFromSource(child);
                if (found == null && source == entry.VisualPrefab)
                {
                    found = child;
                    continue;
                }

                Debug.Log("[Editor] " + entry.Id + ": убран устаревший визуал '" + child.name + "'.");
                Undo.DestroyObjectImmediate(child);
            }

            if (found != null)
                return found;

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(entry.VisualPrefab, building.transform);
            Undo.RegisterCreatedObjectUndo(instance, "Create Building Visual");
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            Debug.Log("[Editor] " + entry.Id + ": создан визуал '" + entry.VisualPrefab.name + "'.");
            return instance;
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
            string comingSoonText, GameObject visual, Quaternion worldRotation)
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

            // Позиция: над bounds визуала (в осях здания), чтобы надпись не пряталась внутри модели.
            Vector3 localPos = new Vector3(0f, LabelDefaultHeight, 0f);
            if (visual != null && TryGetLocalBounds(building.transform, visual, out Bounds vb))
                localPos = new Vector3(vb.center.x, Mathf.Max(vb.max.y, 0f) + LabelMargin, vb.center.z);

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
        /// Buildings/&lt;Id&gt;/LaunchZone для entry с HasLaunchZone &amp;&amp; Enabled; у остальных
        /// существующий LaunchZone удаляется (миграция). Идемпотентно по компонентам.
        /// </summary>
        private static void SyncLaunchZone(GameObject building, BuildingCatalogConfig.BuildingEntry entry, Material markerMaterial)
        {
            var zoneT = building.transform.Find(LaunchZoneName);

            if (!(entry.HasLaunchZone && entry.Enabled))
            {
                if (zoneT != null)
                {
                    Undo.DestroyObjectImmediate(zoneT.gameObject);
                    Debug.Log("[Editor] " + entry.Id + ": зона запуска не нужна — LaunchZone удалён.");
                }
                return;
            }

            GameObject zone;
            if (zoneT == null)
            {
                zone = new GameObject(LaunchZoneName);
                Undo.RegisterCreatedObjectUndo(zone, "Create LaunchZone");
                zone.transform.SetParent(building.transform, false);
                Debug.Log("[Editor] " + entry.Id + ": создан LaunchZone.");
            }
            else
            {
                zone = zoneT.gameObject;
            }

            Undo.RecordObject(zone.transform, "Place LaunchZone");
            zone.transform.localPosition = entry.LaunchZoneOffset;
            zone.transform.localRotation = Quaternion.identity;
            zone.transform.localScale = Vector3.one;

            // Триггер: центр поднят на половину высоты — зона стоит на земле.
            var box = zone.GetComponent<BoxCollider>();
            if (box == null)
                box = Undo.AddComponent<BoxCollider>(zone);
            else
                Undo.RecordObject(box, "Update LaunchZone Collider");
            box.isTrigger = true;
            box.size = entry.LaunchZoneSize;
            box.center = new Vector3(0f, entry.LaunchZoneSize.y * 0.5f, 0f);

            // Триггер без Rigidbody не срабатывает (CLAUDE.md) — kinematic на зоне.
            var rb = zone.GetComponent<Rigidbody>();
            if (rb == null)
                rb = Undo.AddComponent<Rigidbody>(zone);
            else
                Undo.RecordObject(rb, "Update LaunchZone Rigidbody");
            rb.isKinematic = true;
            rb.useGravity = false;

            var controller = zone.GetComponent<LaunchZoneController>();
            if (controller == null)
            {
                controller = Undo.AddComponent<LaunchZoneController>(zone);
                Debug.Log("[Editor] " + entry.Id + ": добавлен LaunchZoneController.");
            }

            var so = new SerializedObject(controller);
            var idProp = so.FindProperty("_buildingId");
            if (idProp == null)
            {
                Debug.LogWarning("[Editor] LaunchZoneController._buildingId не найдено — id зоны " + entry.Id + " не прошит.");
            }
            else if (idProp.intValue != (int)entry.Id)
            {
                idProp.intValue = (int)entry.Id; // enum — числовое значение
                so.ApplyModifiedProperties();
            }

            SyncMarker(zone, entry, markerMaterial);
            EditorUtility.SetDirty(zone);
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
            marker.transform.localPosition = new Vector3(0f, MarkerHeight - entry.Position.y - entry.LaunchZoneOffset.y, 0f);
            marker.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // Quad смотрит лицом вверх
            marker.transform.localScale = new Vector3(entry.LaunchZoneSize.x, entry.LaunchZoneSize.z, 1f);

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
