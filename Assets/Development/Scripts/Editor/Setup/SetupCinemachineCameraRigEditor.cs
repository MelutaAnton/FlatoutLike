#if UNITY_EDITOR
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using FlatOutLike.Controllers.Camera;
using FlatOutLike.Data.Configs.Camera;
using FlatOutLike.Models;

namespace FlatOutLike.Editor.Setup
{
    /// <summary>
    /// Мигрирует camera rig на Cinemachine 3.x: CinemachineBrain на Main Camera,
    /// CM_ChaseCamera (CinemachineCamera + CinemachineFollow + CinemachineRotationComposer +
    /// CinemachineChaseCameraBehaviour) и CM_OrbitCamera (CinemachineCamera + CinemachineOrbitalFollow +
    /// CinemachineRotationComposer + CinemachineOrbitDriverCameraBehaviour), плюс перепрошивает
    /// CameraRig._chase / _orbit на новые Cinemachine-адаптеры.
    ///
    /// Заменяет собой старые ChaseCarCameraBehaviour / OrbitDriverCameraBehaviour — эти классы
    /// удалены рантайм-агентом; если на сцене остались их компоненты, они превратились в
    /// missing scripts и удаляются шагом RemoveMissingScriptsInScene().
    ///
    /// Методы Ensure* — public static и переиспользуются другими Setup-скриптами
    /// (BuildDerbyArenaEditor, BuildDrivingTestSceneEditor, BuildHighJumpSceneEditor,
    /// SetupCameraRigEditor), которым нужна только часть rig'а (например, только chase-камера).
    ///
    /// Идемпотентность: find-or-create по имени объекта + проверка компонента.
    ///
    /// Запускать в открытой сцене (Derby_Arena_01.unity, Stunts_HighJump.unity) ПОСЛЕ того,
    /// как сцена уже собрана (BuildDerbyArenaEditor / BuildHighJumpSceneEditor) и рантайм
    /// Cinemachine-адаптеров скомпилирован.
    /// </summary>
    public static class SetupCinemachineCameraRigEditor
    {
        private const string ChaseCameraObjectName = "CM_ChaseCamera";
        private const string OrbitCameraObjectName = "CM_OrbitCamera";

        private const string ChaseCameraConfigPath        = "Assets/Development/Data/Configs/Camera/ChaseCameraConfig_Default.asset";
        private const string OrbitCameraConfigDefaultPath = "Assets/Development/Data/Configs/Camera/OrbitCameraConfig_Default.asset";
        private const string OrbitCameraConfigHighJumpPath = "Assets/Development/Data/Configs/Camera/OrbitCameraConfig_HighJump.asset";

        private const int ChasePriority = 10;
        private const int OrbitPriority = 0;

        // ── Точка входа меню ────────────────────────────────────────────────

