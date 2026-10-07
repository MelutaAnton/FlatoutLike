#if UNITY_EDITOR
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using FlatOutLike.Data.Configs.Flow;
using FlatOutLike.Editor.Tools;
using FlatOutLike.Installers;
using FlatOutLike.Models;
using FlatOutLike.Views.UI.Common;
using Object = UnityEngine.Object;

namespace FlatOutLike.Editor.UI
{
    /// <summary>
    /// Шаг 11d: собирает экран загрузки между сценами (заменяет чёрный фейд шага 11c) —
    /// единый префаб <see cref="PrefabPath"/> + конфиг <see cref="ConfigPath"/> — и ставит
    /// connected-инстанс префаба в пять сцен (Boot, BaseHub, Garage, Derby_Arena_01,
    /// Stunts_HighJump), прошивая GameLifetimeScope._loadingScreenView/_loadingScreenConfig.
    /// Заодно сносит из сцен инстансы старого ScreenFadeCanvas и удаляет его префаб.
    ///
    /// Структура префаба: корень LoadingScreenCanvas (Canvas Overlay, CanvasScaler,
    /// GraphicRaycaster, CanvasGroup, LoadingScreenView) →
    ///   Background (тёмная заливка, единственный ловец кликов),
    ///   Title (Bungee), HintGroup (CanvasGroup) → HintText (RussoOne),
    ///   ProgressBar → Track + Fill (Filled/Horizontal/Left), PercentText.
    /// Корневой CanvasGroup всегда alpha=0 / interactable=false / blocksRaycasts=false —
    /// экран показывает рантайм, в редакторе Game view чистый.
    ///
    /// Идемпотентность: префаб есть → дособираем недостающее и перевыставляем инварианты;
    /// конфиг есть → значения дизайнера не трогаем, дописываем только отсутствующие
    /// места назначения и подсказки (если массив пуст); сцена с готовым инстансом →
    /// только перепрошиваем ссылки на scope.
    /// </summary>
    public static class BuildLoadingScreenEditor
    {
        public const string PrefabPath = "Assets/Development/Prefabs/UI/LoadingScreenCanvas.prefab";
        public const string ConfigFolder = "Assets/Development/Data/Configs/Flow";
        public const string ConfigPath = ConfigFolder + "/LoadingScreenConfig.asset";

        // Старый экран фейда (шаг 11c) — удаляется этим скриптом.
        private const string OldFadePrefabPath = "Assets/Development/Prefabs/UI/ScreenFadeCanvas.prefab";
        private const string OldFadeRootName = "ScreenFadeCanvas";

        private const string RootName = "LoadingScreenCanvas";

        /// <summary>Поверх PauseOverlay (60) и MobileInputCanvas (50) — перекрывает вообще всё.</summary>
        private const int SortingOrder = 100;

        // Имена дочерних узлов — единственное определение.
        private const string BackgroundName = "Background";
        private const string TitleName = "Title";
        private const string HintGroupName = "HintGroup";
        private const string HintTextName = "HintText";
        private const string ProgressBarName = "ProgressBar";
        private const string TrackName = "Track";
        private const string FillName = "Fill";
        private const string PercentTextName = "PercentText";

        // Имена полей по контракту шага 11d.
        private const string ScopeViewField = "_loadingScreenView";
        private const string ScopeConfigField = "_loadingScreenConfig";

        // Палитра: фон — тёмный почти чёрный; акцент прогресса — жёлтый HUD
        // (как YellowColor в BuildHighJumpHudEditor).
        private static readonly Color BackgroundColor = new Color32(0x0E, 0x11, 0x16, 0xFF);
        private static readonly Color TitleColor = Color.white;
        private static readonly Color HintColor = new Color(0.78f, 0.80f, 0.84f, 1f);
        private static readonly Color TrackColor = new Color(0.16f, 0.17f, 0.20f, 1f);
        private static readonly Color FillColor = new Color(1f, 0.85f, 0.1f, 1f);
        private static readonly Color PercentColor = new Color(0.92f, 0.92f, 0.92f, 1f);

