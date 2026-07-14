#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using FlatOutLike.Controllers.Mobile;
using FlatOutLike.Installers;
using Object = UnityEngine.Object;

namespace FlatOutLike.Editor.UI
{
    /// <summary>
    /// Шаг 9: собирает мобильный input-канвас (виртуальные джойстики + hold-кнопки) для
    /// вождения (DrivingGroup: LeftStick только для руления + педали Gas/Brake +
    /// Handbrake/Nitro/Eject) и полёта (FlightGroup: RightStick + Boost). PauseButton
    /// один на канвасе — видна в обоих контекстах, показ/скрытие групп делает рантайм
    /// (MobileInputCanvas), не editor-скрипт.
    ///
    /// Схема управления (решение пользователя, актуально с шага «педали + стик руления»):
    /// Move.x = LeftStick.x (только руление), Move.y = газ(+1 с GasButton) / тормоз(−1
    /// с BrakeButton) — сборка вектора теперь в MobileInputCanvas.Update, не в этом скрипте.
    ///
    /// Запускается на КАЖДОЙ геймплейной сцене отдельно (канвас — часть сцены, не
    /// префаб-синглтон уровня Boot); НЕ открывает другие сцены программно.
    ///
    /// Порядок: после FlotOutLike/Tools/Generate TMP Font Assets (шрифты не используются
    /// напрямую этим скриптом, но UIBuildHelpers.CreateLabel их подхватывает при желании
    /// добавить подписи — сейчас подписи не нужны, иконки самодостаточны).
    ///
    /// Runtime-типы (FlatOutLike.Controllers.Mobile.*) пишутся параллельно другим агентом —
    /// на момент написания этого скрипта их ещё нет в проекте, поэтому здесь скрипт
    /// компилируется ТОЛЬКО после появления VirtualJoystickView / HoldButtonView /
    /// MobileInputCanvas и поля GameLifetimeScope._mobileInputCanvas.
    /// </summary>
    public static class BuildMobileInputCanvasEditor
    {
        private const string UiPackButtons = UIBuildHelpers.UiPackRoot + "/Buttons/png";
        private const string UiPackIcons   = UIBuildHelpers.UiPackRoot + "/Icons/png";

        // Джойстик — круглая подложка + ручка (Racing Game Ui Pack, Icons/png).
        private const string JoystickBgSprite     = UiPackIcons + "/circle empty.png";
        private const string JoystickHandleSprite = UiPackIcons + "/circle bar.png";

        // Педали вождения (новая схема: газ/тормоз отдельными кнопками, руление — только LeftStick.x).
        private const string GasSprite   = UiPackButtons + "/gas pedal.png";
        private const string BrakeSprite = UiPackButtons + "/brake.png";

        // Кнопки вождения.
        private const string HandbrakeSprite = UiPackButtons + "/hand brake.png";
        private const string NitroSprite     = UiPackButtons + "/nos button.png";
        // Отдельной иконки "eject"/катапульта в паке нет — берём стрелку вверх как
        // семантически ближайшую замену (отклонение от буквального контракта, см. отчёт).
        private const string EjectSprite = UiPackButtons + "/up.png";

        // Кнопка полёта. "gas pedal.png" теперь занята педалью газа в DrivingGroup —
        // для буста берём вторую иконку нитро-кнопки из пака (nos button2.png), она не
        // используется больше нигде, конфликта с NitroButton (nos button.png) нет.
        private const string BoostSprite = UiPackButtons + "/nos button2.png";

        // Пауза — отдельной иконки паузы в паке нет, берём "options" (шестерёнка/меню)
        // как ближайшую по смыслу замену (отклонение от буквального контракта).
        private const string PauseSprite = UiPackIcons + "/options.png";

