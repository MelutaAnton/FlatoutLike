#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using FlatOutLike.Controllers.Camera;
using FlatOutLike.Controllers.Car;
using FlatOutLike.Data.Configs.Camera;
using FlatOutLike.Installers;

namespace FlatOutLike.Editor.Setup
{
    public static class BuildDrivingTestSceneEditor
    {
        private const string ScenePath             = "Assets/Development/Scenes/_TestDriving.unity";
        private const string CameraConfigPath      = "Assets/Development/Data/Configs/Camera/ChaseCameraConfig_Default.asset";
        private const string VehiclesPrefabsFolder = "Assets/Development/Prefabs/Vehicles";

        [MenuItem("FlatOutLike/Setup/Build Driving Test Scene")]
        public static void BuildDrivingTestScene()
        {
            Undo.SetCurrentGroupName("Build Driving Test Scene");
            int undoGroup = Undo.GetCurrentGroup();

            // ── 1. Open or create scene ───────────────────────────────────────
            var scene = OpenOrCreateScene();

            // ── 2. Ground plane ───────────────────────────────────────────────
            var existingGround = GameObject.Find("Ground");
            if (existingGround == null)
            {
                var plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
                Undo.RegisterCreatedObjectUndo(plane, "Create Ground Plane");
                plane.name = "Ground";
                plane.transform.localScale = new Vector3(20f, 1f, 20f);
                Debug.Log("[Editor] Created Ground plane (200x200 units).");
            }
            else
            {
                Debug.Log("[Editor] Ground plane already exists, skipping.");
            }

            // ── 3. Directional light ──────────────────────────────────────────
            Light existingDirLight = null;
            var allLights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            foreach (var l in allLights)
            {
                if (l.type == LightType.Directional)
                {
                    existingDirLight = l;
                    break;
                }
            }

            if (existingDirLight == null)
            {
                var lightGo = new GameObject("Directional Light");
                Undo.RegisterCreatedObjectUndo(lightGo, "Create Directional Light");
                var lightComp = Undo.AddComponent<Light>(lightGo);
                lightComp.type    = LightType.Directional;
                lightComp.intensity = 1f;
                lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
                Debug.Log("[Editor] Created Directional Light.");
            }
            else
            {
                Debug.Log("[Editor] Directional Light already exists, skipping.");
            }

            // ── 4. Main Camera + ChaseCarCameraBehaviour ──────────────────────
            Camera mainCam = Camera.main;
            GameObject camGo;

            if (mainCam == null)
            {
                camGo = new GameObject("Main Camera");
                Undo.RegisterCreatedObjectUndo(camGo, "Create Main Camera");
                camGo.tag = "MainCamera";
                Undo.AddComponent<Camera>(camGo);
                Undo.AddComponent<AudioListener>(camGo);
                Debug.Log("[Editor] Created Main Camera GameObject.");
            }
            else
            {
                camGo = mainCam.gameObject;
                Debug.Log("[Editor] Found existing Main Camera, reusing.");
            }

            // Ensure ChaseCarCameraBehaviour is on the camera GO
            var chaseBehaviour = camGo.GetComponent<ChaseCarCameraBehaviour>();
            if (chaseBehaviour == null)
            {
                chaseBehaviour = Undo.AddComponent<ChaseCarCameraBehaviour>(camGo);
                Debug.Log("[Editor] Added ChaseCarCameraBehaviour to camera.");
            }
            else
            {
                Debug.Log("[Editor] ChaseCarCameraBehaviour already present on camera.");
            }

            // Fill _config on ChaseCarCameraBehaviour
            var cameraConfig = AssetDatabase.LoadAssetAtPath<ChaseCameraConfig>(CameraConfigPath);
            if (cameraConfig == null)
            {
                Debug.LogWarning("[Editor] ChaseCameraConfig_Default not found at " + CameraConfigPath +
                                 ". Run 'FlatOutLike/Configs/Create Default ChaseCameraConfig' first. Field _config will remain null.");
            }
            else
            {
                var camSo = new SerializedObject(chaseBehaviour);
                camSo.FindProperty("_config").objectReferenceValue = cameraConfig;
                camSo.ApplyModifiedProperties();
                Debug.Log("[Editor] Assigned ChaseCameraConfig_Default to ChaseCarCameraBehaviour._config.");
            }

            // ── 5. GameLifetimeScope ──────────────────────────────────────────
            var existingScope = Object.FindAnyObjectByType<GameLifetimeScope>();
            if (existingScope == null)
            {
                var scopeGo = new GameObject("GameLifetimeScope");
                Undo.RegisterCreatedObjectUndo(scopeGo, "Create GameLifetimeScope");
                Undo.AddComponent<GameLifetimeScope>(scopeGo);
                Debug.Log("[Editor] Created GameLifetimeScope.");
            }
            else
            {
                Debug.Log("[Editor] GameLifetimeScope already exists, skipping.");
            }

            // ── 6. GameBootstrap ──────────────────────────────────────────────
            var existingBootstrap = Object.FindAnyObjectByType<FlatOutLike.GameBootstrap>();
            if (existingBootstrap == null)
            {
                var bootstrapGo = new GameObject("GameBootstrap");
                Undo.RegisterCreatedObjectUndo(bootstrapGo, "Create GameBootstrap");
                Undo.AddComponent<FlatOutLike.GameBootstrap>(bootstrapGo);
                Debug.Log("[Editor] Created GameBootstrap.");
            }
            else
            {
                Debug.Log("[Editor] GameBootstrap already exists, skipping.");
            }

            // ── 7. Car instance ───────────────────────────────────────────────
            var carPrefab = FindFirstCarPrefab();
            if (carPrefab == null)
            {
                Debug.LogWarning("[Editor] No car prefabs found in " + VehiclesPrefabsFolder +
                                 ". Run 'FlatOutLike/Setup/Build Car Prefab From Selected FBX' first. Skipping car spawn.");
            }
            else
            {
                var existingCar = Object.FindAnyObjectByType<CarController>();
                if (existingCar == null)
                {
                    var carInstance = (GameObject)PrefabUtility.InstantiatePrefab(carPrefab);
                    Undo.RegisterCreatedObjectUndo(carInstance, "Instantiate Car");
                    carInstance.transform.position = new Vector3(0f, 1f, 0f);
                    Debug.Log("[Editor] Instantiated car prefab '" + carPrefab.name +
                              "' at (0, 1, 0).");

                    // Set _target on ChaseCarCameraBehaviour via SerializedObject
                    var targetSo = new SerializedObject(chaseBehaviour);
                    targetSo.FindProperty("_target").objectReferenceValue = carInstance.transform;
                    targetSo.ApplyModifiedProperties();
                    Debug.Log("[Editor] Assigned car transform to ChaseCarCameraBehaviour._target.");
                }
                else
                {
                    Debug.Log("[Editor] CarController already present in scene, skipping car spawn.");

                    // Still ensure _target is wired if it's missing
                    var targetSo = new SerializedObject(chaseBehaviour);
                    if (targetSo.FindProperty("_target").objectReferenceValue == null)
                    {
                        targetSo.FindProperty("_target").objectReferenceValue = existingCar.transform;
                        targetSo.ApplyModifiedProperties();
                        Debug.Log("[Editor] Wired existing car to ChaseCarCameraBehaviour._target.");
                    }
                }
            }

            // ── 8. Save scene ─────────────────────────────────────────────────
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("[Editor] Scene saved at " + ScenePath);

            Undo.CollapseUndoOperations(undoGroup);

            Debug.Log("[Editor] Driving test scene build complete.");
        }

        // ─────────────────────────────────────────────────────────────────────
        private static Scene OpenOrCreateScene()
        {
            if (System.IO.File.Exists(
                    System.IO.Path.Combine(
                        System.IO.Directory.GetCurrentDirectory(), ScenePath)))
            {
                var existing = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                Debug.Log("[Editor] Opened existing _TestDriving.unity at " + ScenePath);
                return existing;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("[Editor] Created new _TestDriving.unity at " + ScenePath);
            return scene;
        }

        private static GameObject FindFirstCarPrefab()
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { VehiclesPrefabsFolder });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string fileName = System.IO.Path.GetFileNameWithoutExtension(path);
                if (fileName.StartsWith("Car_", System.StringComparison.OrdinalIgnoreCase))
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab != null)
                    {
                        Debug.Log("[Editor] Found car prefab: " + path);
                        return prefab;
                    }
                }
            }
            return null;
        }
    }
}
#endif