        [MenuItem("FlatOutLike/Setup/Setup Cinemachine Camera Rig")]
        public static void Setup()
        {
            Undo.SetCurrentGroupName("Setup Cinemachine Camera Rig");
            int undoGroup = Undo.GetCurrentGroup();

            // ── 1. CinemachineBrain на Main Camera ─────────────────────────
            EnsureBrain();

            // ── 2. Миграция: чистим missing scripts от старых поведений ───
            int removed = RemoveMissingScriptsInScene();
            if (removed > 0)
                Debug.Log("[Editor] Удалено " + removed + " missing-script компонент(ов) — " +
                          "миграция со старых ChaseCarCameraBehaviour/OrbitDriverCameraBehaviour.");
            else
                Debug.Log("[Editor] Missing-script компонентов не найдено — миграция не требуется.");

            // ── 3. CM_ChaseCamera ────────────────────────────────────────
            var chaseConfig = AssetDatabase.LoadAssetAtPath<ChaseCameraConfig>(ChaseCameraConfigPath);
            if (chaseConfig == null)
                Debug.LogWarning("[Editor] ChaseCameraConfig_Default не найден по " + ChaseCameraConfigPath +
                                  ". Запусти 'FlatOutLike/Configs/Create Default ChaseCameraConfig' сначала.");
            var chase = EnsureChaseCamera(chaseConfig, ChasePriority);

            // ── 4. CM_OrbitCamera ────────────────────────────────────────
            string orbitConfigPath = SceneManager.GetActiveScene().name == SceneKeys.StuntsHighJump
                ? OrbitCameraConfigHighJumpPath
                : OrbitCameraConfigDefaultPath;

            var orbitConfig = AssetDatabase.LoadAssetAtPath<OrbitCameraConfig>(orbitConfigPath);
            if (orbitConfig == null)
                Debug.LogWarning("[Editor] OrbitCameraConfig не найден по " + orbitConfigPath +
                                  ". Запусти 'FlatOutLike/Configs/Create Default OrbitCameraConfig' сначала.");
            var orbit = EnsureOrbitCamera(orbitConfig, OrbitPriority);

            // ── 5. Прошиваем CameraRig на Main Camera ───────────────────────
            var mainCam = Camera.main;
            if (mainCam == null)
            {
                Debug.LogWarning("[Editor] Main Camera не найдена после EnsureBrain() — CameraRig не прошит.");
            }
            else
            {
                EnsureCameraRig(mainCam.gameObject, chase, orbit);
            }

            // ── 6. Сохраняем сцену ───────────────────────────────────────────
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

            Undo.CollapseUndoOperations(undoGroup);
            Debug.Log("[Editor] Setup Cinemachine Camera Rig завершён.");
        }

        // ── Переиспользуемые шаги ────────────────────────────────────────────

        /// <summary>
        /// Идемпотентно гарантирует Main Camera + CinemachineBrain. Default Blend оставлен
        /// дефолтным стилем (EaseInOut), длительность выставлена в 1с через прямое поле
        /// (CinemachineBrain.DefaultBlend — публичное поле-структура, доступно напрямую).
        /// </summary>
        public static CinemachineBrain EnsureBrain()
        {
            var mainCam = Camera.main;
            GameObject camGo;

            if (mainCam == null)
            {
                camGo = GameObject.Find("Main Camera");
                if (camGo == null)
                {
                    camGo = new GameObject("Main Camera");
                    Undo.RegisterCreatedObjectUndo(camGo, "Create Main Camera");
                    Debug.Log("[Editor] Создан Main Camera GameObject.");
                }

                // Тег проставляем и найденному объекту: Camera.main ищет по тегу, и без него
                // последующие вызовы Camera.main (прошивка CameraRig ниже) вернут null.
                if (!camGo.CompareTag("MainCamera"))
                    camGo.tag = "MainCamera";

                if (camGo.GetComponent<Camera>() == null)
                    Undo.AddComponent<Camera>(camGo);
                if (camGo.GetComponent<AudioListener>() == null)
                    Undo.AddComponent<AudioListener>(camGo);
            }
            else
            {
                camGo = mainCam.gameObject;
            }

            var brain = camGo.GetComponent<CinemachineBrain>();
            if (brain == null)
            {
                brain = Undo.AddComponent<CinemachineBrain>(camGo);
                Debug.Log("[Editor] Добавлен CinemachineBrain на '" + camGo.name + "'.");
            }
            else
            {
                Debug.Log("[Editor] CinemachineBrain уже присутствует на '" + camGo.name + "'.");
            }

            // DefaultBlend — публичное поле-структура (CinemachineBlendDefinition), меняем
            // напрямую: Style оставляем дефолтным (EaseInOut), Time выставляем в 1с.
            Undo.RecordObject(brain, "Set CinemachineBrain Default Blend");
            brain.DefaultBlend.Time = 1f;
            EditorUtility.SetDirty(brain);
            Debug.Log("[Editor] CinemachineBrain.DefaultBlend.Time = 1s (стиль по умолчанию не менялся).");

            return brain;
        }

