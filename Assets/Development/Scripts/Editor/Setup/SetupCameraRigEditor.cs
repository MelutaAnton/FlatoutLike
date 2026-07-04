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
    /// С миграцией на Cinemachine 3.x сборка chase+orbit+CameraRig делегирована
    /// <see cref="SetupCinemachineCameraRigEditor.Setup"/> (замена ChaseCarCameraBehaviour/
    /// OrbitDriverCameraBehaviour на CinemachineChaseCameraBehaviour/CinemachineOrbitDriverCameraBehaviour).
    /// Этот скрипт лишь довызывает её и дозаполняет GameLifetimeScope-специфичные поля Step 6:
    ///   _cameraRig, _driverEjectConfig, _orbitCameraConfig, _cameraShakeConfig, _cameraShaker.
    /// CinemachineImpulseSource/Listener/CinemachineCameraShaker создаются внутри
    /// SetupCinemachineCameraRigEditor.Setup() (шаг EnsureImpulse) — здесь только находим
    /// уже созданный CinemachineCameraShaker и грузим CameraShakeConfig_Default.asset.
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
        private const string DriverEjectConfigDefaultPath  = "Assets/Development/Data/Configs/Driver/DriverEjectConfig_Default.asset";
        private const string DriverEjectConfigHighJumpPath = "Assets/Development/Data/Configs/Driver/DriverEjectConfig_HighJump.asset";
        private const string OrbitCameraConfigDefaultPath  = "Assets/Development/Data/Configs/Camera/OrbitCameraConfig_Default.asset";
        private const string OrbitCameraConfigHighJumpPath = "Assets/Development/Data/Configs/Camera/OrbitCameraConfig_HighJump.asset";
        private const string CameraShakeConfigPath   = "Assets/Development/Data/Configs/Camera/CameraShakeConfig_Default.asset";

        // Per-scene конфиги (правило проекта): HighJump живёт на своих ассетах eject/orbit.
        // Жёсткая прошивка _Default затирала HighJump-настройки при повторном прогоне
        // в Stunts_HighJump (регрессия «водитель подлетает на 2 м вместо 60»).
        private static bool IsHighJumpScene =>
            SceneManager.GetActiveScene().name == FlatOutLike.Models.SceneKeys.StuntsHighJump;

        [MenuItem("FlatOutLike/Setup/Setup Camera Rig (Step 6)")]
        public static void SetupCameraRig()
        {
            Undo.SetCurrentGroupName("Setup Camera Rig (Step 6)");
            int undoGroup = Undo.GetCurrentGroup();

            // ── 1-3. Chase + Orbit + CameraRig (Cinemachine) ────────────────────
            SetupCinemachineCameraRigEditor.Setup();

            var rig = Object.FindAnyObjectByType<CameraRig>();
            if (rig == null)
            {
                Debug.LogWarning("[Editor] CameraRig не найден после Setup Cinemachine Camera Rig. " +
                                  "GameLifetimeScope не заполнен.");
                return;
            }

            // ── 4. Заполняем GameLifetimeScope ──────────────────────────────────
            var scope = Object.FindAnyObjectByType<GameLifetimeScope>();
            if (scope == null)
            {
                Debug.LogWarning("[Editor] GameLifetimeScope не найден в сцене. Ссылки _cameraRig / " +
                                  "_driverEjectConfig / _orbitCameraConfig не заполнены.");
            }
            else
            {
                string ejectConfigPath = IsHighJumpScene ? DriverEjectConfigHighJumpPath : DriverEjectConfigDefaultPath;
                var ejectConfig = AssetDatabase.LoadAssetAtPath<DriverEjectConfig>(ejectConfigPath);
                if (ejectConfig == null)
                    Debug.LogWarning("[Editor] DriverEjectConfig не найден по " + ejectConfigPath +
                                      ". Запусти 'FlatOutLike/Configs/Create Default DriverEjectConfig' сначала.");

                string orbitConfigPath = IsHighJumpScene ? OrbitCameraConfigHighJumpPath : OrbitCameraConfigDefaultPath;
                var orbitConfig = AssetDatabase.LoadAssetAtPath<OrbitCameraConfig>(orbitConfigPath);
                if (orbitConfig == null)
                    Debug.LogWarning("[Editor] OrbitCameraConfig не найден по " + orbitConfigPath +
                                      ". Запусти 'FlatOutLike/Configs/Create Default OrbitCameraConfig' сначала.");

                var shakeConfig = AssetDatabase.LoadAssetAtPath<CameraShakeConfig>(CameraShakeConfigPath);
                if (shakeConfig == null)
                    Debug.LogWarning("[Editor] CameraShakeConfig_Default не найден по " + CameraShakeConfigPath +
                                      ". Запусти 'FlatOutLike/Configs/Create Default CameraShakeConfig' сначала.");

                var shaker = Object.FindAnyObjectByType<CinemachineCameraShaker>();
                if (shaker == null)
                    Debug.LogWarning("[Editor] CinemachineCameraShaker не найден в сцене. " +
                                      "Ожидается после SetupCinemachineCameraRigEditor.EnsureImpulse().");

                var scopeSo = new SerializedObject(scope);

                var cameraRigProp = scopeSo.FindProperty("_cameraRig");
                if (cameraRigProp != null)
                    cameraRigProp.objectReferenceValue = rig;
                else
                    Debug.LogWarning("[Editor] Поле GameLifetimeScope._cameraRig не найдено. " +
                                      "Возможно, рантайм Step 6 ещё не скомпилировался.");

                // null НЕ прошиваем: если ассет не найден, оставляем существующую ссылку —
                // иначе повторный прогон затирал бы рабочую проводку сцены пустотой.
                var ejectProp = scopeSo.FindProperty("_driverEjectConfig");
                if (ejectProp != null)
                {
                    if (ejectConfig != null)
                        ejectProp.objectReferenceValue = ejectConfig;
                }
                else
                    Debug.LogWarning("[Editor] Поле GameLifetimeScope._driverEjectConfig не найдено. " +
                                      "Возможно, рантайм Step 6 ещё не скомпилировался.");

                var orbitCfgProp = scopeSo.FindProperty("_orbitCameraConfig");
                if (orbitCfgProp != null)
                {
                    if (orbitConfig != null)
                        orbitCfgProp.objectReferenceValue = orbitConfig;
                }
                else
                    Debug.LogWarning("[Editor] Поле GameLifetimeScope._orbitCameraConfig не найдено. " +
                                      "Возможно, рантайм Step 6 ещё не скомпилировался.");

                var shakeConfigProp = scopeSo.FindProperty("_cameraShakeConfig");
                if (shakeConfigProp != null)
                    shakeConfigProp.objectReferenceValue = shakeConfig;
                else
                    Debug.LogWarning("[Editor] Поле GameLifetimeScope._cameraShakeConfig не найдено. " +
                                      "Возможно, рантайм тряски камеры ещё не скомпилировался.");

                var shakerProp = scopeSo.FindProperty("_cameraShaker");
                if (shakerProp != null)
                    shakerProp.objectReferenceValue = shaker;
                else
                    Debug.LogWarning("[Editor] Поле GameLifetimeScope._cameraShaker не найдено. " +
                                      "Возможно, рантайм тряски камеры ещё не скомпилировался.");

                scopeSo.ApplyModifiedProperties();
                Debug.Log("[Editor] GameLifetimeScope: _cameraRig / _driverEjectConfig / _orbitCameraConfig / " +
                          "_cameraShakeConfig / _cameraShaker заполнены (там, где поля существуют).");
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
