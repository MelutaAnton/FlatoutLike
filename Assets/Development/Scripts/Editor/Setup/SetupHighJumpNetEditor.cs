#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

namespace FlatOutLike.Editor.Setup
{
    /// <summary>
    /// Строит измерительную сетку-стену для режима High Jump (Шаг 7): декоративный
    /// фон вдоль +Z с отметками высоты каждые 10 метров, как в FlatOut 2.
    ///
    /// Что создаётся под корнем MeasurementNet:
    ///   • NetPanel      — Quad 44x60, материал HighJumpNet (шейдер FlatOutLike/HighJumpNet).
    ///   • Stripe_10..60 — тонкие горизонтальные полосы-отметки (материал HighJumpNetStripe).
    ///   • Label_10..60  — TextMeshPro 3D-цифры высоты, мировой Y соответствует метрам.
    ///   • Pylon_Left / Pylon_Right — вытянутые кубы-опоры (материал HighJumpPylon).
    ///
    /// Всё — чистый декор: коллайдеры удалены, Rigidbody нет, бизнес-логики нет.
    /// Водитель пролетает сквозь сетку, PeakHeight считается независимо (по мировому Y).
    ///
    /// Идемпотентность: find-or-create по имени дочерних объектов под MeasurementNet,
    /// повторный запуск не дублирует объекты, только обновляет трансформы/материалы.
    /// Материалы создаются один раз через AssetDatabase.LoadAssetAtPath-guard.
    ///
    /// Запускать ПОСЛЕ того, как шейдер Assets/Development/Art/Shaders/HighJumpNet.shader
    /// уже существует (пишется параллельным агентом). Если шейдер не найден — Debug.LogError и выход.
    /// Может быть вызван как отдельным пунктом меню, так и из BuildHighJumpSceneEditor.BuildHighJumpScene()
    /// (см. вызов SetupHighJumpNetEditor.Setup() в конце сборки сцены).
    /// </summary>
    public static class SetupHighJumpNetEditor
    {
        private const string MaterialsFolder = "Assets/Development/Art/Materials";

        private const string NetShaderName = "FlatOutLike/HighJumpNet";
        private const string NetMaterialPath    = MaterialsFolder + "/HighJumpNet.mat";
        private const string StripeMaterialPath = MaterialsFolder + "/HighJumpNetStripe.mat";
        private const string PylonMaterialPath   = MaterialsFolder + "/HighJumpPylon.mat";

        // ── Геометрия полотна сетки ─────────────────────────────────────────────
        private static readonly Vector3 NetPanelPosition = new Vector3(0f, 30f, 130f);
        private static readonly Vector2 NetPanelSize      = new Vector2(44f, 60f);
        private static readonly Vector2 NetTiling         = new Vector2(88f, 120f); // ~0.5 м на ячейку

        // ── Горизонтальные полосы-отметки каждые 10 м ───────────────────────────
        private static readonly int[] StripeHeights = { 10, 20, 30, 40, 50, 60 };
        private const float StripeWidth  = 44f;
        private const float StripeHeight = 0.35f;
        private const float StripeZ      = 129.9f; // чуть выдвинута к камере, чтобы не з-файтить с полотном

        // ── Цифры высоты ─────────────────────────────────────────────────────────
        private const float LabelZ            = 129.8f;
        private const float LabelYOffset      = 1.5f;   // цифра над полосой
        private const float LabelFontSize      = 3f;     // высота символов ~3 м
        private const float LabelWidth        = 12f;
        private const float LabelHeightBox    = 4f;

        // ── Пилоны ───────────────────────────────────────────────────────────────
        private const float PylonX      = 23f;
        private const float PylonHeight = 62f;
        private const float PylonWidth  = 1.2f;
        private const float PylonZ      = 130f;
        private static readonly Color PylonColor = new Color(0.25f, 0.27f, 0.3f);

        [MenuItem("FlatOutLike/Setup/Setup HighJump Measurement Net (Step 7)")]
        public static void SetupMenuItem()
        {
            Setup();
        }

