#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using FlatOutLike.Controllers.Car;
using FlatOutLike.Controllers.Enemies;
using FlatOutLike.Data.Configs.Car;
using FlatOutLike.Data.Configs.Enemies;
using FlatOutLike.Installers;

namespace FlatOutLike.Editor.Setup
{
    /// <summary>
    /// Instantiates an enemy car in the active test scene, adds EnemyCarController,
    /// fills all its SerializedField references and converts its body materials
    /// to the damage shader automatically.
    ///
    /// Finding the player car: first CarController in the scene that does NOT have
    /// an EnemyCarController sibling — this is robust to future multi-enemy setups.
    ///
    /// Idempotent: if EnemyCarController already exists in the scene only the
    /// refs are refreshed; no new GameObject is instantiated.
    ///
    /// Run AFTER:
    ///   FlatOutLike/Setup/Build Driving Test Scene
    ///   FlatOutLike/Configs/Create Default EnemyPersonalityConfig
    ///
    /// Run BEFORE:
    ///   FlatOutLike/Setup/Fill GameLifetimeScope Refs (Step 4)
    /// </summary>
    public static class SetupEnemyCarEditor
    {
        private const string PersonalityConfigPath = "Assets/Development/Data/Configs/Enemies/EnemyPersonalityConfig_Aggressor.asset";
        private const string VehiclesPrefabsFolder = "Assets/Development/Prefabs/Vehicles";

        [MenuItem("FlatOutLike/Setup/Setup Enemy Car (Step 4)")]
        public static void SetupEnemyCar()
        {
            Undo.SetCurrentGroupName("Setup Enemy Car (Step 4)");
            int undoGroup = Undo.GetCurrentGroup();

            // ── 1. Check scene has GameLifetimeScope ──────────────────────────
            var scope = Object.FindAnyObjectByType<GameLifetimeScope>();
            if (scope == null)
            {
                Debug.LogWarning("[Editor] GameLifetimeScope not found in scene. " +
                                 "Run 'FlatOutLike/Setup/Build Driving Test Scene' first.");
                return;
            }

            // ── 2. Find player CarController (no EnemyCarController sibling) ──
            var playerCar = FindPlayerCar();
            if (playerCar == null)
            {
                Debug.LogWarning("[Editor] No player CarController found in scene. " +
                                 "Run 'FlatOutLike/Setup/Build Driving Test Scene' first.");
                return;
            }
            Debug.Log("[Editor] Found player car: " + playerCar.gameObject.name);

            // ── 3. Idempotency: EnemyCarController already in scene ───────────
            var existingEnemy = Object.FindAnyObjectByType<EnemyCarController>();
            if (existingEnemy != null)
            {
                Debug.Log("[Editor] EnemyCarController already in scene on '" +
                          existingEnemy.gameObject.name + "'. Refreshing refs only.");
                FillEnemyCarRefs(existingEnemy, playerCar);
                FinalizeScene();
                Undo.CollapseUndoOperations(undoGroup);
                Debug.Log("[Editor] Enemy car refs refreshed.");
                return;
            }

            // ── 4. Resolve car prefab ─────────────────────────────────────────
            // Prefer the exact same prefab that was used to spawn the player car.
            var prefabSource = PrefabUtility.GetCorrespondingObjectFromSource(playerCar.gameObject)
                               as GameObject;
            if (prefabSource == null)
            {
                Debug.Log("[Editor] Player car is not a plain prefab instance; " +
                          "falling back to Vehicles folder search.");
                prefabSource = FindFirstCarPrefab();
            }
            if (prefabSource == null)
            {
                Debug.LogWarning("[Editor] Could not find a car prefab in " + VehiclesPrefabsFolder +
                                 ". Run 'FlatOutLike/Setup/Build Car Prefab From Selected FBX' first.");
                return;
            }

            // ── 5. Instantiate enemy car ──────────────────────────────────────
            var enemyCarGo = (GameObject)PrefabUtility.InstantiatePrefab(prefabSource);
            Undo.RegisterCreatedObjectUndo(enemyCarGo, "Instantiate Enemy Car");
            enemyCarGo.name               = "EnemyCar";
            enemyCarGo.transform.position = new Vector3(0f, 1f, 25f);  // 25 m ahead of player
            enemyCarGo.transform.rotation = Quaternion.Euler(0f, 180f, 0f); // facing player
            Debug.Log("[Editor] Instantiated EnemyCar from prefab '" + prefabSource.name +
                      "' at (0, 1, 25) facing player.");

            // ── 6a. Set _carId = 1 on enemy's CarController via SerializedObject ─
            var enemyCar = enemyCarGo.GetComponent<CarController>();
            if (enemyCar != null)
            {
                var carSo = new SerializedObject(enemyCar);
                carSo.FindProperty("_carId").intValue = 1;
                carSo.ApplyModifiedProperties();
                Debug.Log("[Editor] Set CarController._carId = 1 on EnemyCar.");
            }
            else
            {
                Debug.LogWarning("[Editor] No CarController found on EnemyCar root. " +
                                 "Check that the car prefab has CarController on its root.");
            }

            // ── 6b. Add EnemyCarController ────────────────────────────────────
            // Using Undo.AddComponent so the addition is undoable.
            var enemyController = enemyCarGo.GetComponent<EnemyCarController>();
            if (enemyController == null)
            {
                enemyController = Undo.AddComponent<EnemyCarController>(enemyCarGo);
                Debug.Log("[Editor] Added EnemyCarController to EnemyCar.");
            }
            else
            {
                Debug.Log("[Editor] EnemyCarController already present on EnemyCar, refreshing refs.");
            }

            // ── 6c. Fill EnemyCarController SerializedFields ──────────────────
            FillEnemyCarRefs(enemyController, playerCar);

            // ── 7. Convert damage shader materials for enemy car ──────────────
            // Delegates to the extracted public method to avoid duplicating material
            // conversion logic and to avoid FindAnyObjectByType finding the wrong car.
            if (enemyCar != null)
            {
                SetupCarDamageShaderEditor.ConvertCar(enemyCar);
                Debug.Log("[Editor] Damage shader conversion applied to EnemyCar.");
            }

            // ── 8. Save scene ─────────────────────────────────────────────────
            FinalizeScene();

            Undo.CollapseUndoOperations(undoGroup);

            Debug.Log("[Editor] Enemy car setup complete. " +
                      "Next: run 'FlatOutLike/Setup/Fill GameLifetimeScope Refs (Step 4)' " +
                      "to register both cars in the DI container.");
        }

