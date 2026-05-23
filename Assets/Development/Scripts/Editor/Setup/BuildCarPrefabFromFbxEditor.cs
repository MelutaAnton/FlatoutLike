#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using FlatOutLike.Controllers.Car;
using FlatOutLike.Data.Configs.Car;

namespace FlatOutLike.Editor.Setup
{
    /// <summary>
    /// Select a source prefab/FBX asset in the Project window (e.g. from
    /// Assets/ThirdParty/Low Poly Destructible Cars/Prefabs/) and run this
    /// menu item to wrap it in a Car_ prefab under Assets/Development/Prefabs/Vehicles/.
    /// </summary>
    public static class BuildCarPrefabFromFbxEditor
    {
        private const string ThirdPartyCarPath   = "Assets/ThirdParty/Low Poly Destructible Cars/";
        private const string StatsConfigPath     = "Assets/Development/Data/Configs/Car/CarStatsConfig_Default.asset";
        private const string VehiclesPrefabsRoot = "Assets/Development/Prefabs/Vehicles";

        private const float WheelRadius            = 0.35f;
        private const float WheelSuspensionDist    = 0.3f;
        private const float WheelDampingRate       = 0.25f;

        [MenuItem("FlatOutLike/Setup/Build Car Prefab From Selected FBX")]
        public static void BuildCarPrefabFromSelected()
        {
            Undo.SetCurrentGroupName("Build Car Prefab From Selected FBX");
            int undoGroup = Undo.GetCurrentGroup();

            // ── 1. Validate selection ─────────────────────────────────────────
            var selected = Selection.activeObject;
            if (selected == null)
            {
                EditorUtility.DisplayDialog("Build Car Prefab",
                    "Nothing selected. Select a prefab or FBX asset in the Project window.", "OK");
                return;
            }

            string assetPath = AssetDatabase.GetAssetPath(selected);
            if (string.IsNullOrEmpty(assetPath))
            {
                EditorUtility.DisplayDialog("Build Car Prefab",
                    "Selected object has no asset path. Select a prefab or FBX in the Project window.", "OK");
                return;
            }

            bool isValidType = assetPath.EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase)
                            || assetPath.EndsWith(".fbx",    System.StringComparison.OrdinalIgnoreCase)
                            || assetPath.EndsWith(".FBX",    System.StringComparison.OrdinalIgnoreCase);

            if (!isValidType)
            {
                EditorUtility.DisplayDialog("Build Car Prefab",
                    "Selected asset is not a .prefab or .fbx file.\nPath: " + assetPath, "OK");
                return;
            }

            // ── 2. Warn if not from expected ThirdParty path ──────────────────
            if (!assetPath.StartsWith(ThirdPartyCarPath, System.StringComparison.OrdinalIgnoreCase))
            {
                bool proceed = EditorUtility.DisplayDialog("Build Car Prefab",
                    "Selected asset is not under:\n" + ThirdPartyCarPath +
                    "\n\nPath: " + assetPath +
                    "\n\nContinue anyway?",
                    "Yes", "Cancel");
                if (!proceed) return;
            }

            // ── 3. Derive mesh name and target prefab path ────────────────────
            string meshName  = Path.GetFileNameWithoutExtension(assetPath);
            string targetPath = VehiclesPrefabsRoot + "/Car_" + meshName + ".prefab";
            Debug.Log("[Editor] Target prefab path: " + targetPath);

            EnsureFolder("Assets/Development/Prefabs", "Vehicles");

            // ── 4. Check if prefab already exists ─────────────────────────────
            if (AssetDatabase.LoadAssetAtPath<GameObject>(targetPath) != null)
            {
                bool overwrite = EditorUtility.DisplayDialog("Build Car Prefab",
                    "Prefab '" + targetPath + "' already exists. Overwrite?", "Yes", "Cancel");
                if (!overwrite) return;
            }

            // ── 5. Load required config ───────────────────────────────────────
            var statsConfig = AssetDatabase.LoadAssetAtPath<CarStatsConfig>(StatsConfigPath);
            if (statsConfig == null)
            {
                EditorUtility.DisplayDialog("Missing Config",
                    "CarStatsConfig_Default not found at:\n" + StatsConfigPath +
                    "\n\nRun 'FlatOutLike/Configs/Create Default CarStatsConfig' first.", "OK");
                return;
            }

            // ── 6. Load source asset ──────────────────────────────────────────
            var sourceAsset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (sourceAsset == null)
            {
                EditorUtility.DisplayDialog("Build Car Prefab",
                    "Could not load selected asset as GameObject.\nPath: " + assetPath, "OK");
                return;
            }