        /// <summary>Публичная точка входа без диалогов — можно вызывать из других Wizard'ов.</summary>
        public static void Setup()
        {
            var netShader = Shader.Find(NetShaderName);
            if (netShader == null)
            {
                Debug.LogError("[Editor] Шейдер " + NetShaderName + " не найден. " +
                                "Убедись, что Assets/Development/Art/Shaders/HighJumpNet.shader уже создан. Отмена.");
                return;
            }

            Undo.SetCurrentGroupName("Setup HighJump Measurement Net");
            int undoGroup = Undo.GetCurrentGroup();

            EnsureMaterialsFolder();

            var netMaterial    = GetOrCreateNetMaterial(netShader);
            var stripeMaterial = GetOrCreateStripeMaterial();
            var pylonMaterial  = GetOrCreatePylonMaterial();

            var root = FindOrCreateRoot();

            CreateNetPanel(root, netMaterial);
            CreateStripesAndLabels(root, stripeMaterial);
            CreatePylons(root, pylonMaterial);

            var scene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);

            Undo.CollapseUndoOperations(undoGroup);
            Debug.Log("[Editor] MeasurementNet собран.");
        }

        // ─────────────────────────────────────────────────────────────────────

        private static void EnsureMaterialsFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Development/Art"))
                AssetDatabase.CreateFolder("Assets/Development", "Art");
            if (!AssetDatabase.IsValidFolder(MaterialsFolder))
                AssetDatabase.CreateFolder("Assets/Development/Art", "Materials");
        }

        private static Material GetOrCreateNetMaterial(Shader netShader)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(NetMaterialPath);
            if (material == null)
            {
                material = new Material(netShader);
                AssetDatabase.CreateAsset(material, NetMaterialPath);
                Debug.Log("[Editor] Создан материал " + NetMaterialPath);
            }

            if (material.HasProperty("_Tiling"))
                material.SetVector("_Tiling", new Vector4(NetTiling.x, NetTiling.y, 0f, 0f));

            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            return material;
        }

        private static Material GetOrCreateStripeMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(StripeMaterialPath);
            if (material == null)
            {
                var litShader = Shader.Find("Universal Render Pipeline/Lit");
                if (litShader == null)
                {
                    Debug.LogError("[Editor] Шейдер 'Universal Render Pipeline/Lit' не найден — материал полос не создан.");
                    return null;
                }
                material = new Material(litShader);
                material.name = "HighJumpNetStripe";
                AssetDatabase.CreateAsset(material, StripeMaterialPath);
                Debug.Log("[Editor] Создан материал " + StripeMaterialPath);
            }

            material.color = Color.white;
            SetSurfaceOpaque(material);

            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            return material;
        }

        private static Material GetOrCreatePylonMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(PylonMaterialPath);
            if (material == null)
            {
                var litShader = Shader.Find("Universal Render Pipeline/Lit");
                if (litShader == null)
                {
                    Debug.LogError("[Editor] Шейдер 'Universal Render Pipeline/Lit' не найден — материал пилонов не создан.");
                    return null;
                }
                material = new Material(litShader);
                material.name = "HighJumpPylon";
                AssetDatabase.CreateAsset(material, PylonMaterialPath);
                Debug.Log("[Editor] Создан материал " + PylonMaterialPath);
            }

            material.color = PylonColor;
            if (material.HasProperty("_Metallic"))
                material.SetFloat("_Metallic", 0.6f);
            if (material.HasProperty("_Smoothness"))
                material.SetFloat("_Smoothness", 0.4f);
            SetSurfaceOpaque(material);

            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            return material;
        }

        private static void SetSurfaceOpaque(Material material)
        {
            // URP/Lit Opaque: стандартные ключевые слова и рендер-настройки.
            material.SetFloat("_Surface", 0f); // 0 = Opaque
            material.SetOverrideTag("RenderType", "Opaque");
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
            material.SetInt("_ZWrite", 1);
            material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
        }

        private static GameObject FindOrCreateRoot()
        {
            var root = GameObject.Find("MeasurementNet");
            if (root == null)
            {
                root = new GameObject("MeasurementNet");
                Undo.RegisterCreatedObjectUndo(root, "Create MeasurementNet");
                Debug.Log("[Editor] Создан корневой объект MeasurementNet.");
            }
            else
            {
                Debug.Log("[Editor] MeasurementNet уже есть, переиспользуем.");
            }

            return root;
        }

        private static void CreateNetPanel(GameObject root, Material netMaterial)
        {
            var existing = root.transform.Find("NetPanel");
            GameObject go;

            if (existing == null)
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Undo.RegisterCreatedObjectUndo(go, "Create NetPanel");
                go.name = "NetPanel";
                go.transform.SetParent(root.transform, false);
                Debug.Log("[Editor] Создан NetPanel.");
            }
            else
            {
                go = existing.gameObject;
                Debug.Log("[Editor] NetPanel уже есть, обновляем.");
            }

            RemoveCollider(go);

            go.transform.position   = NetPanelPosition;
            go.transform.rotation   = Quaternion.identity; // квад смотрит в -Z по умолчанию — достаточно, материал двусторонний (Cull Off)
            go.transform.localScale = new Vector3(NetPanelSize.x, NetPanelSize.y, 1f);

            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = netMaterial;
        }

        private static void CreateStripesAndLabels(GameObject root, Material stripeMaterial)
        {
            foreach (int height in StripeHeights)
            {
                CreateStripe(root, height, stripeMaterial);
                CreateLabel(root, height);
            }
        }

        private static void CreateStripe(GameObject root, int height, Material stripeMaterial)
        {
            string objName = "Stripe_" + height;
            var existing = root.transform.Find(objName);
            GameObject go;

            if (existing == null)
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Undo.RegisterCreatedObjectUndo(go, "Create " + objName);
                go.name = objName;
                go.transform.SetParent(root.transform, false);
                Debug.Log("[Editor] Создана " + objName + ".");
            }
            else
            {
                go = existing.gameObject;
            }

            RemoveCollider(go);

            go.transform.position   = new Vector3(0f, height, StripeZ);
            go.transform.rotation   = Quaternion.identity;
            go.transform.localScale = new Vector3(StripeWidth, StripeHeight, 1f);

            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = stripeMaterial;
        }

        private static void CreateLabel(GameObject root, int height)
        {
            string objName = "Label_" + height;
            var existing = root.transform.Find(objName);
            GameObject go;
            TextMeshPro tmp;

            if (existing == null)
            {
                go = new GameObject(objName);
                Undo.RegisterCreatedObjectUndo(go, "Create " + objName);
                go.transform.SetParent(root.transform, false);
                tmp = go.AddComponent<TextMeshPro>();
                Debug.Log("[Editor] Создана " + objName + ".");
            }
            else
            {
                go = existing.gameObject;
                tmp = go.GetComponent<TextMeshPro>();
                if (tmp == null)
                    tmp = go.AddComponent<TextMeshPro>();
            }

            // Мировой Y цифры = реальная высота в метрах (важно для сверки с PeakHeight).
            go.transform.position = new Vector3(0f, height + LabelYOffset, LabelZ);
            // TMP-квад по умолчанию смотрит в +Z — разворачиваем на 180° по Y,
            // чтобы цифра была лицом к -Z (навстречу летящему водителю).
            go.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

            tmp.text                    = height.ToString();
            tmp.fontSize                 = LabelFontSize;
            tmp.fontStyle                = FontStyles.Bold;
            tmp.color                    = Color.white;
            tmp.alignment                 = TextAlignmentOptions.Center;
            tmp.rectTransform.sizeDelta   = new Vector2(LabelWidth, LabelHeightBox);
        }

        private static void CreatePylons(GameObject root, Material pylonMaterial)
        {
            CreatePylon(root, "Pylon_Left", -PylonX, pylonMaterial);
            CreatePylon(root, "Pylon_Right", PylonX, pylonMaterial);
        }

        private static void CreatePylon(GameObject root, string objName, float x, Material pylonMaterial)
        {
            var existing = root.transform.Find(objName);
            GameObject go;

            if (existing == null)
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Undo.RegisterCreatedObjectUndo(go, "Create " + objName);
                go.name = objName;
                go.transform.SetParent(root.transform, false);
                Debug.Log("[Editor] Создан " + objName + ".");
            }
            else
            {
                go = existing.gameObject;
            }

            RemoveCollider(go);

            // Низ пилона на y=0, значит центр на y = PylonHeight / 2.
            go.transform.position   = new Vector3(x, PylonHeight * 0.5f, PylonZ);
            go.transform.rotation   = Quaternion.identity;
            go.transform.localScale = new Vector3(PylonWidth, PylonHeight, PylonWidth);

            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = pylonMaterial;
        }

        private static void RemoveCollider(GameObject go)
        {
            var collider = go.GetComponent<Collider>();
            if (collider != null)
                Undo.DestroyObjectImmediate(collider);
        }
    }
}
#endif
