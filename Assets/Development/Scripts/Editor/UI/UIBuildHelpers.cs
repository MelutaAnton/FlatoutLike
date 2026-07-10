#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using Object = UnityEngine.Object;

namespace FlatOutLike.Editor.UI
{
    /// <summary>
    /// Общие идемпотентные хелперы сборки мета-UI (Шаг 8c): поиск/создание дочерних
    /// RectTransform, растяжка/якоря, картинки и TMP-тексты/кнопки из спрайтов пака
    /// "Racing Game Ui Pack" (Assets/ThirdParty/Racing Game Ui Pack/...), поиск/создание
    /// Canvas+EventSystem на сценах без готового UICanvas (арена).
    ///
    /// Не содержит [MenuItem] — переиспользуемая утилита (аналог CarPrefabDiscovery).
    /// Содержимое ThirdParty-пака НЕ редактируется (включая .meta/import-настройки!) —
    /// по правилу CLAUDE.md нужный спрайт КОПИРУЕТСЯ в Development/Art/UI/UiPack/,
    /// и Texture Type -> Sprite правится уже на копии. Обновление пака копий не тронет.
    /// </summary>
    public static class UIBuildHelpers
    {
        public const string FontsRoot    = "Assets/Development/Art/Fonts";
        public const string TmpFontsRoot = FontsRoot + "/TMP";
        public const string UiPackRoot   = "Assets/ThirdParty/Racing Game Ui Pack/Assets";
        /// <summary>Куда копируются используемые спрайты пака (наша папка, правки легальны).</summary>
        public const string UiPackCopyRoot = "Assets/Development/Art/UI/UiPack";

        private const string ThirdPartyPrefix = "Assets/ThirdParty/";

        public static string DisplayFontPath => TmpFontsRoot + "/Bungee-Regular SDF.asset";
        public static string BodyFontPath    => TmpFontsRoot + "/RussoOne-Regular SDF.asset";

        /// <summary>
        /// Цвет текста ПОВЕРХ спрайтов пака: кнопки/карточки Racing Game Ui Pack — белые
        /// подложки, белый текст на них невидим (артефакт первого прогона 8c).
        /// </summary>
        public static readonly Color DarkTextColor = new Color(0.12f, 0.12f, 0.14f, 1f);

        /// <summary>Находит дочерний объект по имени под parent или создаёт новый с RectTransform.</summary>
        public static RectTransform FindOrCreateChild(Transform parent, string name)
        {
            var existing = parent.Find(name);
            if (existing != null)
                return existing as RectTransform;

            var go = new GameObject(name, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, "Create " + name);
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        /// <summary>Растягивает RectTransform на весь родительский прямоугольник (anchors 0..1).</summary>
        public static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        /// <summary>Задаёт якоря/пивот/позицию/размер в референсе 1920x1080.</summary>
        public static void Anchor(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPosition;
            rt.sizeDelta = sizeDelta;
        }

        /// <summary>
        /// Загружает спрайт по пути. Путь из ThirdParty прозрачно подменяется локальной
        /// копией в <see cref="UiPackCopyRoot"/> (создаётся при первом обращении) —
        /// import-настройки правятся только на копии, пак остаётся нетронутым.
        /// </summary>
        public static Sprite LoadSprite(string path)
        {
            if (string.IsNullOrEmpty(path))
                return null;

            path = EnsureLocalCopy(path);
            EnsureSpriteImportType(path);
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
                Debug.LogWarning("[Editor] Спрайт не найден: " + path);
            return sprite;
        }

        /// <summary>
        /// Для ассета из ThirdParty возвращает путь его копии в <see cref="UiPackCopyRoot"/>,
        /// создавая копию при отсутствии. Пути вне ThirdParty возвращаются как есть.
        /// </summary>
        private static string EnsureLocalCopy(string path)
        {
            if (!path.StartsWith(ThirdPartyPrefix))
                return path;

            string copyPath = UiPackCopyRoot + "/" + System.IO.Path.GetFileName(path);
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(copyPath) != null)
                return copyPath;

            EnsureFolder("Assets/Development/Art", "UI");
            EnsureFolder("Assets/Development/Art/UI", "UiPack");

            if (!AssetDatabase.CopyAsset(path, copyPath))
            {
                // Копия не удалась — используем оригинал read-only, но импорт его НЕ трогаем.
                Debug.LogWarning("[Editor] Не удалось скопировать " + path + " -> " + copyPath +
                                  ". Использую оригинал без правки import-настроек.");
                return path;
            }

            Debug.Log("[Editor] Спрайт пака скопирован: " + copyPath);
            return copyPath;
        }

        private static void EnsureFolder(string parent, string folderName)
        {
            string full = parent + "/" + folderName;
            if (!AssetDatabase.IsValidFolder(full))
                AssetDatabase.CreateFolder(parent, folderName);
        }

        private static void EnsureSpriteImportType(string path)
        {
            // Страховка: import-настройки внутри ThirdParty не правим никогда.
            if (path.StartsWith(ThirdPartyPrefix))
                return;

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                return;

            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                Debug.Log("[Editor] Texture Type -> Sprite: " + path);
            }
        }