        // Встроенный Unity-спрайт (скруглённый, 9-slice): подходит и для Sliced-трека, и для
        // Filled-заливки. Спрайты UI-пака (nos bar) не подходят — изогнутые сегменты/с иконкой.
        private const string BuiltinSpriteName = "UI/Skin/UISprite.psd";

        private static readonly string[] SceneKeysToProcess =
        {
            SceneKeys.Boot,
            SceneKeys.BaseHub,
            SceneKeys.Garage,
            SceneKeys.DerbyArena01,
            SceneKeys.StuntsHighJump,
        };

        private struct DefaultDestination
        {
            public GameState State;
            public string Title;
            public float MinSeconds;
        }

        private static readonly DefaultDestination[] DefaultDestinations =
        {
            new DefaultDestination { State = GameState.BaseHub, Title = "HOME BASE", MinSeconds = 0.8f },
            new DefaultDestination { State = GameState.GarageMeta, Title = "GARAGE", MinSeconds = 0.8f },
            new DefaultDestination { State = GameState.DerbyMatch, Title = "DERBY ARENA", MinSeconds = 2f },
            new DefaultDestination { State = GameState.StuntsHighJumpDriving, Title = "HIGH JUMP", MinSeconds = 2f },
        };

        private static readonly string[] DefaultHints =
        {
            "Ram at full speed — faster hits deal more damage.",
            "Save nitro for ramming, not just for speed.",
            "Car wrecked? Eject and watch the chaos from above.",
            "Upgrade armor in the Garage to survive longer in the Derby.",
            "In High Jump, your speed at the ramp edge decides the height.",
            "Grab repair pickups when your car starts smoking.",
            "Stand in a launch zone to start a mode.",
            "The last car standing takes the biggest reward.",
            "In High Jump only height counts — aim for the sky.",
            "Handbrake turns help you swing around for a ram.",
            "Each upgrade is tied to its car — choose your favorite.",
            "Grab nitro pickups to refill your boost.",
        };

        [MenuItem("FlatOutLike/UI/Build Loading Screen (All Scenes)")]
        public static void BuildAllScenes()
        {
            // Открытие сцен в режиме Single выгрузит несохранённые изменения — спрашиваем заранее.
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log("[Editor] Build Loading Screen отменён: сохранение сцен не подтверждено.");
                return;
            }

            string originalScenePath = SceneManager.GetActiveScene().path;

            EnsureConfig();
            BuildOrUpdatePrefab();

            int processed = 0;
            foreach (var key in SceneKeysToProcess)
            {
                if (WireScene(key))
                    processed++;
            }

            // Возвращаем сцену, в которой пользователь был до запуска.
            if (!string.IsNullOrEmpty(originalScenePath)
                && SceneManager.GetActiveScene().path != originalScenePath)
            {
                EditorSceneManager.OpenScene(originalScenePath, OpenSceneMode.Single);
                Debug.Log("[Editor] Возвращена исходная сцена " + originalScenePath + ".");
            }

            // Старый префаб фейда больше не нужен — сцены от него уже очищены.
            if (AssetDatabase.LoadAssetAtPath<GameObject>(OldFadePrefabPath) != null)
            {
                if (AssetDatabase.DeleteAsset(OldFadePrefabPath))
                    Debug.Log("[Editor] Удалён старый префаб фейда: " + OldFadePrefabPath + ".");
                else
                    Debug.LogWarning("[Editor] Не удалось удалить старый префаб фейда: " + OldFadePrefabPath + ".");
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[Editor] Build Loading Screen завершён: обработано сцен " + processed
                      + " из " + SceneKeysToProcess.Length + ".");
        }

        // ── Конфиг ─────────────────────────────────────────────────────────────