            // ── 7. Build in scene ─────────────────────────────────────────────
            // Root GO
            var root = new GameObject("Car_" + meshName);
            Undo.RegisterCreatedObjectUndo(root, "Create Car Root");

            // Rigidbody
            var rb = Undo.AddComponent<Rigidbody>(root);
            Debug.Log("[Editor] Added Rigidbody to root.");

            // Mesh child — instantiate source prefab/FBX as child (preserves GUID link)
            var meshGo = (GameObject)PrefabUtility.InstantiatePrefab(sourceAsset);
            Undo.RegisterCreatedObjectUndo(meshGo, "Instantiate Mesh Child");
            meshGo.name = meshName + "_Mesh";
            meshGo.transform.SetParent(root.transform, false);
            Debug.Log("[Editor] Instantiated mesh child '" + meshGo.name + "'.");

            // ── 8. Compute bounding box from all renderers ────────────────────
            var renderers = meshGo.GetComponentsInChildren<Renderer>(true);
            Bounds worldBounds = new Bounds(root.transform.position, Vector3.zero);
            bool hasBounds = false;
            foreach (var r in renderers)
            {
                if (!hasBounds)
                {
                    worldBounds = r.bounds;
                    hasBounds   = true;
                }
                else
                {
                    worldBounds.Encapsulate(r.bounds);
                }
            }

            // Root transform is identity at this point, so world == local
            Vector3 bMin = worldBounds.min;
            Vector3 bMax = worldBounds.max;

            if (!hasBounds)
            {
                // Fallback: compact car box when no renderers found
                bMin = new Vector3(-0.9f, 0f,  -2.0f);
                bMax = new Vector3( 0.9f, 0.7f,  2.0f);
                Debug.LogWarning("[Editor] No renderers found; using fallback bounding box for wheel placement.");
            }

            Debug.Log("[Editor] Bounding box: min=" + bMin + "  max=" + bMax);

            // ── 8.5. Add BoxCollider on body ──────────────────────────────────
            // WheelColliders only handle suspension, not body-to-body contact.
            // The car needs a primary collider so OnCollisionEnter fires in step 3.
            // Sized to full bounds (includes wheels) — tighten manually in Inspector
            // if a body-only box is required.
            var bodyCollider = Undo.AddComponent<BoxCollider>(root);
            bodyCollider.center = (bMin + bMax) * 0.5f;
            bodyCollider.size   = bMax - bMin;
            Debug.Log("[Editor] Added BoxCollider on root (center=" + bodyCollider.center +
                      ", size=" + bodyCollider.size + ").");

            // ── 9. Create wheel GameObjects ───────────────────────────────────
            // WheelCollider pivot must sit at the CENTER of the wheel, not at the
            // ground line. In rest pose the collider projects radius + suspensionDistance/2
            // downward, so a pivot at bounds.min.y would put the contact point below
            // the floor. Lift pivots up by WheelRadius so they land in the wheel hub.
            float wheelY = bMin.y + WheelRadius;
            var wheelFL = CreateWheelGo(root, "WheelFL", new Vector3(bMin.x, wheelY, bMax.z));
            var wheelFR = CreateWheelGo(root, "WheelFR", new Vector3(bMax.x, wheelY, bMax.z));
            var wheelRL = CreateWheelGo(root, "WheelRL", new Vector3(bMin.x, wheelY, bMin.z));
            var wheelRR = CreateWheelGo(root, "WheelRR", new Vector3(bMax.x, wheelY, bMin.z));

            var wcFL = wheelFL.GetComponent<WheelCollider>();
            var wcFR = wheelFR.GetComponent<WheelCollider>();
            var wcRL = wheelRL.GetComponent<WheelCollider>();
            var wcRR = wheelRR.GetComponent<WheelCollider>();

            Debug.Log("[Editor] Created 4 wheel GameObjects with WheelColliders.");

            // ── 10. Add CarController and fill SerializedFields ───────────────
            var carController = Undo.AddComponent<CarController>(root);
            Debug.Log("[Editor] Added CarController to root.");

            var so = new SerializedObject(carController);

            so.FindProperty("_rigidbody").objectReferenceValue = rb;

            // _driveWheels: RL + RR (rear-wheel drive)
            var driveProp = so.FindProperty("_driveWheels");
            driveProp.arraySize = 2;
            driveProp.GetArrayElementAtIndex(0).objectReferenceValue = wcRL;
            driveProp.GetArrayElementAtIndex(1).objectReferenceValue = wcRR;

            // _steerWheels: FL + FR
            var steerProp = so.FindProperty("_steerWheels");
            steerProp.arraySize = 2;
            steerProp.GetArrayElementAtIndex(0).objectReferenceValue = wcFL;
            steerProp.GetArrayElementAtIndex(1).objectReferenceValue = wcFR;

