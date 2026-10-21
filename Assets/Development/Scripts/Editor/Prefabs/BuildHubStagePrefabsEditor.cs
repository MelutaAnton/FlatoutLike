#if UNITY_EDITOR
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using FlatOutLike.Data.Configs.Progression;
using FlatOutLike.Editor.Configs;
using FlatOutLike.Models;

namespace FlatOutLike.Editor.Prefabs
{
    /// <summary>
    /// Шаг 12b: собирает префабы промежуточных стадий стройки BaseHub
    /// (Assets/Development/Prefabs/Hub/&lt;Здание&gt;/&lt;Здание&gt;_Stage1|Stage2.prefab) — по два на
    /// HighJumpTower / TicketBooth / Tribunes.
    ///
    /// Префаб = пустой корень (pivot в центре площадки, на земле) + ВЛОЖЕННЫЕ инстансы Synty
    /// (PrefabUtility.InstantiatePrefab — ссылки, не копии). Размеры — в реальных метрах под ВПИСАННЫЙ СЛЕД
    /// ФИНАЛА: финал (Stages[last].StagePrefab, иначе VisualPrefab из каталога; заглушку-плиту заменяет
    /// финал из CreateBuildingSoSetEditor.HubStagePathsTable) измеряется по Renderer-bounds в preview-сцене,
    /// масштаб k = VisualFootprint / max(size.x, size.z) (ровно как FitVisual в Build Base Hub Layout),
    /// след W = size.x·k, D = size.z·k, высота финала H = size.y·k. Оси префаба = локальные оси здания.
    ///   • Stage1 «Фундамент» — бетонная плита W×D + реквизит;
    ///   • Stage2 «Каркас»    — та же плита + леса/колонны/кран.
    /// Плита ставится низом на y=0, остальные детали — на верх плиты; каждая деталь центрируется по
    /// Renderer-bounds в заданной точке (доли W и D от центра), не выступает за плиту больше чем на
    /// 0.3 м и не выше потолка стадии (фундамент ≤ 0.5·H, каркас ≤ 0.85·H). Коллайдеры деталей не трогаем.
    ///
    /// Сигнатура входов («hubStage:W=12.00;D=6.42») пишется в AssetImporter.userData префаба стадии.
    /// Префаб пересобирается, если его нет или сигнатура отсутствует / не совпадает с текущей (ручные
    /// правки префаба при этом теряются — LogWarning); совпадает — не трогается.
    /// Сборка идёт в preview-сцене (активная сцена пользователя не пачкается), затем SaveAsPrefabAsset.
    /// Пути префабов стадий и финалов — единственное место: CreateBuildingSoSetEditor.HubStagePathsTable.
    ///
    /// Порядок: Build Hub Stage Prefabs → Create Building SO Set → Build Base Hub Layout.
    /// </summary>
    public static class BuildHubStagePrefabsEditor
    {
        private const string SyntyRoot = "Assets/ThirdParty/Synty/PolygonConstruction/Prefabs/";

        // Плита — тот же бетонный пол, что служит заглушкой в каталоге; путь живёт в CreateBuildingSoSetEditor.
        private const string SlabPath = CreateBuildingSoSetEditor.ConcreteFloorPrefab;

        // Запасной габарит, если каталога нет или VisualFootprint <= 0 (значения из таблицы презентации каталога).
        private const float FallbackFootprint = 7f;
        private const float FallbackFootprintHighJump = 8f;

        // Запасная высота финала (доля F), если финал не удалось измерить.
        private const float FallbackHeightK = 0.5f;

        // Деталь по умолчанию не шире этой доли опорного размера R = sqrt(W·D) (защита от крупных мешей пака).
        private const float DefaultMaxSideK = 0.45f;

        // Деталь может выступать за плиту не больше чем на это значение, м.
        private const float SlabOvershoot = 0.3f;

