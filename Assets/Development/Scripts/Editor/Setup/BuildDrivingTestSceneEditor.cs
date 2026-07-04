#if UNITY_EDITOR
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
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

            // ── 4. Main Camera + Cinemachine Chase Camera ─────────────────────
            // Camera rig мигрировал на Cinemachine 3.x: CinemachineBrain на Main Camera +
            // CM_ChaseCamera (CinemachineCamera + CinemachineChaseCameraBehaviour), собранные
            // переиспользуемым SetupCinemachineCameraRigEditor. Эта тестовая сцена не использует
            // CameraRig/ICameraService, поэтому Follow/LookAt выставляем прямо на CinemachineCamera.
            SetupCinemachineCameraRigEditor.EnsureBrain();

            var cameraConfig = AssetDatabase.LoadAssetAtPath<ChaseCameraConfig>(CameraConfigPath);
            if (cameraConfig == null)
                Debug.LogWarning("[Editor] ChaseCameraConfig_Default not found at " + CameraConfigPath +
                                 ". Run 'FlatOutLike/Configs/Create Default ChaseCameraConfig' first. Field _config will remain null.");

            var chaseBehaviour = SetupCinemachineCameraRigEditor.EnsureChaseCamera(cameraConfig, priority: 10);
            var chaseCamera = chaseBehaviour.GetComponent<CinemachineCamera>();

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

                    // Set Follow/LookAt directly on the CinemachineCamera (no CameraRig in this scene)
                    if (chaseCamera != null)
                    {
                        Undo.RecordObject(chaseCamera, "Set Chase Camera Follow Target");
                        chaseCamera.Follow = carInstance.transform;
                        chaseCamera.LookAt = carInstance.transform;
                        EditorUtility.SetDirty(chaseCamera);
                        Debug.Log("[Editor] Assigned car transform to CinemachineCamera.Follow/LookAt.");
                    }
                }
                else
                {
                    Debug.Log("[Editor] CarController already present in scene, skipping car spawn.");

                    // Still ensure Follow/LookAt are wired if missing
                    if (chaseCamera != null && chaseCamera.Follow == null)
                    {
                        Undo.RecordObject(chaseCamera, "Set Chase Camera Follow Target");
                        chaseCamera.Follow = existingCar.transform;
                        chaseCamera.LookAt = existingCar.transform;
                        EditorUtility.SetDirty(chaseCamera);
                        Debug.Log("[Editor] Wired existing car to CinemachineCamera.Follow/LookAt.");
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
