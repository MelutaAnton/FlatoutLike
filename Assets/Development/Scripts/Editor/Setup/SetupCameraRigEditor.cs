#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using FlatOutLike.Controllers.Camera;
using FlatOutLike.Data.Configs.Camera;
using FlatOutLike.Data.Configs.Driver;
using FlatOutLike.Installers;

namespace FlatOutLike.Editor.Setup
{
    /// <summary>
    /// Настраивает camera rig для Step 6 (eject + orbit-камера водителя) в открытой сцене.
    ///
    /// Шаги:
    ///   1. Находит GameObject с ChaseCarCameraBehaviour (обычно Main Camera).
    ///   2. Идемпотентно добавляет на него OrbitDriverCameraBehaviour и CameraRig.
    ///   3. Заполняет CameraRig._chase / _orbit и OrbitDriverCameraBehaviour._config.
    ///   4. Заполняет GameLifetimeScope._cameraRig, _driverEjectConfig, _orbitCameraConfig.
    ///
    /// Идемпотентность: проверяем наличие именно компонента, а не только GameObject —
    /// повторный запуск не дублирует компоненты, только обновляет ссылки.
    ///
    /// Запускать ПОСЛЕ:
    ///   FlatOutLike/Configs/Create Default DriverEjectConfig
    ///   FlatOutLike/Configs/Create Default OrbitCameraConfig
    ///   FlatOutLike/Setup/Build Driver Ragdoll Prefab
    ///   FlatOutLike/Setup/Attach Driver To Car Prefabs
    ///
    /// Запускать в сцене Derby_Arena_01.unity (или любой сцене с GameLifetimeScope
    /// и chase-камерой уже собранными предыдущими шагами Implementation Order).
    /// </summary>
    public static class SetupCameraRigEditor
    {
        private const string DriverEjectConfigPath  = "Assets/Development/Data/Configs/Driver/DriverEjectConfig_Default.asset";
        private const string OrbitCameraConfigPath  = "Assets/Development/Data/Configs/Camera/OrbitCameraConfig_Default.asset";