        private static void EnsureConfig()
        {
            EnsureFolder("Assets/Development/Data/Configs", "Flow");

            var config = AssetDatabase.LoadAssetAtPath<LoadingScreenConfig>(ConfigPath);
            if (config == null)
            {
                // Числа не пишем: CreateInstance уже даёт инициализаторы полей SO —
                // единственный источник дефолтов длительностей и прогресса.
                config = ScriptableObject.CreateInstance<LoadingScreenConfig>();
                AssetDatabase.CreateAsset(config, ConfigPath);
                Debug.Log("[Editor] Создан конфиг: " + ConfigPath + ".");
            }

            var so = new SerializedObject(config);

            FillDestinations(so);
            FillHints(so);

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
        }

        /// <summary>Дописывает отсутствующие (по State) записи _destinations; существующие не трогает.</summary>
        private static void FillDestinations(SerializedObject so)
        {
            var list = so.FindProperty("_destinations");
            if (list == null || !list.isArray)
            {
                Debug.LogWarning("[Editor] У LoadingScreenConfig нет списка _destinations — места назначения не заполнены.");
                return;
            }

            foreach (var def in DefaultDestinations)
            {
                if (HasDestination(list, def.State))
                    continue;

                // arraySize++ зануляет field-инициализаторы нового элемента — заполняем ВСЕ поля явно.
                int index = list.arraySize;
                list.arraySize = index + 1;
                var element = list.GetArrayElementAtIndex(index);

                var stateProp = FindEntryField(element, "State");
                var titleProp = FindEntryField(element, "Title");
                var minProp = FindEntryField(element, "MinSeconds");
                if (stateProp == null || titleProp == null || minProp == null)
                {
                    Debug.LogWarning("[Editor] Не найдены поля записи LoadingDestinationEntry (State/Title/MinSeconds) — запись "
                                     + def.State + " не заполнена.");
                    continue;
                }

                // У GameState явные значения с дыркой — enumValueIndex нельзя, пишем число.
                stateProp.intValue = (int)def.State;
                titleProp.stringValue = def.Title;
                minProp.floatValue = def.MinSeconds;
                Debug.Log("[Editor] В конфиг добавлено место назначения " + def.State + " (\"" + def.Title + "\").");
            }
        }

        private static bool HasDestination(SerializedProperty list, GameState state)
        {
            for (int i = 0; i < list.arraySize; i++)
            {
                var stateProp = FindEntryField(list.GetArrayElementAtIndex(i), "State");
                if (stateProp != null && stateProp.intValue == (int)state)
                    return true;
            }

            return false;
        }

        /// <summary>Поле записи: обычное поле или авто-свойство с [field: SerializeField].</summary>
        private static SerializedProperty FindEntryField(SerializedProperty element, string name)
        {
            var prop = element.FindPropertyRelative(name);
            if (prop == null)
                prop = element.FindPropertyRelative("<" + name + ">k__BackingField");
            return prop;
        }

        private static void FillHints(SerializedObject so)
        {
            var hints = so.FindProperty("_hints");
            if (hints == null || !hints.isArray)
            {
                Debug.LogWarning("[Editor] У LoadingScreenConfig нет массива _hints — подсказки не заполнены.");
                return;
            }

            if (hints.arraySize > 0)
                return; // подсказки дизайнера не перезаписываем

            hints.arraySize = DefaultHints.Length;
            for (int i = 0; i < DefaultHints.Length; i++)
                hints.GetArrayElementAtIndex(i).stringValue = DefaultHints[i];

            Debug.Log("[Editor] В конфиг записаны подсказки по умолчанию: " + DefaultHints.Length + ".");
        }

        // ── Префаб ─────────────────────────────────────────────────────────────