        /// <summary>
        /// Сносит компоненты с отсутствующим скриптом по всей активной сцене — после удаления
        /// классов ChaseCarCameraBehaviour/OrbitDriverCameraBehaviour оставшиеся на объектах
        /// компоненты превращаются в missing scripts.
        ///
        /// Примечание: GameObjectUtility.RemoveMonoBehavioursWithMissingScript не поддерживает
        /// Undo (у missing-script компонента нет осмысленного состояния для отката) — шаг
        /// сознательно не оборачивается в Undo.RegisterCreatedObjectUndo/RecordObject.
        /// </summary>
        public static int RemoveMissingScriptsInScene()
        {
            int total = 0;
            var scene = SceneManager.GetActiveScene();
            foreach (var root in scene.GetRootGameObjects())
                total += RemoveMissingScriptsRecursive(root);
            return total;
        }

        private static int RemoveMissingScriptsRecursive(GameObject go)
        {
            int count = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
            foreach (Transform child in go.transform)
                count += RemoveMissingScriptsRecursive(child.gameObject);
            return count;
        }

        /// <summary>
        /// Идемпотентно гарантирует CM_ChaseCamera: CinemachineCamera + CinemachineFollow +
        /// CinemachineRotationComposer + CinemachineChaseCameraBehaviour. Заполняет
        /// behaviour._camera / _config. Target (Follow/LookAt) НЕ выставляется здесь —
        /// в сценах с CameraRig/ICameraService он приходит в рантайме через SetTarget
        /// (тот же паттерн, что был у старой ChaseCarCameraBehaviour).
        /// </summary>
        public static CinemachineChaseCameraBehaviour EnsureChaseCamera(ChaseCameraConfig config, int priority)
        {
            var go = GameObject.Find(ChaseCameraObjectName);
            if (go == null)
            {
                go = new GameObject(ChaseCameraObjectName);
                Undo.RegisterCreatedObjectUndo(go, "Create " + ChaseCameraObjectName);
                Debug.Log("[Editor] Создан " + ChaseCameraObjectName + ".");
            }
            else
            {
                Debug.Log("[Editor] " + ChaseCameraObjectName + " уже существует, переиспользуем.");
            }

            var cmCam = go.GetComponent<CinemachineCamera>();
            if (cmCam == null)
                cmCam = Undo.AddComponent<CinemachineCamera>(go);

            if (go.GetComponent<CinemachineFollow>() == null)
                Undo.AddComponent<CinemachineFollow>(go);

            if (go.GetComponent<CinemachineRotationComposer>() == null)
                Undo.AddComponent<CinemachineRotationComposer>(go);

            var behaviour = go.GetComponent<CinemachineChaseCameraBehaviour>();
            if (behaviour == null)
            {
                behaviour = Undo.AddComponent<CinemachineChaseCameraBehaviour>(go);
                Debug.Log("[Editor] Добавлен CinemachineChaseCameraBehaviour на " + ChaseCameraObjectName + ".");
            }

            cmCam.Priority = priority;

            var so = new SerializedObject(behaviour);
            var camProp = so.FindProperty("_camera");
            if (camProp != null)
                camProp.objectReferenceValue = cmCam;
            else
                Debug.LogWarning("[Editor] Поле CinemachineChaseCameraBehaviour._camera не найдено. " +
                                  "Возможно, рантайм-адаптер ещё не скомпилировался.");

            var cfgProp = so.FindProperty("_config");
            if (cfgProp != null)
                cfgProp.objectReferenceValue = config;
            else
                Debug.LogWarning("[Editor] Поле CinemachineChaseCameraBehaviour._config не найдено. " +
                                  "Возможно, рантайм-адаптер ещё не скомпилировался.");

            so.ApplyModifiedProperties();

            Debug.Log("[Editor] " + ChaseCameraObjectName + " настроен (Priority=" + priority + ").");
            return behaviour;
        }