        // Потолок верха деталей стадии как доля высоты финала: фундамент < каркас < финал.
        private const float Stage1HeightK = 0.5f;
        private const float Stage2HeightK = 0.85f;

        // Дополнительный потолок для высоких финалов (доли F): min(K·H, K·F).
        private const float Stage1MaxFootprintK = 0.3f;
        private const float Stage2MaxFootprintK = 0.75f;

        // Минимальная сторона плиты (доля F): у тонких финалов (измерительная сетка) след не вырождается.
        private const float MinPlateSideFraction = 0.5f;

        // Минимальная допустимая высота детали над плитой при подрезке по потолку, м.
        private const float MinPartHeight = 0.25f;

        private const string SignaturePrefix = "hubStage:";

        /// <summary>Вписанные габариты финала здания (м): след W×D и высота H. Measured = false — запасные значения.</summary>
        public readonly struct FinalDims
        {
            public readonly float Width;
            public readonly float Depth;
            public readonly float Height;
            public readonly float Footprint;
            public readonly bool Measured;

            public FinalDims(float width, float depth, float height, float footprint, bool measured)
            {
                Width = width;
                Depth = depth;
                Height = height;
                Footprint = footprint;
                Measured = measured;
            }
        }

        /// <summary>
        /// Одна деталь стадии. X — доля ширины W от центра, Z — доля глубины D, Yaw — градусы вокруг вертикали.
        /// </summary>
        private readonly struct Part
        {
            public readonly string Path;
            public readonly float X;
            public readonly float Z;
            public readonly float Yaw;
            public readonly float MaxSide;   // максимум большей горизонтальной стороны, доли R (только уменьшает)
            public readonly float Height;    // целевая высота, доли высоты финала H (0 — не масштабировать по высоте)
            public readonly bool StandUp;    // самая длинная ось меша -> вертикаль

            public Part(string path, float x, float z, float yaw = 0f, float maxSide = DefaultMaxSideK,
                float height = 0f, bool standUp = false)
            {
                Path = path;
                X = x;
                Z = z;
                Yaw = yaw;
                MaxSide = maxSide;
                Height = height;
                StandUp = standUp;
            }
        }

        private static string Prop(string name) => SyntyRoot + "Props/" + name + ".prefab";
        private static string Bld(string name) => SyntyRoot + "Buildings/" + name + ".prefab";

        [MenuItem("FlatOutLike/Prefabs/Build Hub Stage Prefabs")]
        public static void BuildHubStagePrefabs()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<BuildingCatalogConfig>(CreateBuildingSoSetEditor.CatalogPath);
            if (catalog == null)
                Debug.LogWarning("[Editor] Каталог зданий не найден (" + CreateBuildingSoSetEditor.CatalogPath +
                                 ") — габариты берутся из запасных констант.");

            int created = 0;
            int rebuilt = 0;
            int existing = 0;