        private static void BuildOrUpdatePrefab()
        {
            EnsureFolder("Assets/Development/Prefabs", "UI");

            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
            {
                var contents = PrefabUtility.LoadPrefabContents(PrefabPath);
                try
                {
                    ConfigureRoot(contents);
                    PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
                    Debug.Log("[Editor] Префаб обновлён: " + PrefabPath + ".");
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(contents);
                }
            }
            else
            {
                var temp = new GameObject(RootName, typeof(RectTransform));
                try
                {
                    ConfigureRoot(temp);
                    PrefabUtility.SaveAsPrefabAsset(temp, PrefabPath);
                    Debug.Log("[Editor] Префаб создан: " + PrefabPath + ".");
                }
                finally
                {
                    Object.DestroyImmediate(temp);
                }
            }

            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Доукомплектовывает корень и все дочерние узлы. Работает и на временном GameObject,
        /// и на содержимом префаба (поэтому без Undo — он неприменим к prefab contents).
        /// </summary>
        private static void ConfigureRoot(GameObject root)
        {
            // ── Canvas / Scaler / Raycaster ──
            var canvas = GetOrAdd<Canvas>(root);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;

            // Настройки скейлера — как у остальных канвасов проекта.
            var scaler = GetOrAdd<CanvasScaler>(root);
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            GetOrAdd<GraphicRaycaster>(root);

            // ── CanvasGroup: инвариант — невидим и не блокирует клики, показывает рантайм ──
            var group = GetOrAdd<CanvasGroup>(root);
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;

            var uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>(BuiltinSpriteName);
            if (uiSprite == null)
                Debug.LogWarning("[Editor] Встроенный спрайт " + BuiltinSpriteName
                                 + " не найден — прогресс-бар не сможет заполняться (Filled требует спрайт).");

            var displayFont = UIBuildHelpers.LoadFont(UIBuildHelpers.DisplayFontPath);
            var bodyFont = UIBuildHelpers.LoadFont(UIBuildHelpers.BodyFontPath);

            // ── Background: единственный ловец кликов ──
            var bgRt = GetOrCreateChild(root.transform, BackgroundName);
            UIBuildHelpers.Stretch(bgRt);
            bgRt.SetAsFirstSibling();
            var bg = GetOrAdd<Image>(bgRt.gameObject);
            bg.sprite = null;
            bg.color = BackgroundColor;
            bg.raycastTarget = true;
            bg.maskable = false;
            // Vignette пропущена: подходящего спрайта в UI-паке нет (circle*.png — иконки).

            // ── Title: верхняя треть ──
            var titleRt = GetOrCreateChild(root.transform, TitleName);
            UIBuildHelpers.Anchor(titleRt, new Vector2(0.5f, 0.67f), new Vector2(0.5f, 0.67f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1400f, 150f));
            var title = ConfigureText(titleRt.gameObject, "LOADING", displayFont, 96f, TitleColor,
                TextAlignmentOptions.Center, false);

            // ── HintGroup → HintText: над прогресс-баром ──
            var hintGroupRt = GetOrCreateChild(root.transform, HintGroupName);
            UIBuildHelpers.Anchor(hintGroupRt, new Vector2(0.5f, 0.15f), new Vector2(0.5f, 0.15f),
                new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(1344f, 150f));
            var hintGroup = GetOrAdd<CanvasGroup>(hintGroupRt.gameObject);
            hintGroup.alpha = 1f;
            hintGroup.interactable = false;
            hintGroup.blocksRaycasts = false;

            var hintRt = GetOrCreateChild(hintGroupRt, HintTextName);
            UIBuildHelpers.Stretch(hintRt);
            var hint = ConfigureText(hintRt.gameObject, string.Empty, bodyFont, 40f, HintColor,
                TextAlignmentOptions.Bottom, true);

            // ── ProgressBar: ~15% от низа, 60% ширины, высота 36 ──
            var barRt = GetOrCreateChild(root.transform, ProgressBarName);
            UIBuildHelpers.Anchor(barRt, new Vector2(0.5f, 0.15f), new Vector2(0.5f, 0.15f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1152f, 36f));

            var trackRt = GetOrCreateChild(barRt, TrackName);
            UIBuildHelpers.Stretch(trackRt);
            var track = GetOrAdd<Image>(trackRt.gameObject);
            track.sprite = uiSprite;
            track.type = Image.Type.Sliced;
            track.color = TrackColor;
            track.raycastTarget = false;
            track.maskable = false;

            var fillRt = GetOrCreateChild(trackRt, FillName);
            UIBuildHelpers.Stretch(fillRt);
            fillRt.offsetMin = new Vector2(4f, 4f);
            fillRt.offsetMax = new Vector2(-4f, -4f);
            var fill = GetOrAdd<Image>(fillRt.gameObject);
            fill.sprite = uiSprite;
            fill.color = FillColor;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 0f;
            fill.raycastTarget = false;
            fill.maskable = false;

            // ── PercentText: под баром ──
            var percentRt = GetOrCreateChild(barRt, PercentTextName);
            UIBuildHelpers.Anchor(percentRt, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(300f, 52f));
            var percent = ConfigureText(percentRt.gameObject, "0%", bodyFont, 36f, PercentColor,
                TextAlignmentOptions.Top, false);

            // ── LoadingScreenView + прошивка ссылок ──
            var view = GetOrAdd<LoadingScreenView>(root);
            var so = new SerializedObject(view);
            SetRef(so, "_canvasGroup", group);
            SetRef(so, "_titleText", title);
            SetRef(so, "_hintText", hint);
            SetRef(so, "_hintGroup", hintGroup);
            SetRef(so, "_progressFill", fill);
            SetRef(so, "_percentText", percent);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static TMP_Text ConfigureText(GameObject go, string text, TMP_FontAsset font, float size,
            Color color, TextAlignmentOptions alignment, bool wrap)
        {
            var tmp = GetOrAdd<TextMeshProUGUI>(go);
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = alignment;
            tmp.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            tmp.enableAutoSizing = false;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.raycastTarget = false;
            tmp.maskable = false;
            if (font != null) tmp.font = font;
            return tmp;
        }

        private static void SetRef(SerializedObject so, string propName, Object value)
        {
            var prop = so.FindProperty(propName);
            if (prop == null)
            {
                Debug.LogWarning("[Editor] У LoadingScreenView нет поля " + propName + " — ссылка не прошита.");
                return;
            }

            prop.objectReferenceValue = value;
        }

        private static T GetOrAdd<T>(GameObject go) where T : Component
        {
            var comp = go.GetComponent<T>();
            if (comp == null) comp = go.AddComponent<T>();
            return comp;
        }

        /// <summary>Дочерний RectTransform по имени (без Undo — работаем и на содержимом префаба).</summary>
        private static RectTransform GetOrCreateChild(Transform parent, string name)
        {
            var existing = parent.Find(name);
            if (existing != null)
                return existing as RectTransform;

            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        // ── Сцены ──────────────────────────────────────────────────────────────

        private static bool WireScene(string sceneKey)
        {
            string path = ScenePaths.For(sceneKey);
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
            {
                Debug.LogWarning("[Editor] Сцена не найдена, пропускаем: " + path);
                return false;
            }

            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            Debug.Log("[Editor] Открыта сцена " + path + ".");

            // Ассеты грузим ПОСЛЕ OpenScene — открытие выгружает неиспользуемые ассеты.
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var config = AssetDatabase.LoadAssetAtPath<LoadingScreenConfig>(ConfigPath);
            if (prefab == null)
            {
                Debug.LogWarning("[Editor] Префаб не загружен: " + PrefabPath + " — сцена " + sceneKey + " пропущена.");
                return false;
            }

            RemoveOldFade(scene, sceneKey);

            LoadingScreenView instance = FindInstance(prefab);
            if (instance == null)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                Undo.RegisterCreatedObjectUndo(go, "Instantiate LoadingScreenCanvas");
                instance = go.GetComponent<LoadingScreenView>();
                Debug.Log("[Editor] LoadingScreenCanvas инстанцирован в " + sceneKey + ".");
            }
            else
            {
                Debug.Log("[Editor] LoadingScreenCanvas уже есть в " + sceneKey + ", переиспользуем.");
            }

            if (instance == null)
                Debug.LogWarning("[Editor] На инстансе нет LoadingScreenView — прошивка scope пропущена в " + sceneKey + ".");
            else
                WireScope(instance, config, sceneKey);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Editor] Сцена " + sceneKey + " сохранена.");
            return true;
        }

        /// <summary>
        /// Сносит корневые инстансы старого ScreenFadeCanvas: по пути префаба либо по имени
        /// (скрипт ScreenFadeView к этому моменту может быть удалён — компонент висит Missing).
        /// </summary>
        private static void RemoveOldFade(Scene scene, string sceneKey)
        {
            var toDelete = new List<GameObject>();
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root == null) continue;

                bool byPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root) == OldFadePrefabPath;
                bool byName = root.name == OldFadeRootName;
                if (byPath || byName)
                    toDelete.Add(root);
            }

            foreach (var go in toDelete)
            {
                Undo.DestroyObjectImmediate(go);
                Debug.Log("[Editor] Удалён старый ScreenFadeCanvas из " + sceneKey + ".");
            }
        }

        /// <summary>
        /// Ищет в открытой сцене инстанс именно нашего префаба (по LoadingScreenView). Если их
        /// несколько — предупреждаем, ничего не удаляем, возвращаем первый.
        /// </summary>
        private static LoadingScreenView FindInstance(GameObject prefab)
        {
            var all = Object.FindObjectsByType<LoadingScreenView>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            LoadingScreenView first = null;
            int count = 0;
            foreach (var view in all)
            {
                if (view == null) continue;
                if (PrefabUtility.GetCorrespondingObjectFromSource(view.gameObject) != prefab) continue;

                count++;
                if (first == null) first = view;
            }

            if (count > 1)
                Debug.LogWarning("[Editor] В сцене найдено " + count + " инстансов LoadingScreenCanvas — удалите лишние вручную; используется первый.");

            return first;
        }

        private static void WireScope(LoadingScreenView instance, LoadingScreenConfig config, string sceneKey)
        {
            var scope = Object.FindAnyObjectByType<GameLifetimeScope>(FindObjectsInactive.Include);
            if (scope == null)
            {
                Debug.LogWarning("[Editor] GameLifetimeScope не найден в " + sceneKey + " — ссылки экрана загрузки не прошиты.");
                return;
            }

            var so = new SerializedObject(scope);

            var viewProp = so.FindProperty(ScopeViewField);
            if (viewProp == null)
            {
                Debug.LogWarning("[Editor] У GameLifetimeScope нет поля " + ScopeViewField + " (сцена " + sceneKey + ") — ничего не присвоено.");
            }
            else
            {
                viewProp.objectReferenceValue = instance;
                Debug.Log("[Editor] GameLifetimeScope." + ScopeViewField + " прошит в " + sceneKey + ".");
            }

            var configProp = so.FindProperty(ScopeConfigField);
            if (configProp == null)
            {
                Debug.LogWarning("[Editor] У GameLifetimeScope нет поля " + ScopeConfigField + " (сцена " + sceneKey + ") — ничего не присвоено.");
            }
            else if (config == null)
            {
                // null не присваиваем — иначе повторный прогон ломает рабочую проводку.
                Debug.LogWarning("[Editor] Конфиг не загружен: " + ConfigPath + " — " + ScopeConfigField + " не прошит в " + sceneKey + ".");
            }
            else
            {
                configProp.objectReferenceValue = config;
                Debug.Log("[Editor] GameLifetimeScope." + ScopeConfigField + " прошит в " + sceneKey + ".");
            }

            so.ApplyModifiedProperties();
        }

        private static void EnsureFolder(string parent, string folderName)
        {
            string full = parent + "/" + folderName;
            if (!AssetDatabase.IsValidFolder(full))
                AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}
#endif