        /// <summary>
        /// Идемпотентно гарантирует CM_OrbitCamera: CinemachineCamera + CinemachineOrbitalFollow +
        /// CinemachineRotationComposer + CinemachineOrbitDriverCameraBehaviour. Заполняет
        /// behaviour._camera / _config.
        /// </summary>
        public static CinemachineOrbitDriverCameraBehaviour EnsureOrbitCamera(OrbitCameraConfig config, int priority)
        {
            var go = GameObject.Find(OrbitCameraObjectName);
            if (go == null)
            {
                go = new GameObject(OrbitCameraObjectName);
                Undo.RegisterCreatedObjectUndo(go, "Create " + OrbitCameraObjectName);
                Debug.Log("[Editor] Создан " + OrbitCameraObjectName + ".");
            }
            else
            {
                Debug.Log("[Editor] " + OrbitCameraObjectName + " уже существует, переиспользуем.");
            }

            var cmCam = go.GetComponent<CinemachineCamera>();
            if (cmCam == null)
                cmCam = Undo.AddComponent<CinemachineCamera>(go);

            if (go.GetComponent<CinemachineOrbitalFollow>() == null)
                Undo.AddComponent<CinemachineOrbitalFollow>(go);

            if (go.GetComponent<CinemachineRotationComposer>() == null)
                Undo.AddComponent<CinemachineRotationComposer>(go);

            var behaviour = go.GetComponent<CinemachineOrbitDriverCameraBehaviour>();
            if (behaviour == null)
            {
                behaviour = Undo.AddComponent<CinemachineOrbitDriverCameraBehaviour>(go);
                Debug.Log("[Editor] Добавлен CinemachineOrbitDriverCameraBehaviour на " + OrbitCameraObjectName + ".");
            }

            cmCam.Priority = priority;

            var so = new SerializedObject(behaviour);
            var camProp = so.FindProperty("_camera");
            if (camProp != null)
                camProp.objectReferenceValue = cmCam;
            else
                Debug.LogWarning("[Editor] Поле CinemachineOrbitDriverCameraBehaviour._camera не найдено. " +
                                  "Возможно, рантайм-адаптер ещё не скомпилировался.");

            var cfgProp = so.FindProperty("_config");
            if (cfgProp != null)
                cfgProp.objectReferenceValue = config;
            else
                Debug.LogWarning("[Editor] Поле CinemachineOrbitDriverCameraBehaviour._config не найдено. " +
                                  "Возможно, рантайм-адаптер ещё не скомпилировался.");

            so.ApplyModifiedProperties();

            Debug.Log("[Editor] " + OrbitCameraObjectName + " настроен (Priority=" + priority + ").");
            return behaviour;
        }

        /// <summary>
        /// Идемпотентно гарантирует CameraRig на <paramref name="hostGo"/> (обычно Main Camera)
        /// и прошивает _chase / _orbit на переданные Cinemachine-адаптеры. Имена полей те же,
        /// что были у старых ChaseCarCameraBehaviour/OrbitDriverCameraBehaviour — меняются
        /// только типы значений.
        /// </summary>
        public static CameraRig EnsureCameraRig(GameObject hostGo, CinemachineChaseCameraBehaviour chase, CinemachineOrbitDriverCameraBehaviour orbit)
        {
            var rig = hostGo.GetComponent<CameraRig>();
            if (rig == null)
            {
                rig = Undo.AddComponent<CameraRig>(hostGo);
                Debug.Log("[Editor] Добавлен CameraRig на '" + hostGo.name + "'.");
            }

            var so = new SerializedObject(rig);

            var chaseProp = so.FindProperty("_chase");
            if (chaseProp != null)
                chaseProp.objectReferenceValue = chase;
            else
                Debug.LogWarning("[Editor] Поле CameraRig._chase не найдено или несовместимого типа. " +
                                  "Проверь, что рантайм CameraRig обновлён на новые Cinemachine-типы (та же " +
                                  "сигнатура поля, но CinemachineChaseCameraBehaviour вместо ChaseCarCameraBehaviour).");

            var orbitProp = so.FindProperty("_orbit");
            if (orbitProp != null)
                orbitProp.objectReferenceValue = orbit;
            else
                Debug.LogWarning("[Editor] Поле CameraRig._orbit не найдено или несовместимого типа. " +
                                  "Проверь, что рантайм CameraRig обновлён на новые Cinemachine-типы (та же " +
                                  "сигнатура поля, но CinemachineOrbitDriverCameraBehaviour вместо OrbitDriverCameraBehaviour).");

            so.ApplyModifiedProperties();

            Debug.Log("[Editor] CameraRig._chase / _orbit заполнены новыми Cinemachine-адаптерами.");
            return rig;
        }
    }
}
#endif
