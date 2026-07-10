#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using FlatOutLike.Data.Configs.Car;

namespace FlatOutLike.Editor.Tools
{
    /// <summary>
    /// Шаг 8c: рендерит превью каждого prefab'а машины из CarCatalogConfig_Default
    /// в PNG (AssetPreview.GetAssetPreview, с ожиданием асинхронной прогрузки), импортирует
    /// как Sprite в Assets/Development/Art/UI/CarIcons/&lt;id&gt;.png и прошивает в
    /// CarCatalogEntry._icon — ТОЛЬКО если поле пустое, чтобы не затирать иконку,
    /// уже подставленную художником вручную.
    ///
    /// Идемпотентно: entry с уже заполненным _icon пропускается.
    /// Запускать ПОСЛЕ: FlatOutLike/Configs/Create Default CarCatalogConfig.
    /// </summary>
    public static class GenerateCarIconsEditor
    {
        private const string CatalogPath = "Assets/Development/Data/Configs/Car/CarCatalogConfig_Default.asset";
        private const string IconsFolder = "Assets/Development/Art/UI/CarIcons";

        private const int MaxPreviewWaitIterations = 100; // ~5 секунд на превью (по 50мс)

        [MenuItem("FlatOutLike/Tools/Generate Car Icons")]
        public static void GenerateCarIcons()
        {
            Undo.SetCurrentGroupName("Generate Car Icons (Step 8c)");
            int undoGroup = Undo.GetCurrentGroup();

            EnsureFolderPath(IconsFolder);

            var config = AssetDatabase.LoadAssetAtPath<CarCatalogConfig>(CatalogPath);
            if (config == null)
            {
                Debug.LogWarning("[Editor] CarCatalogConfig_Default не найден по " + CatalogPath +
                                  ". Запусти 'FlatOutLike/Configs/Create Default CarCatalogConfig' сначала.");
                Undo.CollapseUndoOperations(undoGroup);
                return;
            }

            var so = new SerializedObject(config);
            var entriesProp = so.FindProperty("_entries");
            if (entriesProp == null)
            {
                Debug.LogWarning("[Editor] Поле CarCatalogConfig._entries не найдено.");
                Undo.CollapseUndoOperations(undoGroup);
                return;
            }

            bool changed = false;

            for (int i = 0; i < entriesProp.arraySize; i++)
            {
                var entry = entriesProp.GetArrayElementAtIndex(i);
                var idProp = entry.FindPropertyRelative("_id");
                var iconProp = entry.FindPropertyRelative("_icon");
                var prefabProp = entry.FindPropertyRelative("_prefab");

                if (idProp == null || iconProp == null || prefabProp == null)
                    continue;

                string id = idProp.stringValue;

                if (iconProp.objectReferenceValue != null)
                {
                    Debug.Log("[Editor] Entry '" + id + "' уже имеет _icon — пропускаем.");
                    continue;
                }

                var prefab = prefabProp.objectReferenceValue as GameObject;
                if (prefab == null)
                {
                    Debug.LogWarning("[Editor] Entry '" + id + "' без _prefab — иконку сгенерировать нельзя.");
                    continue;
                }

                var sprite = GenerateIconFor(id, prefab);
                if (sprite == null)
                    continue;

                iconProp.objectReferenceValue = sprite;
                changed = true;
                Debug.Log("[Editor] Иконка сгенерирована и прошита для entry '" + id + "'.");
            }

            if (changed)
            {
                so.ApplyModifiedProperties();
                AssetDatabase.SaveAssets();
            }

            Undo.CollapseUndoOperations(undoGroup);
            Debug.Log("[Editor] Generate Car Icons (Step 8c) завершён.");
        }

        private static Sprite GenerateIconFor(string id, GameObject prefab)
        {
            string pngPath = IconsFolder + "/" + id + ".png";

            var existingSprite = AssetDatabase.LoadAssetAtPath<Sprite>(pngPath);
            if (existingSprite != null)
                return existingSprite;

            var texture = WaitForPreview(prefab);
            if (texture == null)
            {
                Debug.LogWarning("[Editor] Превью для '" + id + "' не прогрузилось за отведённое время — иконка не создана.");
                return null;
            }

            byte[] png;
            try
            {
                // AssetPreview-текстуры в редакторе доступны для чтения — EncodeToPNG работает синхронно.
                png = texture.EncodeToPNG();
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[Editor] Не удалось закодировать превью '" + id + "' в PNG: " + ex.Message);
                return null;
            }

            if (png == null || png.Length == 0)
            {
                Debug.LogWarning("[Editor] Превью '" + id + "' закодировалось в пустой PNG — пропускаем.");
                return null;
            }

            File.WriteAllBytes(pngPath, png);
            AssetDatabase.ImportAsset(pngPath, ImportAssetOptions.ForceUpdate);

            var importer = AssetImporter.GetAtPath(pngPath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                AssetDatabase.ImportAsset(pngPath, ImportAssetOptions.ForceUpdate);
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(pngPath);
        }

        private static Texture2D WaitForPreview(GameObject prefab)
        {
            Texture2D texture = null;
            for (int i = 0; i < MaxPreviewWaitIterations; i++)
            {
                texture = AssetPreview.GetAssetPreview(prefab);
                if (texture != null)
                    break;

                if (!AssetPreview.IsLoadingAssetPreview(prefab.GetInstanceID()))
                {
                    // Не грузится и не готово — пробуем ещё раз через явный запрос кадра рендера.
                    AssetPreview.GetAssetPreview(prefab);
                }

                System.Threading.Thread.Sleep(50);
            }

            return texture != null ? texture : AssetPreview.GetAssetPreview(prefab);
        }

        private static void EnsureFolderPath(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            var parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                    Debug.Log("[Editor] Created folder " + next);
                }
                current = next;
            }
        }
    }
}
#endif