            // _wheelVisuals: try to auto-bind from mesh hierarchy by name heuristic.
            // Searches for transforms whose name contains "wheel" or "tire" (case-insensitive),
            // picks the 4 most extreme by |dx|+|dz| from bounds center, classifies into
            // FL/FR/RL/RR, and fills the array in iteration order: drive ([RL, RR])
            // then steer ([FL, FR]).
            var visualsOrdered = TryFindWheelVisuals(meshGo, bMin, bMax);
            var visualsProp = so.FindProperty("_wheelVisuals");
            if (visualsOrdered != null)
            {
                visualsProp.arraySize = visualsOrdered.Length;
                for (int i = 0; i < visualsOrdered.Length; i++)
                    visualsProp.GetArrayElementAtIndex(i).objectReferenceValue = visualsOrdered[i];
                Debug.Log("[Editor] Bound " + visualsOrdered.Length +
                          " wheel visuals in order [RL, RR, FL, FR].");
            }
            else
            {
                visualsProp.arraySize = 0;
                Debug.LogWarning("[Editor] Could not auto-find 4 wheel visuals in mesh hierarchy. " +
                                 "Drag them into CarController._wheelVisuals manually if needed.");
            }

            // _config
            so.FindProperty("_config").objectReferenceValue = statsConfig;

            so.ApplyModifiedProperties();
            Debug.Log("[Editor] Filled CarController SerializedFields (_rigidbody, _driveWheels[2], _steerWheels[2], _config).");

            // ── 11. Save prefab ───────────────────────────────────────────────
            var prefabAsset = PrefabUtility.SaveAsPrefabAssetAndConnect(
                root, targetPath, InteractionMode.AutomatedAction);
            Debug.Log("[Editor] Saved prefab at " + targetPath);

            // ── 12. Register prefab with Undo ─────────────────────────────────
            if (prefabAsset != null)
                Undo.RegisterCreatedObjectUndo(prefabAsset, "Build Car Prefab");

            // ── 13. Destroy temp root in scene ────────────────────────────────
            Object.DestroyImmediate(root);
            Debug.Log("[Editor] Cleaned up temporary scene object.");

            // ── 14. Refresh database ──────────────────────────────────────────
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // ── 15. Ping and select ───────────────────────────────────────────
            var result = AssetDatabase.LoadAssetAtPath<GameObject>(targetPath);
            Selection.activeObject = result;
            EditorGUIUtility.PingObject(result);

            Undo.CollapseUndoOperations(undoGroup);

            Debug.Log("[Editor] Car prefab build complete: " + targetPath +
                      "  (driveWheels=2, steerWheels=2)");
        }

        // ─────────────────────────────────────────────────────────────────────
        private static GameObject CreateWheelGo(GameObject parent, string name, Vector3 localPos)
        {
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Create " + name);
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = localPos;

            var wc = Undo.AddComponent<WheelCollider>(go);
            wc.radius             = WheelRadius;
            wc.suspensionDistance = WheelSuspensionDist;
            wc.wheelDampingRate   = WheelDampingRate;

            return go;
        }

        private static Transform[] TryFindWheelVisuals(GameObject meshGo, Vector3 bMin, Vector3 bMax)
        {
            var all = meshGo.GetComponentsInChildren<Transform>(true);
            var candidates = new List<Transform>();
            foreach (var t in all)
            {
                if (t == null) continue;
                string n = t.name.ToLowerInvariant();
                if (n.Contains("wheel") || n.Contains("tire"))
                    candidates.Add(t);
            }
            if (candidates.Count < 4) return null;

            Vector3 boundsCenter = (bMin + bMax) * 0.5f;
            candidates.Sort((a, b) =>
            {
                float da = Mathf.Abs(a.position.x - boundsCenter.x) + Mathf.Abs(a.position.z - boundsCenter.z);
                float db = Mathf.Abs(b.position.x - boundsCenter.x) + Mathf.Abs(b.position.z - boundsCenter.z);
                return db.CompareTo(da); // descending — most extreme first
            });

            Transform fl = null, fr = null, rl = null, rr = null;
            for (int i = 0; i < 4; i++)
            {
                var t = candidates[i];
                bool isLeft  = t.position.x < boundsCenter.x;
                bool isFront = t.position.z > boundsCenter.z;
                if      ( isFront &&  isLeft) fl = t;
                else if ( isFront && !isLeft) fr = t;
                else if (!isFront &&  isLeft) rl = t;
                else                          rr = t;
            }
            if (fl == null || fr == null || rl == null || rr == null) return null;

            // Order matches CarController iteration: drive ([RL, RR]) then steer ([FL, FR]).
            return new[] { rl, rr, fl, fr };
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