        [MenuItem("FlatOutLike/Setup/Setup Camera Rig (Step 6)")]
        public static void SetupCameraRig()
        {
            Undo.SetCurrentGroupName("Setup Camera Rig (Step 6)");
            int undoGroup = Undo.GetCurrentGroup();

            // ── 1. Ищем камеру с ChaseCarCameraBehaviour ───────────────────────
            var chase = Object.FindAnyObjectByType<ChaseCarCameraBehaviour>();
            if (chase == null)
            {
                Debug.LogWarning("[Editor] ChaseCarCameraBehaviour не найден в открытой сцене. " +
                                  "Собери арену/сцену с chase-камерой сначала.");
                return;
            }

            var cameraGo = chase.gameObject;
            Debug.Log("[Editor] Найдена chase-камера на '" + cameraGo.name + "'.");

            // ── 2. Идемпотентно добавляем OrbitDriverCameraBehaviour и CameraRig ─
            var orbit = cameraGo.GetComponent<OrbitDriverCameraBehaviour>();
            if (orbit == null)
            {
                orbit = Undo.AddComponent<OrbitDriverCameraBehaviour>(cameraGo);
                Debug.Log("[Editor] Добавлен OrbitDriverCameraBehaviour на '" + cameraGo.name + "'.");
            }
            else
            {
                Debug.Log("[Editor] OrbitDriverCameraBehaviour уже присутствует на '" + cameraGo.name + "'.");
            }

            var rig = cameraGo.GetComponent<CameraRig>();
            if (rig == null)
            {
                rig = Undo.AddComponent<CameraRig>(cameraGo);
                Debug.Log("[Editor] Добавлен CameraRig на '" + cameraGo.name + "'.");
            }
            else
            {
                Debug.Log("[Editor] CameraRig уже присутствует на '" + cameraGo.name + "'.");
            }

            // ── 3a. CameraRig._chase / _orbit ───────────────────────────────────
            var rigSo = new SerializedObject(rig);
            var chaseProp = rigSo.FindProperty("_chase");
            var orbitProp = rigSo.FindProperty("_orbit");
            if (chaseProp != null) chaseProp.objectReferenceValue = chase;
            if (orbitProp != null) orbitProp.objectReferenceValue = orbit;
            rigSo.ApplyModifiedProperties();
            Debug.Log("[Editor] CameraRig._chase / _orbit заполнены.");

            // ── 3b. OrbitDriverCameraBehaviour._config ──────────────────────────
            var orbitConfig = AssetDatabase.LoadAssetAtPath<OrbitCameraConfig>(OrbitCameraConfigPath);
            if (orbitConfig == null)
                Debug.LogWarning("[Editor] OrbitCameraConfig_Default не найден по " + OrbitCameraConfigPath +
                                  ". Запусти 'FlatOutLike/Configs/Create Default OrbitCameraConfig' сначала.");

            var orbitSo = new SerializedObject(orbit);
            var orbitConfigProp = orbitSo.FindProperty("_config");
            if (orbitConfigProp != null) orbitConfigProp.objectReferenceValue = orbitConfig;
            orbitSo.ApplyModifiedProperties();
            Debug.Log("[Editor] OrbitDriverCameraBehaviour._config = " +
                      (orbitConfig != null ? orbitConfig.name : "NULL") + ".");

            // ── 4. Заполняем GameLifetimeScope ──────────────────────────────────
            var scope = Object.FindAnyObjectByType<GameLifetimeScope>();
            if (scope == null)
            {
                Debug.LogWarning("[Editor] GameLifetimeScope не найден в сцене. Ссылки _cameraRig / " +
                                  "_driverEjectConfig / _orbitCameraConfig не заполнены.");
            }
            else
            {
                var ejectConfig = AssetDatabase.LoadAssetAtPath<DriverEjectConfig>(DriverEjectConfigPath);
                if (ejectConfig == null)
                    Debug.LogWarning("[Editor] DriverEjectConfig_Default не найден по " + DriverEjectConfigPath +
                                      ". Запусти 'FlatOutLike/Configs/Create Default DriverEjectConfig' сначала.");

                var scopeSo = new SerializedObject(scope);

                var cameraRigProp = scopeSo.FindProperty("_cameraRig");
                if (cameraRigProp != null)
                    cameraRigProp.objectReferenceValue = rig;
                else
                    Debug.LogWarning("[Editor] Поле GameLifetimeScope._cameraRig не найдено. " +
                                      "Возможно, рантайм Step 6 ещё не скомпилировался.");

                var ejectProp = scopeSo.FindProperty("_driverEjectConfig");
                if (ejectProp != null)
                    ejectProp.objectReferenceValue = ejectConfig;
                else
                    Debug.LogWarning("[Editor] Поле GameLifetimeScope._driverEjectConfig не найдено. " +
                                      "Возможно, рантайм Step 6 ещё не скомпилировался.");

                var orbitCfgProp = scopeSo.FindProperty("_orbitCameraConfig");
                if (orbitCfgProp != null)
                    orbitCfgProp.objectReferenceValue = orbitConfig;
                else
                    Debug.LogWarning("[Editor] Поле GameLifetimeScope._orbitCameraConfig не найдено. " +
                                      "Возможно, рантайм Step 6 ещё не скомпилировался.");

                scopeSo.ApplyModifiedProperties();
                Debug.Log("[Editor] GameLifetimeScope: _cameraRig / _driverEjectConfig / _orbitCameraConfig " +
                          "заполнены (там, где поля существуют).");
            }

            // ── 5. Сохраняем сцену ───────────────────────────────────────────────
            var activeScene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);

            Undo.CollapseUndoOperations(undoGroup);

            Debug.Log("[Editor] Setup Camera Rig (Step 6) завершён и сцена сохранена.");
        }
    }
}
#endif