            foreach (var def in CreateBuildingSoSetEditor.HubStagePathsTable)
            {
                BuildingCatalogConfig.BuildingEntry entry = null;
                if (catalog != null)
                    catalog.TryGetEntry(def.Id, out entry);

                FinalDims dims = ResolveFinalDims(entry, def.Id, true);
                string signature = BuildSignature(dims);
                Debug.Log("[Editor] " + def.Id + ": финал " + (dims.Measured ? "измерен" : "НЕ измерен (запасные значения)") +
                          ", след " + F2(dims.Width) + " x " + F2(dims.Depth) + " м, высота " + F2(dims.Height) +
                          " м (VisualFootprint " + F2(dims.Footprint) + "), сигнатура " + signature + ".");

                float foundationHeight = -1f;
                float frameHeight = -1f;

                for (int stage = 1; stage <= 2; stage++)
                {
                    string path = stage == 1 ? def.Stage1 : def.Stage2;
                    bool existed = AssetDatabase.LoadAssetAtPath<GameObject>(path) != null;
                    if (existed)
                    {
                        string have = ReadSignature(path);
                        if (have == signature)
                        {
                            existing++;
                            Debug.Log("[Editor] Префаб стадии актуален: " + path + " (" + have + ") — не трогаем.");
                            continue;
                        }

                        Debug.LogWarning("[Editor] Префаб стадии " + path + " пересобран: габарит финала изменился " +
                                         "(было '" + (string.IsNullOrEmpty(have) ? "нет сигнатуры" : have) + "', стало '" +
                                         signature + "'), ручные правки префаба потеряны.");
                    }

                    if (BuildPrefab(path, def.Id, stage, dims, signature, out float height))
                    {
                        if (existed)
                            rebuilt++;
                        else
                            created++;

                        if (stage == 1)
                            foundationHeight = height;
                        else
                            frameHeight = height;
                    }
                }

                LogHeights(def.Id, foundationHeight, frameHeight, dims.Height);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Editor] Build Hub Stage Prefabs завершён: создано " + created + ", пересобрано " + rebuilt +
                      ", актуальных " + existing + ".");
        }

        private static void LogHeights(BuildingId id, float foundation, float frame, float final)
        {
            Debug.Log("[Editor] " + id + ": высоты — фундамент " + (foundation >= 0f ? F2(foundation) : "не пересобирался") +
                      " м, каркас " + (frame >= 0f ? F2(frame) : "не пересобирался") + " м, финал " + F2(final) + " м.");

            if (foundation >= 0f && frame >= 0f && foundation >= frame)
                Debug.LogWarning("[Editor] " + id + ": фундамент (" + F2(foundation) + " м) не ниже каркаса (" + F2(frame) +
                                 " м) — проверь состав деталей стадий.");
            if (frame >= 0f && frame >= final)
                Debug.LogWarning("[Editor] " + id + ": каркас (" + F2(frame) + " м) не ниже финала (" + F2(final) + " м).");
            if (foundation >= 0f && foundation >= final)
                Debug.LogWarning("[Editor] " + id + ": фундамент (" + F2(foundation) + " м) не ниже финала (" + F2(final) + " м).");
        }

        // ── Габарит финала и сигнатура (публично: читает и Build Base Hub Layout) ──

        /// <summary>
        /// Вписанные габариты финала здания. Финал не найден / не измерен — запасной след F×F (Warning при
        /// <paramref name="verbose"/>). <paramref name="entry"/> может быть null (каталога нет).
        /// </summary>
        public static FinalDims ResolveFinalDims(BuildingCatalogConfig.BuildingEntry entry, BuildingId id, bool verbose)
        {
            float f = ResolveFootprint(entry, id, verbose);
            float fallbackHeight = f * FallbackHeightK;

            GameObject finalPrefab = ResolveFinalPrefab(entry, id);
            if (finalPrefab == null)
            {
                if (verbose)
                    Debug.LogWarning("[Editor] " + id + ": финальный префаб не найден — плита стадий F x F (" + F2(f) + " м).");
                return new FinalDims(f, f, fallbackHeight, f, false);
            }

            if (!TryMeasurePrefab(finalPrefab, out Vector3 size) || Mathf.Max(size.x, size.z) < 0.0001f)
            {
                if (verbose)
                    Debug.LogWarning("[Editor] " + id + ": не удалось измерить финал '" + finalPrefab.name +
                                     "' — плита стадий F x F (" + F2(f) + " м).");
                return new FinalDims(f, f, fallbackHeight, f, false);
            }

            // Тот же масштаб, что у FitVisual: по большей горизонтальной стороне до VisualFootprint.
            float k = f / Mathf.Max(size.x, size.z);
            // Каждая сторона плиты не меньше MinPlateSideFraction·F; сигнатура строится из клампнутых значений
            // (WarnIfStageStale в Build Base Hub Layout использует эту же функцию).
            float minSide = MinPlateSideFraction * f;
            float width = Mathf.Max(size.x * k, minSide);
            float depth = Mathf.Max(size.z * k, minSide);
            return new FinalDims(width, depth, size.y * k, f, true);
        }

        /// <summary>Сигнатура входов сборки стадии, например «hubStage:W=12.00;D=6.42» (округление до 0.01).</summary>
        public static string BuildSignature(FinalDims dims)
        {
            return SignaturePrefix + "W=" + dims.Width.ToString("F2", CultureInfo.InvariantCulture) +
                   ";D=" + dims.Depth.ToString("F2", CultureInfo.InvariantCulture);
        }

        /// <summary>Сигнатура, записанная в userData префаба стадии (пусто, если нет).</summary>
        public static string ReadSignature(string prefabPath)
        {
            var importer = AssetImporter.GetAtPath(prefabPath);
            return importer != null && importer.userData != null ? importer.userData : string.Empty;
        }

        /// <summary>
        /// Для Build Base Hub Layout: если <paramref name="stagePrefab"/> — наш префаб стадии здания
        /// <paramref name="entry"/>, а его сигнатура не совпадает с текущей сигнатурой финала — LogWarning.
        /// Чужие (назначенные вручную) префабы не проверяются.
        /// </summary>
        public static void WarnIfStageStale(BuildingCatalogConfig.BuildingEntry entry, GameObject stagePrefab)
        {
            if (entry == null || stagePrefab == null)
                return;

            string path = AssetDatabase.GetAssetPath(stagePrefab);
            if (!TryFindDef(entry.Id, out CreateBuildingSoSetEditor.HubStagePaths def) ||
                (path != def.Stage1 && path != def.Stage2))
                return;

            string expected = BuildSignature(ResolveFinalDims(entry, entry.Id, false));
            string actual = ReadSignature(path);
            if (actual == expected)
                return;

            Debug.LogWarning("[Editor] " + entry.Id + ": префаб стадии " + path + " собран под другой габарит финала (" +
                             (string.IsNullOrEmpty(actual) ? "сигнатуры нет" : actual) + ", сейчас нужно " + expected +
                             ") — прогони FlatOutLike/Prefabs/Build Hub Stage Prefabs.");
        }

        private static bool TryFindDef(BuildingId id, out CreateBuildingSoSetEditor.HubStagePaths result)
        {
            foreach (var def in CreateBuildingSoSetEditor.HubStagePathsTable)
            {
                if (def.Id != id)
                    continue;
                result = def;
                return true;
            }

            result = default;
            return false;
        }

        /// <summary>
        /// Финал здания: Stages[last].StagePrefab (если это не наш же префаб стадии), иначе VisualPrefab;
        /// пустой VisualPrefab или заглушка-плита у здания с ReplaceStubVisual — финал из таблицы путей.
        /// </summary>
        private static GameObject ResolveFinalPrefab(BuildingCatalogConfig.BuildingEntry entry, BuildingId id)
        {
            bool hasDef = TryFindDef(id, out CreateBuildingSoSetEditor.HubStagePaths def);

            if (entry != null && entry.Stages != null && entry.Stages.Length > 0)
            {
                var last = entry.Stages[entry.Stages.Length - 1];
                if (last != null && last.StagePrefab != null)
                {
                    string p = AssetDatabase.GetAssetPath(last.StagePrefab);
                    if (!hasDef || (p != def.Stage1 && p != def.Stage2))
                        return last.StagePrefab;
                }
            }

            if (entry != null && entry.VisualPrefab != null)
            {
                string visualPath = AssetDatabase.GetAssetPath(entry.VisualPrefab);
                bool stub = hasDef && def.ReplaceStubVisual && visualPath == CreateBuildingSoSetEditor.ConcreteFloorPrefab;
                if (!stub)
                    return entry.VisualPrefab;
            }

            return hasDef ? AssetDatabase.LoadAssetAtPath<GameObject>(def.Final) : null;
        }

        /// <summary>Renderer-bounds префаба (инстанс в preview-сцене, identity) — размер в осях здания.</summary>
        private static bool TryMeasurePrefab(GameObject prefab, out Vector3 size)
        {
            size = default;
            var previewScene = EditorSceneManager.NewPreviewScene();
            try
            {
                var holder = new GameObject("Measure");
                SceneManager.MoveGameObjectToScene(holder, previewScene);

                var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, holder.transform);
                inst.transform.localPosition = Vector3.zero;
                inst.transform.localRotation = Quaternion.identity;
                inst.transform.localScale = Vector3.one;

                if (!TryGetBounds(holder.transform, inst, out Bounds b))
                    return false;

                size = b.size;
                return true;
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(previewScene);
            }
        }

        private static float ResolveFootprint(BuildingCatalogConfig.BuildingEntry entry, BuildingId id, bool verbose)
        {
            if (entry != null && entry.VisualFootprint > 0f)
                return entry.VisualFootprint;

            float fallback = id == BuildingId.HighJumpTower ? FallbackFootprintHighJump : FallbackFootprint;
            if (verbose)
                Debug.LogWarning("[Editor] " + id + ": VisualFootprint в каталоге не задан — запасной габарит " +
                                 fallback + " м.");
            return fallback;
        }

        private static string F2(float value) => value.ToString("F2", CultureInfo.InvariantCulture);

        // ── Сборка префаба ──────────────────────────────────────────────────────

        private static bool BuildPrefab(string path, BuildingId id, int stage, FinalDims dims, string signature,
            out float totalHeight)
        {
            totalHeight = -1f;
            EnsureFolderPath(Path.GetDirectoryName(path).Replace('\\', '/'));

            var parts = GetParts(id, stage);
            var previewScene = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = new GameObject(Path.GetFileNameWithoutExtension(path));
                SceneManager.MoveGameObjectToScene(root, previewScene);
                // Undo для объектов preview-сцены и ассетов не регистрируем (см. editor-files.md).

                int placed = 0;
                float groundY = 0f;

                // Плита: низом на y=0, XZ подгоняется под вписанный след финала W×D.
                if (PlaceSlab(root.transform, dims, out float slabTop))
                {
                    placed++;
                    groundY = slabTop;
                }

                // Потолок верха деталей этой стадии (абсолютный y).
                float capTop = stage == 1
                    ? Mathf.Min(Stage1HeightK * dims.Height, Stage1MaxFootprintK * dims.Footprint)
                    : Mathf.Min(Stage2HeightK * dims.Height, Stage2MaxFootprintK * dims.Footprint);

                foreach (var part in parts)
                {
                    if (PlacePart(root.transform, part, dims, groundY, capTop))
                        placed++;
                }

                if (placed == 0)
                {
                    Debug.LogWarning("[Editor] " + path + ": не удалось разместить ни одной детали — префаб не создан.");
                    return false;
                }

                string sizeText = "габарит не определён";
                if (TryGetBounds(root.transform, root, out Bounds total))
                {
                    totalHeight = total.size.y;
                    sizeText = "габарит " + F2(total.size.x) + " x " + F2(total.size.y) + " x " + F2(total.size.z) +
                               " м (плита " + F2(dims.Width) + " x " + F2(dims.Depth) + ", потолок " + F2(capTop) + " м)";
                }

                var saved = PrefabUtility.SaveAsPrefabAsset(root, path);
                if (saved == null)
                {
                    Debug.LogError("[Editor] SaveAsPrefabAsset вернул null: " + path);
                    return false;
                }

                WriteSignature(path, signature);

                Debug.Log("[Editor] Создан префаб " + id + " Stage" + stage + ": " + path + ", деталей " + placed + ", " +
                          sizeText + ", сигнатура " + signature + ".");
                return true;
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(previewScene);
            }
        }

        /// <summary>Пишет сигнатуру в userData импортёра префаба (хранится в .meta).</summary>
        private static void WriteSignature(string path, string signature)
        {
            var importer = AssetImporter.GetAtPath(path);
            if (importer == null)
            {
                Debug.LogWarning("[Editor] У " + path + " нет AssetImporter — сигнатура не записана " +
                                 "(префаб будет пересобираться при каждом прогоне).");
                return;
            }

            if (importer.userData == signature)
                return;

            importer.userData = signature;
            importer.SaveAndReimport();
        }

        private static bool PlaceSlab(Transform root, FinalDims dims, out float top)
        {
            top = 0f;
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(SlabPath);
            if (asset == null)
            {
                Debug.LogWarning("[Editor] Плита не найдена: " + SlabPath + " — стадия без плиты (детали на y=0).");
                return false;
            }

            var inst = (GameObject)PrefabUtility.InstantiatePrefab(asset, root);
            var t = inst.transform;
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one;

            if (!TryGetBounds(root, inst, out Bounds b) || b.size.x < 0.0001f || b.size.z < 0.0001f)
            {
                Debug.LogWarning("[Editor] У плиты нет подходящих Renderer'ов — деталь пропущена.");
                Object.DestroyImmediate(inst);
                return false;
            }

            // Подгонка по XZ под W×D (высоту плиты не масштабируем).
            t.localScale = new Vector3(dims.Width / b.size.x, 1f, dims.Depth / b.size.z);
            TryGetBounds(root, inst, out b);
            t.localPosition = new Vector3(-b.center.x, -b.min.y, -b.center.z);
            TryGetBounds(root, inst, out b);
            top = b.max.y;

            Debug.Log("[Editor] Плита: scale " + t.localScale.ToString("F3") + ", толщина " + F2(b.size.y) + " м.");
            return true;
        }

        private static bool PlacePart(Transform root, Part p, FinalDims dims, float groundY, float capTop)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(p.Path);
            if (asset == null)
            {
                Debug.LogWarning("[Editor] Деталь не найдена: " + p.Path + " — пропущена.");
                return false;
            }

            var inst = (GameObject)PrefabUtility.InstantiatePrefab(asset, root);
            var t = inst.transform;
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one;

            if (!TryGetBounds(root, inst, out Bounds b))
            {
                Debug.LogWarning("[Editor] У детали '" + asset.name + "' нет подходящих Renderer'ов — пропущена.");
                Object.DestroyImmediate(inst);
                return false;
            }

            // Вертикаль: самая длинная ось меша -> Y (кран и т.п.). Поворот в осях корня, затем yaw.
            Quaternion stand = Quaternion.identity;
            if (p.StandUp)
            {
                if (b.size.x >= b.size.y && b.size.x >= b.size.z)
                    stand = Quaternion.Euler(0f, 0f, 90f);
                else if (b.size.z >= b.size.y && b.size.z >= b.size.x)
                    stand = Quaternion.Euler(90f, 0f, 0f);
            }
            t.localRotation = Quaternion.Euler(0f, p.Yaw, 0f) * stand;
            TryGetBounds(root, inst, out b);

            float scale = 1f;

            // 1) Целевая высота (доля высоты финала).
            if (p.Height > 0f && b.size.y > 0.0001f)
                scale = Rescale(root, inst, p.Height * dims.Height / b.size.y, scale, out b);

            // 2) Ограничение по горизонтальной стороне (доли R = sqrt(W·D)), только уменьшает.
            float reference = Mathf.Sqrt(dims.Width * dims.Depth);
            float side = Mathf.Max(b.size.x, b.size.z);
            float cap = p.MaxSide * reference;
            if (p.MaxSide > 0f && side > cap && side > 0.0001f)
                scale = Rescale(root, inst, cap / side, scale, out b);

            // 3) Не выступать за плиту больше чем на SlabOvershoot (по обеим осям).
            float fitX = b.size.x > 0.0001f ? (dims.Width + 2f * SlabOvershoot) / b.size.x : 1f;
            float fitZ = b.size.z > 0.0001f ? (dims.Depth + 2f * SlabOvershoot) / b.size.z : 1f;
            float fit = Mathf.Min(fitX, fitZ);
            if (fit < 1f)
                scale = Rescale(root, inst, fit, scale, out b);

            // 4) Потолок стадии: верх детали не выше capTop (фундамент < каркас < финал).
            float allowedHeight = Mathf.Max(capTop - groundY, MinPartHeight);
            if (b.size.y > allowedHeight && b.size.y > 0.0001f)
                scale = Rescale(root, inst, allowedHeight / b.size.y, scale, out b);

            // Центр габарита — в заданную точку (доли W и D), прижатую внутрь плиты так, чтобы деталь
            // не выступала дальше SlabOvershoot; низ — на верх плиты (b посчитан при localPosition = 0).
            float targetX = p.X * dims.Width;
            float targetZ = p.Z * dims.Depth;
            float limitX = Mathf.Max(0f, dims.Width * 0.5f + SlabOvershoot - b.size.x * 0.5f);
            float limitZ = Mathf.Max(0f, dims.Depth * 0.5f + SlabOvershoot - b.size.z * 0.5f);
            float clampedX = Mathf.Clamp(targetX, -limitX, limitX);
            float clampedZ = Mathf.Clamp(targetZ, -limitZ, limitZ);
            if (!Mathf.Approximately(clampedX, targetX) || !Mathf.Approximately(clampedZ, targetZ))
                Debug.Log("[Editor] Деталь '" + asset.name + "': точка (" + F2(targetX) + ", " + F2(targetZ) +
                          ") прижата к плите -> (" + F2(clampedX) + ", " + F2(clampedZ) + ").");

            t.localPosition = new Vector3(clampedX - b.center.x, groundY - b.min.y, clampedZ - b.center.z);
            TryGetBounds(root, inst, out b);

            Debug.Log("[Editor] Деталь '" + asset.name + "' (" + F2(clampedX) + ", " + F2(clampedZ) +
                      "), scale " + scale.ToString("F2", CultureInfo.InvariantCulture) + ", размер " + b.size.ToString("F2") + ".");
            return true;
        }

        /// <summary>Умножает uniform-масштаб детали на factor и пересчитывает bounds (при localPosition = 0).</summary>
        private static float Rescale(Transform root, GameObject inst, float factor, float currentScale, out Bounds bounds)
        {
            float scale = currentScale * factor;
            inst.transform.localScale = Vector3.one * scale;
            TryGetBounds(root, inst, out bounds);
            return scale;
        }

        // ── Описание стадий ─────────────────────────────────────────────────────
        // X и Z — доли ширины W и глубины D плиты; MaxSide — доли R = sqrt(W·D); Height — доли высоты финала.

        private static Part[] GetParts(BuildingId id, int stage)
        {
            switch (id)
            {
                case BuildingId.HighJumpTower:
                    return stage == 1
                        ? new[]
                        {
                            new Part(Prop("SM_Prop_PalletStack_01"), -0.35f, -0.35f),
                            new Part(Prop("SM_Prop_Brick_Stack_01"),  0.35f, -0.35f, 90f),
                            new Part(Prop("SM_Prop_Cone_01"),         0.35f,  0.35f),
                            new Part(Prop("SM_Prop_Cone_01"),         0.22f,  0.40f),
                            new Part(Prop("SM_Prop_Plank_Stack_01"), -0.35f,  0.35f, 90f),
                        }
                        : new[]
                        {
                            new Part(Prop("SM_Prop_Scaffold_Preset_01"),   -0.28f,  0.10f, 0f,   0.35f),
                            new Part(Prop("SM_Prop_Scaffold_Preset_01"),    0.28f,  0.10f, 180f, 0.35f),
                            // Кран «встаёт» вертикально; высота 0.7 финала (потолок стадии — 0.85 финала).
                            new Part(Prop("SM_Prop_Crane_Section_01"),      0f,    -0.15f, 0f,   0.30f, 0.70f, true),
                            new Part(Prop("SM_Prop_Plank_Long_Stack_01"),   0f,     0.40f, 0f,   0.70f),
                        };

                case BuildingId.TicketBooth:
                    return stage == 1
                        ? new[]
                        {
                            new Part(Prop("SM_Prop_Brick_Stack_02"), -0.35f, -0.35f),
                            new Part(Prop("SM_Prop_Barrier_01"),     -0.15f,  0.40f),
                            new Part(Prop("SM_Prop_Barrier_01"),      0.15f,  0.40f),
                            new Part(Prop("SM_Prop_Cone_01"),         0.35f, -0.35f),
                        }
                        : new[]
                        {
                            new Part(Bld("SM_Bld_ConcreteFrame_Pillar_01"), -0.40f, -0.40f),
                            new Part(Bld("SM_Bld_ConcreteFrame_Pillar_01"),  0.40f, -0.40f),
                            new Part(Bld("SM_Bld_ConcreteFrame_Pillar_01"), -0.40f,  0.40f),
                            new Part(Bld("SM_Bld_ConcreteFrame_Pillar_01"),  0.40f,  0.40f),
                            new Part(Prop("SM_Prop_Scaffold_Preset_02_Tarp"), 0f,     0f, 0f, 0.50f),
                        };

                case BuildingId.Tribunes:
                    return stage == 1
                        ? new[]
                        {
                            new Part(Prop("SM_Prop_Pipe_Stack_01"),     -0.35f, -0.30f),
                            new Part(Prop("SM_Prop_PalletStack_02"),     0.35f, -0.30f),
                            new Part(Prop("SM_Prop_Cone_Barrier_01"),    0f,     0.38f),
                        }
                        : new[]
                        {
                            // Ряд лесов вдоль длинной стороны (локальная ось X здания).
                            new Part(Prop("SM_Prop_Scaffold_Preset_01"), -0.375f, 0.10f, 0f, 0.30f),
                            new Part(Prop("SM_Prop_Scaffold_Preset_01"), -0.125f, 0.10f, 0f, 0.30f),
                            new Part(Prop("SM_Prop_Scaffold_Preset_01"),  0.125f, 0.10f, 0f, 0.30f),
                            new Part(Prop("SM_Prop_Scaffold_Preset_01"),  0.375f, 0.10f, 0f, 0.30f),
                            new Part(Prop("SM_Prop_Plank_Stack_02"),      0f,    -0.35f, 90f),
                        };

                default:
                    Debug.LogWarning("[Editor] Для " + id + " нет описания стадий — только плита.");
                    return new Part[0];
            }
        }

        // ── Утилиты ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Bounds всех Renderer'ов <paramref name="target"/> в локальных осях <paramref name="space"/>
        /// (учитывает поворот/масштаб, не зависит от сцены). Частицы и TMP-тексты не учитываются —
        /// как в FitVisual (Build Base Hub Layout), иначе измеренный финал разойдётся с вписанным в сцене.
        /// </summary>
        private static bool TryGetBounds(Transform space, GameObject target, out Bounds bounds)
        {
            bounds = default;
            bool has = false;
            Matrix4x4 worldToSpace = space.worldToLocalMatrix;

            foreach (var r in target.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer || r.GetComponent<TMP_Text>() != null)
                    continue;

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

        /// <summary>Создаёт цепочку папок "Assets/A/B/C", если каких-то нет.</summary>
        private static void EnsureFolderPath(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
                return;

            var segments = new List<string>(folder.Split('/'));
            string current = segments[0];
            for (int i = 1; i < segments.Count; i++)
            {
                string next = current + "/" + segments[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, segments[i]);
                    Debug.Log("[Editor] Создана папка: " + next);
                }
                current = next;
            }
        }
    }
}
#endif
