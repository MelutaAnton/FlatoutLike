#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using FlatOutLike.Controllers.Car;
using FlatOutLike.Data.Configs.Combat;
using FlatOutLike.Data.Configs.Vfx;
using FlatOutLike.Installers;

namespace FlatOutLike.Editor.Setup
{
    /// <summary>
    /// Заполняет serialized-ссылки на GameLifetimeScope из ассетов и активной сцены.
    ///
    /// Поля шага 3: _damageConfig, _vfxConfig
    /// Поля шага 4: _sceneCars (все корневые GameObject'ы с CarController в сцене)
    ///
    /// Все поля заполняются за один проход; метод идемпотентен — повторный запуск
    /// перезаписывает ссылки текущим состоянием сцены, ничего не дублируя.
    ///
    /// Запускать ПОСЛЕ:
    ///   FlatOutLike/Configs/Create Default DamageConfig
    ///   FlatOutLike/Configs/Create Default VfxConfig
    ///   FlatOutLike/Setup/Build Driving Test Scene   (создаёт GameLifetimeScope)
    ///   FlatOutLike/Setup/Setup Enemy Car (Step 4)   (опционально; добавляет врага в _sceneCars)
    /// </summary>
    public static class FillGameLifetimeScopeRefsEditor
    {
        private const string DamageConfigPath = "Assets/Development/Data/Configs/Combat/DamageConfig.asset";
        private const string VfxConfigPath    = "Assets/Development/Data/Configs/Vfx/VfxConfig.asset";

        [MenuItem("FlatOutLike/Setup/Fill GameLifetimeScope Refs (Step 3)")]
        public static void FillRefs()
        {
            Undo.SetCurrentGroupName("Fill GameLifetimeScope Refs");
            int undoGroup = Undo.GetCurrentGroup();

            // ── 1. Ищем GameLifetimeScope в открытой сцене ────────────────────
            var scope = Object.FindAnyObjectByType<GameLifetimeScope>();
            if (scope == null)
            {
                Debug.LogWarning("[Editor] GameLifetimeScope not found in scene. " +
                                 "Run 'FlatOutLike/Setup/Build Driving Test Scene' first.");
                return;
            }

            Debug.Log("[Editor] Found GameLifetimeScope on '" + scope.gameObject.name + "'.");

            // ── 2. Загружаем ассеты конфигов шага 3 ───────────────────────────
            var damageConfig = AssetDatabase.LoadAssetAtPath<DamageConfig>(DamageConfigPath);
            if (damageConfig == null)
                Debug.LogWarning("[Editor] DamageConfig not found at " + DamageConfigPath +
                                 ". Run 'FlatOutLike/Configs/Create Default DamageConfig' first. " +
                                 "_damageConfig will remain null.");

            var vfxConfig = AssetDatabase.LoadAssetAtPath<VfxConfig>(VfxConfigPath);
            if (vfxConfig == null)
                Debug.LogWarning("[Editor] VfxConfig not found at " + VfxConfigPath +
                                 ". Run 'FlatOutLike/Configs/Create Default VfxConfig' first. " +
                                 "_vfxConfig will remain null.");

            var so = new SerializedObject(scope);

            // ── 3. Заполняем ссылки на конфиги шага 3 ─────────────────────────
            so.FindProperty("_damageConfig").objectReferenceValue = damageConfig;
            so.FindProperty("_vfxConfig").objectReferenceValue    = vfxConfig;

            Debug.Log("[Editor] GameLifetimeScope._damageConfig = " +
                      (damageConfig != null ? damageConfig.name : "NULL") +
                      ", _vfxConfig = " +
                      (vfxConfig    != null ? vfxConfig.name    : "NULL") + ".");

            // ── 4. Заполняем _sceneCars всеми корнями CarController в сцене ───
            // transform.root гарантирует, что мы всегда сохраняем самый верхний корень
            // префаба, а не дочерний GameObject — GameLifetimeScope.Configure() вызывает
            // InjectGameObject(go), который инжектит всю иерархию.
            var allCars  = Object.FindObjectsByType<CarController>(FindObjectsSortMode.None);
            var carsProp = so.FindProperty("_sceneCars");
            carsProp.arraySize = allCars.Length;
            for (int i = 0; i < allCars.Length; i++)
                carsProp.GetArrayElementAtIndex(i).objectReferenceValue =
                    allCars[i].transform.root.gameObject;

            Debug.Log("[Editor] GameLifetimeScope._sceneCars filled with " + allCars.Length + " car(s):");
            for (int i = 0; i < allCars.Length; i++)
                Debug.Log("[Editor]   [" + i + "] " + allCars[i].transform.root.gameObject.name);

            // ── Применяем все изменения разом ─────────────────────────────────
            so.ApplyModifiedProperties();

            // ── 5. Помечаем сцену как dirty ───────────────────────────────────
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

            Undo.CollapseUndoOperations(undoGroup);

            Debug.Log("[Editor] GameLifetimeScope refs fill complete.");
        }

        /// <summary>
        /// Удобный алиас точки входа для workflow шага 4.
        /// Заполняет _damageConfig, _vfxConfig И _sceneCars — идентично пункту шага 3.
        /// Запускать после 'Setup Enemy Car (Step 4)', чтобы и игрок, и враг были зарегистрированы.
        /// </summary>
        [MenuItem("FlatOutLike/Setup/Fill GameLifetimeScope Refs (Step 4)")]
        public static void FillRefsStep4() => FillRefs();
    }
}
#endif
