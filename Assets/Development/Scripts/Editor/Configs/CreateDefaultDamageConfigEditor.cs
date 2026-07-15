#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using FlatOutLike.Data.Configs.Combat;
using FlatOutLike.Models;

namespace FlatOutLike.Editor.Configs
{
    public static class CreateDefaultDamageConfigEditor
    {
        private const string AssetPath    = "Assets/Development/Data/Configs/Combat/DamageConfig.asset";
        private const string FolderData   = "Assets/Development/Data";
        private const string FolderCfg    = "Assets/Development/Data/Configs";
        private const string FolderCombat = "Assets/Development/Data/Configs/Combat";

        [MenuItem("FlatOutLike/Configs/Create Default DamageConfig")]
        public static void CreateDefaultDamageConfig()
        {
            // ── Проверка идемпотентности ──────────────────────────────────────
            if (AssetDatabase.LoadAssetAtPath<DamageConfig>(AssetPath) != null)
            {
                Debug.Log("[Editor] DamageConfig already exists at " + AssetPath);
                ApplyDamageIgnoreLayers();
                return;
            }

            // ── Убеждаемся, что иерархия папок существует ─────────────────────
            EnsureFolder("Assets/Development", "Data");
            EnsureFolder(FolderData,           "Configs");
            EnsureFolder(FolderCfg,            "Combat");

            // ── Создание ассета ───────────────────────────────────────────────
            var config = ScriptableObject.CreateInstance<DamageConfig>();
            AssetDatabase.CreateAsset(config, AssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // ── Явные дефолтные значения (DamageScale=1, MinImpactSpeed=3, WallMassRatio=1) ──
            // Field-initializers класса уже совпадают с этими значениями; задаём явно для ясности.
            var so = new SerializedObject(config);
            so.FindProperty("_damageScale").floatValue    = 1f;
            so.FindProperty("_minImpactSpeed").floatValue = 3f;
            so.FindProperty("_wallMassRatio").floatValue  = 1f;
            so.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();

            Undo.RegisterCreatedObjectUndo(config, "Create DamageConfig");

            Selection.activeObject      = config;
            EditorGUIUtility.PingObject(config);

            Debug.Log("[Editor] Created DamageConfig at " + AssetPath);

            // ── Прошиваем маску исключения урона (Floor) сразу после создания ──
            ApplyDamageIgnoreLayers();
        }

        /// <summary>
        /// Добавляет слой Layers.Floor в маску _damageIgnoreLayers существующего DamageConfig
        /// побитовым OR — не перезаписывая маску целиком, чтобы сохранить прочие слои,
        /// которые мог добавить дизайнер вручную через Inspector.
        ///
        /// ПОРЯДОК ЗАПУСКА: сначала 'FlatOutLike/Setup/Create Physics Layers' (регистрирует
        /// слой Floor в TagManager), затем этот пункт меню.
        /// Идемпотентность: повторный запуск при уже установленном бите — no-op с логом.
        /// </summary>
        [MenuItem("FlatOutLike/Configs/Set Damage Ignore Layers")]
        public static void ApplyDamageIgnoreLayers()
        {
            var config = AssetDatabase.LoadAssetAtPath<DamageConfig>(AssetPath);
            if (config == null)
            {
                Debug.LogWarning("[Editor] DamageConfig не найден по пути " + AssetPath +
                                 ". Сначала запусти 'FlatOutLike/Configs/Create Default DamageConfig'.");
                return;
            }

            int floorLayer = LayerMask.NameToLayer(Layers.Floor);
            if (floorLayer < 0)
            {
                Debug.LogWarning($"[Editor] Слой '{Layers.Floor}' не зарегистрирован в TagManager. " +
                                 "Сначала запусти 'FlatOutLike/Setup/Create Physics Layers'.");
                return;
            }

            var so = new SerializedObject(config);
            var ignoreLayersProp = so.FindProperty("_damageIgnoreLayers");
            if (ignoreLayersProp == null)
            {
                Debug.LogWarning("[Editor] Поле '_damageIgnoreLayers' не найдено в DamageConfig — " +
                                 "рантайм ещё не скомпилирован. Дождись компиляции и повтори.");
                return;
            }

            int floorBit = 1 << floorLayer;
            if ((ignoreLayersProp.intValue & floorBit) != 0)
            {
                Debug.Log("[Editor] DamageIgnoreLayers уже содержит слой Floor — пропускаем.");
                return;
            }

            ignoreLayersProp.intValue |= floorBit;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();

            Debug.Log($"[Editor] В DamageConfig.DamageIgnoreLayers добавлен слой '{Layers.Floor}'.");
        }

        // ─────────────────────────────────────────────────────────────────────
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
