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
    /// Converts car body materials on the scene car to use FlatOutLike/CarDamageLit shader,
    /// creating per-material _Damage variants under Assets/Development/Art/Materials/Damage/.
    /// Properties (_BaseMap, _BaseColor, _Metallic, _Smoothness) are copied from the
    /// original material with URP-compatibility fallbacks (_MainTex, _Color).
    ///
    /// Excluded from conversion:
    ///   • Materials listed in CarDamageVisualConfig._preservedMaterials (glass, headlights).
    ///   • Renderers that are children of CarController._wheelVisuals.
    ///   • ParticleSystemRenderer components.
    ///
    /// Idempotent: materials already on the damage shader are skipped;
    ///             existing _Damage.mat assets are reused and properties are refreshed.
    ///
    /// Run AFTER:
    ///   FlatOutLike/Setup/Build Driving Test Scene
    ///   FlatOutLike/Configs/Create Default CarDamageVisualConfig
    /// </summary>
    public static class SetupCarDamageShaderEditor
    {
        private const string ScenePath          = "Assets/Development/Scenes/_TestDriving.unity";
        private const string VisualConfigPath   = "Assets/Development/Data/Configs/Car/CarDamageVisualConfig.asset";
        private const string DamageShaderName   = "FlatOutLike/CarDamageLit";
        private const string DamageMatFolder    = "Assets/Development/Art/Materials/Damage";

        [MenuItem("FlatOutLike/Setup/Convert Car Body To Damage Shader")]
        public static void ConvertCarBodyToDamageShader()
        {
            Undo.SetCurrentGroupName("Convert Car Body To Damage Shader");
            int undoGroup = Undo.GetCurrentGroup();

            // ── 1. Ensure _TestDriving is open ────────────────────────────────
            OpenSceneIfNeeded();

            // ── 2. Find car by component ──────────────────────────────────────
            var carController = Object.FindAnyObjectByType<CarController>();
            if (carController == null)
            {
                Debug.LogWarning("[Editor] No CarController found in scene. " +
                                 "Run 'FlatOutLike/Setup/Build Driving Test Scene' first.");
                return;
            }
            var carGo = carController.gameObject;
            Debug.Log("[Editor] Found car: " + carGo.name);

            // ── 3. Build preserve-set from CarDamageVisualConfig ──────────────
            // Read via SerializedObject so no compile-time dependency on the
            // _preservedMaterials field that code-writer adds in parallel.
            // When that field doesn't exist yet, FindProperty returns null and we
            // continue with an empty set (all body materials convertible).
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

            // ── 4. Find damage shader ─────────────────────────────────────────
            var damageShader = Shader.Find(DamageShaderName);
            if (damageShader == null)
            {
                Debug.LogError("[Editor] Shader '" + DamageShaderName + "' not found. " +
                               "Ensure the URP shader asset exists and is compiled.");
                return;
            }

            // ── 5. Collect wheel visual transforms for exclusion ──────────────
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

            // ── 6. Ensure output folder hierarchy exists ──────────────────────
            EnsureFolder("Assets/Development",               "Art");
            EnsureFolder("Assets/Development/Art",           "Materials");
            EnsureFolder("Assets/Development/Art/Materials", "Damage");

            // ── 7. Process renderers ──────────────────────────────────────────
            var allRenderers = carGo.GetComponentsInChildren<Renderer>(true);
            int converted = 0;
            int reused    = 0;
            int skipped   = 0;

            foreach (var renderer in allRenderers)
            {
                // Exclude particle renderers
                if (renderer is ParticleSystemRenderer)
                {
                    skipped++;
                    continue;
                }

                // Exclude children of wheel visual transforms
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

                // ── Per-slot material conversion ──────────────────────────────
                // sharedMaterials returns a COPY; assign back after changes.
                var slots        = renderer.sharedMaterials;
                bool slotsChanged = false;

                for (int s = 0; s < slots.Length; s++)
                {
                    var srcMat = slots[s];
                    if (srcMat == null) continue;

                    // Idempotency: slot already uses damage shader
                    if (srcMat.shader == damageShader)
                    {
                        Debug.Log("[Editor] '" + renderer.name + "' slot " + s +
                                  " already uses " + DamageShaderName + ", skipping.");
                        continue;
                    }

                    // Preserve-list: glass, headlights, trim, etc.
                    if (preservedSet.Contains(srcMat))
                    {
                        Debug.Log("[Editor] '" + renderer.name + "' slot " + s +
                                  " ('" + srcMat.name + "') is preserved, skipping.");
                        continue;
                    }

                    // ── Resolve output asset path ─────────────────────────────
                    string damageMatPath = DamageMatFolder + "/" + srcMat.name + "_Damage.mat";

                    var destMat  = AssetDatabase.LoadAssetAtPath<Material>(damageMatPath);
                    bool isNew   = destMat == null;

                    if (isNew)
                    {
                        destMat = new Material(damageShader);
                        AssetDatabase.CreateAsset(destMat, damageMatPath);
                        Undo.RegisterCreatedObjectUndo(destMat, "Create " + srcMat.name + "_Damage");
                        Debug.Log("[Editor] Created damage material: " + damageMatPath);
                    }
                    else
                    {
                        // Ensure shader is up to date (e.g. if script re-runs after shader rename)
                        destMat.shader = damageShader;
                        Debug.Log("[Editor] Reusing damage material: " + damageMatPath);
                    }

                    // ── Copy properties (with URP-conversion fallbacks) ────────

                    // _BaseMap: URP standard name; fallback _MainTex (many ThirdParty packs)
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

                    // _BaseColor: URP standard name; fallback _Color (legacy/Standard)
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

                    slots[s]     = destMat;
                    slotsChanged = true;

                    if (isNew) converted++; else reused++;
                }

                // Assign back only if something changed; record undo on renderer
                if (slotsChanged)
                {
                    Undo.RecordObject(renderer, "Convert Materials on " + renderer.name);
                    renderer.sharedMaterials = slots;
                    Debug.Log("[Editor] Updated sharedMaterials on renderer '" + renderer.name + "'.");
                }
            }

            // ── 8. Save and mark scene dirty ──────────────────────────────────
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

            Undo.CollapseUndoOperations(undoGroup);

            Debug.Log("[Editor] Convert Car Body To Damage Shader complete — " +
                      "new: " + converted + ", reused: " + reused +
                      ", skipped (preserved/wheel/particle): " + skipped + ".");
        }

        // ─────────────────────────────────────────────────────────────────────
        private static void OpenSceneIfNeeded()
        {
            if (SceneManager.GetActiveScene().path == ScenePath) return;

            if (!System.IO.File.Exists(
                    System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), ScenePath)))
            {
                Debug.LogWarning("[Editor] _TestDriving.unity not found at " + ScenePath +
                                 ". Run 'FlatOutLike/Setup/Build Driving Test Scene' first.");
                return;
            }

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Debug.Log("[Editor] Opened _TestDriving.unity.");
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
