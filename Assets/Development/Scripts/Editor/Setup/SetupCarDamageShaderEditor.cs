#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using FlatOutLike.Controllers.Car;
using FlatOutLike.Data.Configs.Car;

namespace FlatOutLike.Editor.Setup
{
    /// <summary>
    /// Конвертирует материалы корпуса машины на ВЫДЕЛЕННЫХ prefab-ассетах в окне
    /// Project (поддерживается мультивыделение), вне зависимости от открытой сцены.
    /// Каждый префаб редактируется через LoadPrefabContents → ConvertCar →
    /// SaveAsPrefabAsset. Для исходных материалов создаются per-material
    /// _Damage-варианты с шейдером FlatOutLike/CarDamageLit в
    /// Assets/Development/Art/Materials/Damage/. Свойства (_BaseMap, _BaseColor,
    /// _Metallic, _Smoothness) копируются из оригинала с URP-fallback'ами
    /// (_MainTex, _Color).
    ///
    /// Исключаются из конвертации:
    ///   • Материалы из списка CarDamageVisualConfig._preservedMaterials (стекло, фары).
    ///   • Рендереры, являющиеся детьми CarController._wheelVisuals.
    ///   • Компоненты ParticleSystemRenderer.
    ///
    /// Пропускаются целиком: выделенные ассеты без CarController (скрипт работает
    /// только с префабами машин).
    ///
    /// Идемпотентность: материалы, уже использующие damage-шейдер, пропускаются;
    ///                  существующие _Damage.mat-ассеты переиспользуются и обновляются.
    ///
    /// Запускать ПОСЛЕ:
    ///   FlatOutLike/Configs/Create Default CarDamageVisualConfig  (для preserve-list)
    ///   и при наличии самого prefab-ассета машины.
    /// </summary>
    public static class SetupCarDamageShaderEditor
    {
        private const string VisualConfigPath = "Assets/Development/Data/Configs/Car/CarDamageVisualConfig.asset";
        private const string DamageShaderName = "FlatOutLike/CarDamageLit";
        private const string DamageMatFolder  = "Assets/Development/Art/Materials/Damage";

        [MenuItem("FlatOutLike/Setup/Convert Car Body To Damage Shader")]
        public static void ConvertCarBodyToDamageShader()
        {
            Undo.SetCurrentGroupName("Convert Car Body To Damage Shader");
            int undoGroup = Undo.GetCurrentGroup();

            // ── 1. Собираем выделенные prefab-ассеты из окна Project ──────────
            // Работаем именно с ассетами (не с объектами сцены): для каждого
            // выделенного объекта берём путь ассета и фильтруем .prefab.
            var prefabPaths = new List<string>();
            foreach (var obj in Selection.objects)
            {
                var path = AssetDatabase.GetAssetPath(obj);
                if (string.IsNullOrEmpty(path)) continue;          // не ассет (объект сцены и т.п.)
                if (!path.EndsWith(".prefab")) continue;           // только префабы
                if (!prefabPaths.Contains(path)) prefabPaths.Add(path);
            }

            if (prefabPaths.Count == 0)
            {
                Debug.LogWarning("[Editor] Convert Car Body To Damage Shader: выдели один или несколько " +
                                 "prefab-ассетов машины в окне Project и запусти снова.");
                return;
            }

            // ── 2. Конвертируем каждый префаб через prefab-contents ───────────
            int prefabsConverted = 0;
            foreach (var path in prefabPaths)
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var carController = root.GetComponentInChildren<CarController>(true);
                    if (carController == null)
                    {
                        Debug.LogWarning("[Editor] '" + path + "' не содержит CarController — пропущен " +
                                         "(скрипт конвертирует только префабы машин).");
                        continue;
                    }

                    // В prefab-contents контексте Undo.RecordObject внутри ConvertCar — no-op,
                    // но назначение материалов корректно сохраняется через SaveAsPrefabAsset ниже.
                    ConvertCar(carController);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    Debug.Log("[Editor] Damage-шейдер конвертирован и сохранён: " + path);
                    prefabsConverted++;
                }
                finally
                {
                    // UnloadPrefabContents ОБЯЗАТЕЛЕН в finally — уничтожает preview-сцену
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Undo.CollapseUndoOperations(undoGroup);

            Debug.Log("[Editor] Convert Car Body To Damage Shader: обработано префабов — " +
                      prefabsConverted + " из " + prefabPaths.Count + " выделенных.");
        }