        public static TMP_FontAsset LoadFont(string path)
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (font == null)
                Debug.LogWarning("[Editor] TMP-шрифт не найден: " + path +
                                  ". Запусти 'FlatOutLike/Tools/Generate TMP Font Assets' сначала.");
            return font;
        }

        public static Image CreateFullscreenBackground(Transform parent, string spritePath, string name = "Background")
        {
            var rt = FindOrCreateChild(parent, name);
            Stretch(rt);
            rt.SetAsFirstSibling();

            var img = rt.GetComponent<Image>();
            if (img == null) img = Undo.AddComponent<Image>(rt.gameObject);
            img.sprite = LoadSprite(spritePath);
            img.preserveAspect = false;
            img.raycastTarget = false;
            img.type = Image.Type.Simple;
            return img;
        }

        public static Image CreateImage(Transform parent, string name, string spritePath)
        {
            var rt = FindOrCreateChild(parent, name);
            var img = rt.GetComponent<Image>();
            if (img == null) img = Undo.AddComponent<Image>(rt.gameObject);
            if (!string.IsNullOrEmpty(spritePath))
                img.sprite = LoadSprite(spritePath);
            return img;
        }

        public static TMP_Text CreateLabel(Transform parent, string name, string text, string fontPath,
            float fontSize, Color color, TextAlignmentOptions alignment = TextAlignmentOptions.Center)
        {
            var rt = FindOrCreateChild(parent, name);
            Stretch(rt);

            var tmp = rt.GetComponent<TextMeshProUGUI>();
            if (tmp == null) tmp = Undo.AddComponent<TextMeshProUGUI>(rt.gameObject);
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.alignment = alignment;
            tmp.raycastTarget = false;
            tmp.textWrappingMode = TextWrappingModes.NoWrap; // enableWordWrapping устарел в Unity 6 TMP

            var font = LoadFont(fontPath);
            if (font != null) tmp.font = font;
            return tmp;
        }

        public static Button CreateButton(Transform parent, string name, string spritePath, string label,
            string fontPath, float fontSize, Color textColor)
        {
            var rt = FindOrCreateChild(parent, name);

            var img = rt.GetComponent<Image>();
            if (img == null) img = Undo.AddComponent<Image>(rt.gameObject);
            if (!string.IsNullOrEmpty(spritePath))
                img.sprite = LoadSprite(spritePath);
            img.type = Image.Type.Simple;

            var btn = rt.GetComponent<Button>();
            if (btn == null) btn = Undo.AddComponent<Button>(rt.gameObject);
            btn.targetGraphic = img;

            if (!string.IsNullOrEmpty(label))
                CreateLabel(rt, "Label", label, fontPath, fontSize, textColor);

            return btn;
        }

        /// <summary>
        /// Полупрозрачная цветная плашка (без спрайта — Image рисует сплошной прямоугольник).
        /// Используется как подложка карточек/бейджей.
        /// </summary>
        public static Image CreatePanel(Transform parent, string name, Color color)
        {
            var rt = FindOrCreateChild(parent, name);
            var img = rt.GetComponent<Image>();
            if (img == null) img = Undo.AddComponent<Image>(rt.gameObject);
            img.sprite = null;
            img.color = color;
            return img;
        }

        /// <summary>
        /// Находит Canvas в активной сцене или создаёт новый (Screen Space - Overlay, 1920x1080),
        /// вместе с EventSystem. Используется сценами без готового UICanvas (например, ареной).
        /// Идемпотентно по компоненту (правило editor-files.md), не по имени GameObject.
        /// </summary>
        public static Canvas EnsureCanvasAndEventSystem()
        {
            var existingCanvas = Object.FindAnyObjectByType<Canvas>();
            GameObject canvasGo = existingCanvas != null ? existingCanvas.gameObject : null;
            Canvas canvas;

            if (canvasGo == null)
            {
                canvasGo = new GameObject("UICanvas");
                Undo.RegisterCreatedObjectUndo(canvasGo, "Create UICanvas");
                canvas = Undo.AddComponent<Canvas>(canvasGo);
                Undo.AddComponent<CanvasScaler>(canvasGo);
                Undo.AddComponent<GraphicRaycaster>(canvasGo);
                Debug.Log("[Editor] Создан UICanvas.");
            }
            else
            {
                canvas = canvasGo.GetComponent<Canvas>();
                if (canvasGo.GetComponent<CanvasScaler>() == null)
                    Undo.AddComponent<CanvasScaler>(canvasGo);
                if (canvasGo.GetComponent<GraphicRaycaster>() == null)
                    Undo.AddComponent<GraphicRaycaster>(canvasGo);
                Debug.Log("[Editor] Canvas уже есть, переиспользуем.");
            }

            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            var existingEs = Object.FindAnyObjectByType<EventSystem>();
            GameObject esGo = existingEs != null ? existingEs.gameObject : null;
            if (esGo == null)
            {
                esGo = new GameObject("EventSystem");
                Undo.RegisterCreatedObjectUndo(esGo, "Create EventSystem");
                Undo.AddComponent<EventSystem>(esGo);
                Debug.Log("[Editor] Создан EventSystem.");
            }

            if (esGo.GetComponent<StandaloneInputModule>() == null)
                Undo.AddComponent<StandaloneInputModule>(esGo);

            return canvas;
        }
    }
}
#endif