        // ─────────────────────────────────────────────────────────────────────
        /// <summary>
        /// Fills all SerializedField refs on EnemyCarController.
        /// Reads _config and _rigidbody from the sibling CarController via SerializedObject
        /// so we use exactly the already-assigned references, not a fresh GetComponent lookup.
        /// </summary>
        private static void FillEnemyCarRefs(EnemyCarController enemyController, CarController playerCar)
        {
            var enemyCarGo = enemyController.gameObject;
            var enemyCar   = enemyCarGo.GetComponent<CarController>();

            // Read _config and _rigidbody from CarController via SerializedObject —
            // these may differ from GetComponent results if the prefab has overrides.
            CarStatsConfig statsConfig = null;
            Rigidbody      rigidbody   = null;

            if (enemyCar != null)
            {
                var carSo   = new SerializedObject(enemyCar);
                statsConfig = carSo.FindProperty("_config").objectReferenceValue    as CarStatsConfig;
                rigidbody   = carSo.FindProperty("_rigidbody").objectReferenceValue as Rigidbody;
            }

            // Fallback if the SO fields are not yet wired (fresh prefab instance)
            if (rigidbody == null)
                rigidbody = enemyCarGo.GetComponent<Rigidbody>();

            var profile = AssetDatabase.LoadAssetAtPath<EnemyPersonalityConfig>(PersonalityConfigPath);
            if (profile == null)
                Debug.LogWarning("[Editor] EnemyPersonalityConfig_Aggressor not found at " +
                                 PersonalityConfigPath +
                                 ". Run 'FlatOutLike/Configs/Create Default EnemyPersonalityConfig' first. " +
                                 "_profile will remain null.");

            var so = new SerializedObject(enemyController);
            so.FindProperty("_car").objectReferenceValue         = enemyCar;
            so.FindProperty("_target").objectReferenceValue      = playerCar;
            so.FindProperty("_statsConfig").objectReferenceValue = statsConfig;
            so.FindProperty("_profile").objectReferenceValue     = profile;
            so.FindProperty("_rigidbody").objectReferenceValue   = rigidbody;
            so.ApplyModifiedProperties();

            Debug.Log("[Editor] EnemyCarController refs filled on '" + enemyCarGo.name + "'" +
                      " — _car="        + (enemyCar    != null ? enemyCar.name    : "NULL") +
                      ", _target="      + (playerCar   != null ? playerCar.name   : "NULL") +
                      ", _statsConfig=" + (statsConfig != null ? statsConfig.name : "NULL") +
                      ", _profile="     + (profile     != null ? profile.name     : "NULL") +
                      ", _rigidbody="   + (rigidbody   != null ? rigidbody.name   : "NULL") + ".");
        }

        /// <summary>
        /// Returns the first CarController in the scene that does NOT have an
        /// EnemyCarController component on the same GameObject — that is the player.
        /// </summary>
        private static CarController FindPlayerCar()
        {
            var allCars = Object.FindObjectsByType<CarController>(FindObjectsSortMode.None);
            foreach (var car in allCars)
            {
                if (car.GetComponent<EnemyCarController>() == null)
                    return car;
            }
            return null;
        }

        /// <summary>
        /// Fallback prefab lookup: finds the first Car_* prefab in the Vehicles folder.
        /// Mirrors the logic in BuildDrivingTestSceneEditor.FindFirstCarPrefab().
        /// </summary>
        private static GameObject FindFirstCarPrefab()
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { VehiclesPrefabsFolder });
            foreach (string guid in guids)
            {
                string path     = AssetDatabase.GUIDToAssetPath(guid);
                string fileName = System.IO.Path.GetFileNameWithoutExtension(path);
                if (fileName.StartsWith("Car_", System.StringComparison.OrdinalIgnoreCase))
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab != null)
                    {
                        Debug.Log("[Editor] Fallback prefab search: found " + path);
                        return prefab;
                    }
                }
            }
            return null;
        }

        private static void FinalizeScene()
        {
            var activeScene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
            Debug.Log("[Editor] Scene saved.");
        }
    }
}
#endif