        /// <summary>
        /// Конвертирует материалы корпуса на GameObject указанного CarController на damage-шейдер.
        /// Безопасно вызывать из других editor-скриптов (например, SetupEnemyCarEditor),
        /// не влияя на то, какую машину обрабатывает существующий пункт меню.
        /// Группами Undo НЕ управляет — это ответственность вызывающего.
        /// </summary>
        public static void ConvertCar(CarController carController)
        {
            var carGo = carController.gameObject;
            Debug.Log("[Editor] Converting damage shader materials on: " + carGo.name);

            // ── Собираем preserve-set из CarDamageVisualConfig ────────────────
            // Читаем через SerializedObject, чтобы не было compile-time зависимости
            // от поля _preservedMaterials, которое параллельно добавляет code-writer.
            // Когда этого поля ещё нет, FindProperty возвращает null и мы продолжаем
            // с пустым набором (все материалы корпуса конвертируемы).
            var preservedSet = new HashSet<Material>();
            var visualConfig = AssetDatabase.LoadAssetAtPath<CarDamageVisualConfig>(VisualConfigPath);
            if (visualConfig != null)
            {
                var sovc = new SerializedObject(visualConfig);
                var preservedProp = sovc.FindProperty("_preservedMaterials");
                if (preservedProp != null && preservedProp.isArray)
                {
                    for (int i = 0; i < preservedProp.arraySize; i++)
                    {
                        var mat = preservedProp.GetArrayElementAtIndex(i).objectReferenceValue as Material;
                        if (mat != null) preservedSet.Add(mat);
                    }
                    Debug.Log("[Editor] Preserve-list: " + preservedSet.Count + " material(s) from CarDamageVisualConfig.");
                }
                else
                {
                    Debug.Log("[Editor] CarDamageVisualConfig._preservedMaterials not present yet — all body materials are convertible.");
                }
            }
            else
            {
                Debug.LogWarning("[Editor] CarDamageVisualConfig not found at " + VisualConfigPath +
                                 ". Continuing without preserve-list.");
            }

            // ── Ищем damage-шейдер ────────────────────────────────────────────
            var damageShader = Shader.Find(DamageShaderName);
            if (damageShader == null)
            {
                Debug.LogError("[Editor] Shader '" + DamageShaderName + "' not found. " +
                               "Ensure the URP shader asset exists and is compiled.");
                return;
            }

            // ── Собираем Transform'ы wheel-visuals для исключения ─────────────
            var wheelVisualRoots = new HashSet<Transform>();
            var soController = new SerializedObject(carController);
            var wheelVisualsProp = soController.FindProperty("_wheelVisuals");
            if (wheelVisualsProp != null && wheelVisualsProp.isArray)
            {
                for (int i = 0; i < wheelVisualsProp.arraySize; i++)
                {
                    var t = wheelVisualsProp.GetArrayElementAtIndex(i).objectReferenceValue as Transform;
                    if (t != null) wheelVisualRoots.Add(t);
                }
                Debug.Log("[Editor] Wheel visual exclusion set: " + wheelVisualRoots.Count + " transform(s).");
            }

            // ── Убеждаемся, что иерархия выходных папок существует ────────────
            EnsureFolder("Assets/Development",               "Art");
            EnsureFolder("Assets/Development/Art",           "Materials");
            EnsureFolder("Assets/Development/Art/Materials", "Damage");

            // ── Обрабатываем рендереры ────────────────────────────────────────
            var allRenderers = carGo.GetComponentsInChildren<Renderer>(true);
            int converted = 0;
            int reused    = 0;
            int skipped   = 0;

            foreach (var renderer in allRenderers)
            {
                // Исключаем particle-рендереры
                if (renderer is ParticleSystemRenderer)
                {
                    skipped++;
                    continue;
                }

                // Исключаем детей wheel-visual Transform'ов
                bool isWheel = false;
                foreach (var wv in wheelVisualRoots)
                {
                    if (renderer.transform == wv || renderer.transform.IsChildOf(wv))
                    {
                        isWheel = true;
                        break;
                    }
                }
                if (isWheel)
                {
                    skipped++;
                    continue;
                }

                // ── Поcлотовая конвертация материалов ─────────────────────────
                // sharedMaterials возвращает КОПИЮ; нужно присвоить обратно после изменений.
                var slots         = renderer.sharedMaterials;
                bool slotsChanged = false;

                for (int s = 0; s < slots.Length; s++)
                {
                    var srcMat = slots[s];
                    if (srcMat == null) continue;

                    // Идемпотентность: слот уже использует damage-шейдер
                    if (srcMat.shader == damageShader)
                    {
                        Debug.Log("[Editor] '" + renderer.name + "' slot " + s +
                                  " already uses " + DamageShaderName + ", skipping.");
                        continue;
                    }

                    // Preserve-list: стекло, фары, молдинги и т.д.
                    if (preservedSet.Contains(srcMat))
                    {
                        Debug.Log("[Editor] '" + renderer.name + "' slot " + s +
                                  " ('" + srcMat.name + "') is preserved, skipping.");
                        continue;
                    }

                    // ── Определяем путь выходного ассета ──────────────────────
                    string damageMatPath = DamageMatFolder + "/" + srcMat.name + "_Damage.mat";

                    var destMat = AssetDatabase.LoadAssetAtPath<Material>(damageMatPath);
                    bool isNew  = destMat == null;

                    if (isNew)
                    {
                        destMat = new Material(damageShader);
                        AssetDatabase.CreateAsset(destMat, damageMatPath);
                        Undo.RegisterCreatedObjectUndo(destMat, "Create " + srcMat.name + "_Damage");
                        Debug.Log("[Editor] Created damage material: " + damageMatPath);
                    }
                    else
                    {
                        // Убеждаемся, что шейдер актуален (например, если скрипт перезапущен после переименования шейдера)
                        destMat.shader = damageShader;
                        Debug.Log("[Editor] Reusing damage material: " + damageMatPath);
                    }

                    // ── Копируем свойства (с URP-fallback'ами) ────────────────

                    // _BaseMap: стандартное имя URP; fallback на _MainTex (многие ThirdParty-паки)
                    string baseMapSrc = srcMat.HasProperty("_BaseMap")  ? "_BaseMap"
                                      : srcMat.HasProperty("_MainTex") ? "_MainTex"
                                      : null;
                    if (baseMapSrc != null)
                    {
                        var tex = srcMat.GetTexture(baseMapSrc);
                        if (tex != null)
                        {
                            destMat.SetTexture("_BaseMap", tex);
                            destMat.SetTextureScale( "_BaseMap", srcMat.GetTextureScale(baseMapSrc));
                            destMat.SetTextureOffset("_BaseMap", srcMat.GetTextureOffset(baseMapSrc));
                        }
                    }

                    // _BaseColor: стандартное имя URP; fallback на _Color (legacy/Standard)
                    if (srcMat.HasProperty("_BaseColor"))
                        destMat.SetColor("_BaseColor", srcMat.GetColor("_BaseColor"));
                    else if (srcMat.HasProperty("_Color"))
                        destMat.SetColor("_BaseColor", srcMat.GetColor("_Color"));

                    // _Metallic
                    if (srcMat.HasProperty("_Metallic"))
                        destMat.SetFloat("_Metallic", srcMat.GetFloat("_Metallic"));

                    // _Smoothness
                    if (srcMat.HasProperty("_Smoothness"))
                        destMat.SetFloat("_Smoothness", srcMat.GetFloat("_Smoothness"));

                    EditorUtility.SetDirty(destMat);

                    slots[s]      = destMat;
                    slotsChanged  = true;

                    if (isNew) converted++; else reused++;
                }

                // Присваиваем обратно только если что-то изменилось; пишем undo на рендерер
                if (slotsChanged)
                {
                    Undo.RecordObject(renderer, "Convert Materials on " + renderer.name);
                    renderer.sharedMaterials = slots;
                    Debug.Log("[Editor] Updated sharedMaterials on renderer '" + renderer.name + "'.");
                }
            }

            // ── Сохраняем и помечаем сцену как dirty ──────────────────────────
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

            Debug.Log("[Editor] Convert Car Body To Damage Shader complete for '" + carGo.name + "' — " +
                      "new: " + converted + ", reused: " + reused +
                      ", skipped (preserved/wheel/particle): " + skipped + ".");
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
