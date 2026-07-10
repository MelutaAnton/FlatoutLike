#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEngine;
using TMPro;

namespace FlatOutLike.Editor.Tools
{
    /// <summary>
    /// Шаг 8c: генерирует TMP_FontAsset'ы из трёх TTF-файлов проекта
    /// (Assets/Development/Art/Fonts/{Bungee,RussoOne,TitanOne}-Regular.ttf) в
    /// Assets/Development/Art/Fonts/TMP/. Bungee используется как display-шрифт
    /// заголовков/кнопок, RussoOne — как body-шрифт (лейблы, цены, счётчики) во всех
    /// Build*UIEditor Шага 8c. TitanOne генерируется про запас (не используется сборкой).
    ///
    /// TMP_FontAsset.CreateFontAsset(font) сразу синхронно запекает атлас со стандартным
    /// ASCII-набором символов (statically baked at creation time — рантайм-зависимости
    /// от TMP Font Asset Creator Window нет). Дополнительно добираем кириллицу
    /// (для будущей RU-локализации UI, см. правило CLAUDE.md про русские комментарии/тексты).
    ///
    /// Идемпотентно: существующий ассет не пересоздаётся.
    /// </summary>
    public static class GenerateTmpFontAssetsEditor
    {
        private const string SourceFolder = "Assets/Development/Art/Fonts";
        private const string TargetFolder = SourceFolder + "/TMP";

        private static readonly string[] FontNames =
        {
            "Bungee-Regular",
            "RussoOne-Regular",
            "TitanOne-Regular",
        };

        [MenuItem("FlatOutLike/Tools/Generate TMP Font Assets")]
        public static void GenerateTmpFontAssets()
        {
            Undo.SetCurrentGroupName("Generate TMP Font Assets (Step 8c)");
            int undoGroup = Undo.GetCurrentGroup();

            EnsureFolder("Assets/Development/Art", "Fonts");
            EnsureFolder(SourceFolder, "TMP");

            foreach (var fontName in FontNames)
                GenerateOne(fontName);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Undo.CollapseUndoOperations(undoGroup);
            Debug.Log("[Editor] Generate TMP Font Assets (Step 8c) завершён.");
        }

        private static void GenerateOne(string fontName)
        {
            string sourcePath = SourceFolder + "/" + fontName + ".ttf";
            string targetPath = TargetFolder + "/" + fontName + " SDF.asset";

            if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(targetPath) != null)
            {
                Debug.Log("[Editor] " + targetPath + " уже существует — пропускаем.");
                return;
            }

            var sourceFont = AssetDatabase.LoadAssetAtPath<Font>(sourcePath);
            if (sourceFont == null)
            {
                Debug.LogWarning("[Editor] TTF не найден: " + sourcePath + ". Ассет не создан.");
                return;
            }

            var fontAsset = TMP_FontAsset.CreateFontAsset(sourceFont);
            if (fontAsset == null)
            {
                Debug.LogWarning("[Editor] TMP_FontAsset.CreateFontAsset вернул null для " + sourcePath);
                return;
            }

            fontAsset.name = fontName + " SDF";

            // Добираем кириллицу и типовые пунктуационные символы в уже запечённый атлас.
            fontAsset.TryAddCharacters(BuildExtraCharacterSet(), out _);

            AssetDatabase.CreateAsset(fontAsset, targetPath);

            if (fontAsset.material != null)
                AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);

            if (fontAsset.atlasTextures != null)
            {
                foreach (var tex in fontAsset.atlasTextures)
                {
                    if (tex != null)
                        AssetDatabase.AddObjectToAsset(tex, fontAsset);
                }
            }

            Undo.RegisterCreatedObjectUndo(fontAsset, "Create " + targetPath);
            Debug.Log("[Editor] Создан TMP_FontAsset: " + targetPath);
        }

        private static string BuildExtraCharacterSet()
        {
            var sb = new StringBuilder();
            for (char c = 'А'; c <= 'я'; c++)
                sb.Append(c);
            sb.Append('Ё').Append('ё');
            sb.Append("№–—…");
            return sb.ToString();
        }

        private static void EnsureFolder(string parent, string folderName)
        {
            string full = parent + "/" + folderName;
            if (!AssetDatabase.IsValidFolder(full))
            {
                AssetDatabase.CreateFolder(parent, folderName);
                Debug.Log("[Editor] Created folder " + full);
            }
        }
    }
}
#endif