        // Полупрозрачный тёмный tint — у пака белые ассеты, без затемнения кнопки
        // сливаются с ярким геймплейным фоном (см. UIBuildHelpers.DarkTextColor комментарий).
        private static readonly Color ButtonTint = new Color(0.08f, 0.08f, 0.1f, 0.75f);
        private static readonly Color StickBgTint = new Color(1f, 1f, 1f, 0.55f);
        private static readonly Color StickHandleTint = new Color(1f, 1f, 1f, 0.9f);

        // Палитра стилизации hold-кнопок — семантика функций, не декоративный выбор.
        private static readonly Color GasColor       = new Color(0.18f, 0.62f, 0.28f, 0.85f); // зелёный «газ»
        private static readonly Color BrakeColor     = new Color(0.78f, 0.22f, 0.18f, 0.85f); // красный «тормоз»
        private static readonly Color HandbrakeColor = new Color(0.87f, 0.62f, 0.12f, 0.85f); // янтарный «ручник»
        private static readonly Color NitroColor     = new Color(0.12f, 0.55f, 0.80f, 0.85f); // сине-голубой «ускорение»
        private static readonly Color BoostColor     = NitroColor; // Boost — та же семантика ускорения, что и Nitro.
        private static readonly Color EjectColor     = new Color(0.58f, 0.28f, 0.72f, 0.85f); // фиолетовый «особое действие» — осознанно не красный, чтобы не путать с тормозом.
        private static readonly Color PauseColor     = ButtonTint; // паузу по семантике функции не перекрашиваем.

        private const string PrefabPath = "Assets/Development/Prefabs/UI/MobileInputCanvas.prefab";

        [MenuItem("FlatOutLike/UI/Build Mobile Input Canvas")]
        public static void BuildMobileInputCanvas()
        {
            Undo.SetCurrentGroupName("Build Mobile Input Canvas (Step 9)");
            int undoGroup = Undo.GetCurrentGroup();

            // Идемпотентность — ищем ИМЕННО компонент MobileInputCanvas, не по имени GameObject.
            var existing = Object.FindAnyObjectByType<MobileInputCanvas>();
            GameObject canvasGo = existing != null ? existing.gameObject : null;

            if (canvasGo == null)
            {
                canvasGo = new GameObject("MobileInputCanvas",
                    typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                Undo.RegisterCreatedObjectUndo(canvasGo, "Create MobileInputCanvas");
                Debug.Log("[Editor] Создан MobileInputCanvas.");
            }
            else
            {
                Debug.Log("[Editor] MobileInputCanvas уже есть на сцене, переиспользуем.");
            }

            var canvas = canvasGo.GetComponent<Canvas>();
            if (canvas == null) canvas = Undo.AddComponent<Canvas>(canvasGo);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Выше HUD (BuildDerbyHudEditor/BuildHighJumpHudEditor кладут свои Canvas'ы
            // с дефолтным sortingOrder=0) — мобильный ввод должен ловить тапы поверх всего.
            canvas.sortingOrder = 50;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = Undo.AddComponent<CanvasScaler>(canvasGo);
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            if (canvasGo.GetComponent<GraphicRaycaster>() == null)
                Undo.AddComponent<GraphicRaycaster>(canvasGo);

            var root = canvasGo.transform;

            // ── DrivingGroup ─────────────────────────────────────────────────
            var drivingGroup = UIBuildHelpers.FindOrCreateChild(root, "DrivingGroup");
            UIBuildHelpers.Stretch(drivingGroup);

            var leftStick = CreateJoystick(drivingGroup, "LeftStick",
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(60f, 60f));

            // Педали — низ-право экрана. GasButton в самом углу (крупнее, основной палец),
            // BrakeButton слева от неё (чуть меньше). Обе кнопки заметно ниже столбика
            // Handbrake/Nitro/Eject, который сдвинут выше, чтобы не перекрываться с педалями.
            var gasBtn = CreateHoldButton(drivingGroup, "GasButton", GasSprite,
                new Vector2(1f, 0f), new Vector2(-140f, 140f), 220f);
            var brakeBtn = CreateHoldButton(drivingGroup, "BrakeButton", BrakeSprite,
                new Vector2(1f, 0f), new Vector2(-360f, 120f), 180f);

            // Столбик Handbrake/Nitro/Eject поднят выше педалей (педали занимают y до ~250,
            // столбик стартует с y=350 — запас 100px, чтобы не перекрываться на любом aspect ratio).
            var handbrakeBtn = CreateHoldButton(drivingGroup, "HandbrakeButton", HandbrakeSprite,
                new Vector2(1f, 0f), new Vector2(-60f, 350f), 160f);
            var nitroBtn = CreateHoldButton(drivingGroup, "NitroButton", NitroSprite,
                new Vector2(1f, 0f), new Vector2(-60f, 530f), 160f);
            var ejectBtn = CreateHoldButton(drivingGroup, "EjectButton", EjectSprite,
                new Vector2(1f, 0f), new Vector2(-60f, 710f), 160f);

            // ── FlightGroup ──────────────────────────────────────────────────
            var flightGroup = UIBuildHelpers.FindOrCreateChild(root, "FlightGroup");
            UIBuildHelpers.Stretch(flightGroup);

            var rightStick = CreateJoystick(flightGroup, "RightStick",
                new Vector2(1f, 0f), new Vector2(-60f, 0f), new Vector2(60f, 60f));

            var boostBtn = CreateHoldButton(flightGroup, "BoostButton", BoostSprite,
                new Vector2(0f, 0f), new Vector2(60f, 60f), 200f);

            // ── PauseButton (общий, на корне канваса — виден в обоих контекстах) ─
            var pauseBtn = CreateHoldButton(root, "PauseButton", PauseSprite,
                new Vector2(1f, 1f), new Vector2(-60f, -60f), 100f);

            // ── Стилизация hold-кнопок: transition ColorTint + перекраска Image ─
            // Перекрывает нейтральный ButtonTint, который CreateHoldButton выше поставил
            // всем Image — делается ПОСЛЕ создания всех кнопок и ДО сохранения префаба,
            // чтобы пересборка канваса не теряла стиль.
            StyleHoldButton(gasBtn.gameObject, GasColor);
            StyleHoldButton(brakeBtn.gameObject, BrakeColor);
            StyleHoldButton(handbrakeBtn.gameObject, HandbrakeColor);
            StyleHoldButton(nitroBtn.gameObject, NitroColor);
            StyleHoldButton(ejectBtn.gameObject, EjectColor);
            StyleHoldButton(boostBtn.gameObject, BoostColor);
            StyleHoldButton(pauseBtn.gameObject, PauseColor);

            // ── Компонент MobileInputCanvas + прошивка полей ────────────────────
            var mic = canvasGo.GetComponent<MobileInputCanvas>();
            if (mic == null) mic = Undo.AddComponent<MobileInputCanvas>(canvasGo);

            var so = new SerializedObject(mic);
            SetRef(so, "_leftStick", leftStick.GetComponent<VirtualJoystickView>());
            SetRef(so, "_rightStick", rightStick.GetComponent<VirtualJoystickView>());
            SetRef(so, "_drivingGroup", drivingGroup.gameObject);
            SetRef(so, "_flightGroup", flightGroup.gameObject);
            SetRef(so, "_gasButton", gasBtn.GetComponent<HoldButtonView>());
            SetRef(so, "_brakeButton", brakeBtn.GetComponent<HoldButtonView>());
            SetRef(so, "_handbrakeButton", handbrakeBtn.GetComponent<HoldButtonView>());
            SetRef(so, "_nitroButton", nitroBtn.GetComponent<HoldButtonView>());
            SetRef(so, "_ejectButton", ejectBtn.GetComponent<HoldButtonView>());
            SetRef(so, "_boostButton", boostBtn.GetComponent<HoldButtonView>());
            SetRef(so, "_pauseButton", pauseBtn.GetComponent<HoldButtonView>());
            so.ApplyModifiedProperties();

            // ── Сохранить префаб, инстанс остаётся в сцене ──────────────────────
            EnsureFolder("Assets/Development/Prefabs", "UI");
            var existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            PrefabUtility.SaveAsPrefabAssetAndConnect(canvasGo, PrefabPath,
                InteractionMode.AutomatedAction);
            Debug.Log("[Editor] Префаб сохранён: " + PrefabPath +
                (existingPrefab == null ? " (создан)" : " (обновлён)"));

            // ── Прошивка GameLifetimeScope._mobileInputCanvas ────────────────────
            BindMobileInputCanvas(mic);

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Undo.CollapseUndoOperations(undoGroup);
            Debug.Log("[Editor] Mobile Input Canvas собран на сцене " +
                SceneManager.GetActiveScene().name + ".");
        }

        public static void BindMobileInputCanvas(MobileInputCanvas mic)
        {
            var scope = Object.FindAnyObjectByType<GameLifetimeScope>();
            if (scope == null)
            {
                Debug.LogWarning("[Editor] GameLifetimeScope не найден в активной сцене — " +
                                  "_mobileInputCanvas не прошит.");
                return;
            }

            var so = new SerializedObject(scope);
            var prop = so.FindProperty("_mobileInputCanvas");
            if (prop == null)
            {
                Debug.LogWarning("[Editor] Поле GameLifetimeScope._mobileInputCanvas не найдено " +
                                  "(рантайм ещё не скомпилирован?).");
                return;
            }

            prop.objectReferenceValue = mic;
            so.ApplyModifiedProperties();
            Debug.Log("[Editor] GameLifetimeScope._mobileInputCanvas прошит.");
        }

        [MenuItem("FlatOutLike/UI/Style Mobile Input Buttons")]
        public static void StyleMobileInputButtons()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
            {
                Debug.LogWarning("[Editor] " + PrefabPath + " не найден. Сначала запусти " +
                                  "'FlatOutLike/UI/Build Mobile Input Canvas'.");
                return;
            }

            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                StyleByName(root, "GasButton", GasColor);
                StyleByName(root, "BrakeButton", BrakeColor);
                StyleByName(root, "HandbrakeButton", HandbrakeColor);
                StyleByName(root, "NitroButton", NitroColor);
                StyleByName(root, "EjectButton", EjectColor);
                StyleByName(root, "BoostButton", BoostColor);
                StyleByName(root, "PauseButton", PauseColor);

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log("[Editor] Style Mobile Input Buttons: префаб сохранён — " + PrefabPath + ".");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void StyleByName(GameObject root, string childName, Color color)
        {
            var t = FindChildByName(root.transform, childName);
            if (t == null)
            {
                Debug.LogWarning("[Editor] Style Mobile Input Buttons: '" + childName +
                                  "' не найден среди потомков " + root.name + ".");
                return;
            }

            StyleHoldButton(t.gameObject, color);
        }

        private static Transform FindChildByName(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t != root && t.name == name)
                    return t;
            return null;
        }

        /// <summary>
        /// Настраивает hold-кнопку: цвет Image + Button.transition = ColorTint с
        /// targetGraphic на этот же Image (без targetGraphic tint не применяется).
        /// Мигрирует и уже добавленные вручную Button (пользователь мог настроить их
        /// по-разному), и создаёт недостающие — миграционная идемпотентность.
        /// </summary>
        private static void StyleHoldButton(GameObject btnGo, Color iconColor)
        {
            var image = btnGo.GetComponent<Image>();
            if (image == null)
            {
                Debug.LogWarning("[Editor] StyleHoldButton: на '" + btnGo.name + "' нет Image — пропускаем.");
                return;
            }
            image.color = iconColor;

            var btn = btnGo.GetComponent<Button>();
            if (btn == null) btn = btnGo.AddComponent<Button>();

            btn.transition = Selectable.Transition.ColorTint;
            btn.targetGraphic = image;

            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.white;
            colors.pressedColor = new Color(0.62f, 0.62f, 0.62f, 1f);
            colors.selectedColor = Color.white; // = normal, иначе после тапа кнопка залипает в selected-цвете.
            colors.disabledColor = new Color(1f, 1f, 1f, 0.4f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            btn.colors = colors;

            // Automatic-навигация на тач-HUD даёт неявные переходы фокуса/залипание selected.
            btn.navigation = new Navigation { mode = Navigation.Mode.None };

            Debug.Log("[Editor] Styled " + btnGo.name);
        }

        // ── Хелперы сборки ────────────────────────────────────────────────────

        private static RectTransform CreateJoystick(Transform parent, string name,
            Vector2 anchor, Vector2 anchoredPosition, Vector2 pivotSign)
        {
            var stickRt = UIBuildHelpers.FindOrCreateChild(parent, name);
            // pivotSign используется только как «направление» пивота относительно якоря
            // (0,0 = низ-лево, 1,0 = низ-право) — сам pivot всегда 0.5/0.5 у диска джойстика.
            UIBuildHelpers.Anchor(stickRt, anchor, anchor, anchor, anchoredPosition,
                new Vector2(300f, 300f));

            var bg = UIBuildHelpers.CreateImage(stickRt, "Background", JoystickBgSprite);
            var bgRt = (RectTransform)bg.transform;
            UIBuildHelpers.Stretch(bgRt);
            bg.type = Image.Type.Simple;
            bg.preserveAspect = true;
            bg.color = StickBgTint;

            var handle = UIBuildHelpers.CreateImage(bgRt, "Handle", JoystickHandleSprite);
            var handleRt = (RectTransform)handle.transform;
            UIBuildHelpers.Anchor(handleRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(120f, 120f));
            handle.type = Image.Type.Simple;
            handle.preserveAspect = true;
            handle.color = StickHandleTint;

            var view = stickRt.GetComponent<VirtualJoystickView>();
            if (view == null) view = Undo.AddComponent<VirtualJoystickView>(stickRt.gameObject);

            var so = new SerializedObject(view);
            SetRef(so, "_background", bgRt);
            SetRef(so, "_handle", handleRt);
            so.ApplyModifiedProperties();

            return stickRt;
        }

        private static RectTransform CreateHoldButton(Transform parent, string name,
            string spritePath, Vector2 anchor, Vector2 anchoredPosition, float size)
        {
            var btnRt = UIBuildHelpers.FindOrCreateChild(parent, name);
            UIBuildHelpers.Anchor(btnRt, anchor, anchor, anchor, anchoredPosition,
                new Vector2(size, size));

            var img = btnRt.GetComponent<Image>();
            if (img == null) img = Undo.AddComponent<Image>(btnRt.gameObject);
            img.sprite = UIBuildHelpers.LoadSprite(spritePath);
            img.type = Image.Type.Simple;
            img.preserveAspect = true;
            img.color = ButtonTint;

            if (btnRt.GetComponent<HoldButtonView>() == null)
                Undo.AddComponent<HoldButtonView>(btnRt.gameObject);

            return btnRt;
        }

        private static void EnsureFolder(string parent, string folderName)
        {
            string full = parent + "/" + folderName;
            if (!AssetDatabase.IsValidFolder(full))
                AssetDatabase.CreateFolder(parent, folderName);
        }

        private static void SetRef(SerializedObject so, string propName, Object value)
        {
            var prop = so.FindProperty(propName);
            if (prop != null)
                prop.objectReferenceValue = value;
            else
                Debug.LogWarning("[Editor] Поле " + so.targetObject.GetType().Name + "." + propName +
                                  " не найдено (рантайм ещё не скомпилирован?).");
        }
    }
}
#endif
