#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using FlatOutLike.Controllers.Hub;
using FlatOutLike.Models;
using Object = UnityEngine.Object;

namespace FlatOutLike.Editor.Setup
{
    /// <summary>
    /// Шаг 11a: собирает персонажа BaseHub.
    ///   1) AC_HubCharacter.controller — float-параметр <see cref="AnimatorHashes.HubSpeedParam"/>,
    ///      на base layer 1D BlendTree по нему: 0 → Idle, 0.5 → Walking, 1 → Runnig (клипы Arcade Idle Engine
    ///      ССЫЛКОЙ, не копируются). useAutomaticThresholds = false — иначе Unity молча
    ///      пересчитает пороги. Существующий контроллер пересобирается на месте (тот же GUID).
    ///   2) HubCharacter.prefab — корень с CharacterController + HubCharacterController и
    ///      вложенный prefab-instance аватара водителя БЕЗ ragdoll (Driver_Default не трогаем),
    ///      Animator аватара = AC_HubCharacter, applyRootMotion = false.
    ///
    /// Идемпотентно: повторный прогон обновляет контроллер и префаб на месте, GUID сохраняются.
    /// Запускать ПОСЛЕ компиляции runtime-класса HubCharacterController.
    /// </summary>
    public static class BuildHubCharacterPrefabEditor
    {
        public const string ControllerPath = "Assets/Development/Art/Animations/AC_HubCharacter.controller";
        public const string PrefabPath     = "Assets/Development/Prefabs/Characters/HubCharacter.prefab";

        private const string AvatarPath =
            "Assets/ThirdParty/DriverAvatars/npc_casual_set_00/Prefabs/npc_csl_00_character_01m_01.prefab";
        private const string IdleClipPath =
            "Assets/ThirdParty/Arcade Idle Engine/Animation/Idle.anim";
        private const string WalkClipPath =
            "Assets/ThirdParty/Arcade Idle Engine/Animation/Walking.anim";
        private const string RunClipPath =
            "Assets/ThirdParty/Arcade Idle Engine/Animation/Runnig.anim";

        private const string RootName = "HubCharacter";
        private const string BlendTreeName = "Locomotion";
        private const float IdleThreshold = 0f;
        // Промежуточный шаг — правка пользователя после плейтеста 11a (ходьба между idle и бегом).
        private const float WalkThreshold = 0.5f;
        private const float RunThreshold  = 1f;

        private const float CapsuleHeight = 1.8f;
        public const float CapsuleRadius = 0.35f;

        [MenuItem("FlatOutLike/Setup/Build Hub Character Prefab")]
        public static void BuildHubCharacterPrefab()
        {
            var controller = BuildAnimatorController();
            if (controller == null)
                return;

            BuildPrefab(controller);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Editor] Build Hub Character Prefab завершён: " + PrefabPath);
        }

        // ── AnimatorController ──────────────────────────────────────────────────

        private static AnimatorController BuildAnimatorController()
        {
            var idle = AssetDatabase.LoadAssetAtPath<AnimationClip>(IdleClipPath);
            var walk = AssetDatabase.LoadAssetAtPath<AnimationClip>(WalkClipPath);
            var run  = AssetDatabase.LoadAssetAtPath<AnimationClip>(RunClipPath);
            if (idle == null || walk == null || run == null)
            {
                Debug.LogError("[Editor] Клипы Arcade Idle Engine не найдены (" + IdleClipPath + " / " +
                               WalkClipPath + " / " + RunClipPath + ") — AC_HubCharacter не собран.");
                return null;
            }

            EnsureFolder("Assets/Development", "Art");
            EnsureFolder("Assets/Development/Art", "Animations");

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
                Debug.Log("[Editor] Создан AnimatorController: " + ControllerPath);
            }
            else
            {
                Debug.Log("[Editor] AC_HubCharacter.controller уже существует — пересобираем содержимое на месте.");
            }

            // Параметры: сносим всё и заводим единственный float Speed.
            var parameters = controller.parameters;
            for (int i = parameters.Length - 1; i >= 0; i--)
                controller.RemoveParameter(i);
            controller.AddParameter(AnimatorHashes.HubSpeedParam, AnimatorControllerParameterType.Float);

            // Base layer: чистим состояния и осиротевшие блендтри-сабассеты.
            var stateMachine = controller.layers[0].stateMachine;
            foreach (var childState in stateMachine.states)
            {
                var oldMotion = childState.state.motion;
                stateMachine.RemoveState(childState.state);
                if (oldMotion is BlendTree oldTree && AssetDatabase.IsSubAsset(oldTree))
                {
                    AssetDatabase.RemoveObjectFromAsset(oldTree);
                    Object.DestroyImmediate(oldTree, true);
                }
            }

            var tree = new BlendTree
            {
                name = BlendTreeName,
                blendType = BlendTreeType.Simple1D,
                blendParameter = AnimatorHashes.HubSpeedParam,
                // КРИТИЧНО: при true Unity перераспределит пороги равномерно и затрёт заданные.
                useAutomaticThresholds = false,
            };
            AssetDatabase.AddObjectToAsset(tree, controller);

            // Порядок детей = порядок порогов (1D-блендтри ожидает возрастание).
            tree.AddChild(idle, IdleThreshold);
            tree.AddChild(walk, WalkThreshold);
            tree.AddChild(run, RunThreshold);

            // Явная перезапись порогов — на случай, если AddChild что-то пересчитал.
            var children = tree.children;
            children[0].threshold = IdleThreshold;
            children[1].threshold = WalkThreshold;
            children[2].threshold = RunThreshold;
            tree.children = children;
            tree.minThreshold = IdleThreshold;
            tree.maxThreshold = RunThreshold;

            var state = stateMachine.AddState(BlendTreeName);
            state.motion = tree;
            stateMachine.defaultState = state;

            EditorUtility.SetDirty(tree);
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            Debug.Log("[Editor] AC_HubCharacter: параметр '" + AnimatorHashes.HubSpeedParam +
                      "', BlendTree " + IdleThreshold + " → " + idle.name + ", " + WalkThreshold + " → " + walk.name +
                      ", " + RunThreshold + " → " + run.name + ".");
            return controller;
        }

        // ── Префаб ──────────────────────────────────────────────────────────────

        private static void BuildPrefab(AnimatorController controller)
        {
            // Ассеты — свежими ссылками (managed-ссылка не удерживает нативный объект).
            var avatarPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AvatarPath);
            if (avatarPrefab == null)
            {
                Debug.LogError("[Editor] Префаб аватара не найден: " + AvatarPath + " — HubCharacter не собран.");
                return;
            }

            EnsureFolder("Assets/Development/Prefabs", "Characters");

            bool exists = File.Exists(Path.Combine(Directory.GetCurrentDirectory(), PrefabPath));
            GameObject root = exists
                ? PrefabUtility.LoadPrefabContents(PrefabPath)
                : new GameObject(RootName);

            try
            {
                ConfigureRoot(root, avatarPrefab, controller);

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log("[Editor] " + (exists ? "Обновлён" : "Создан") + " префаб: " + PrefabPath);
            }
            finally
            {
                if (exists)
                    PrefabUtility.UnloadPrefabContents(root);
                else
                    Object.DestroyImmediate(root);
            }
        }

        private static void ConfigureRoot(GameObject root, GameObject avatarPrefab, AnimatorController controller)
        {
            // ── CharacterController ──
            var cc = root.GetComponent<CharacterController>();
            if (cc == null)
            {
                cc = root.AddComponent<CharacterController>();
                Debug.Log("[Editor] Добавлен CharacterController на " + root.name + ".");
            }
            cc.height = CapsuleHeight;
            cc.radius = CapsuleRadius;
            cc.center = new Vector3(0f, CapsuleHeight * 0.5f, 0f);

            // ── HubCharacterController ──
            var hub = root.GetComponent<HubCharacterController>();
            if (hub == null)
            {
                hub = root.AddComponent<HubCharacterController>();
                Debug.Log("[Editor] Добавлен HubCharacterController на " + root.name + ".");
            }

            // ── Вложенный аватар (prefab-instance, не unpack) ──
            var avatarInstance = FindAvatarChild(root, avatarPrefab);
            if (avatarInstance == null)
            {
                avatarInstance = (GameObject)PrefabUtility.InstantiatePrefab(avatarPrefab);
                avatarInstance.transform.SetParent(root.transform, false);
                Debug.Log("[Editor] Вложен аватар '" + avatarPrefab.name + "' как prefab-instance.");
            }
            else
            {
                Debug.Log("[Editor] Аватар '" + avatarPrefab.name + "' уже вложен — переиспользуем.");
            }
            avatarInstance.transform.localPosition = Vector3.zero;
            avatarInstance.transform.localRotation = Quaternion.identity;

            // Персонаж двигается CharacterController'ом — физика на костях аватара не нужна.
            StripPhysics(avatarInstance);

            // ── Animator: существующий на аватаре, иначе новый на корне аватара ──
            var animator = avatarInstance.GetComponentInChildren<Animator>(true);
            if (animator == null)
            {
                animator = avatarInstance.AddComponent<Animator>();
                Debug.Log("[Editor] На аватаре не было Animator — добавлен на корень аватара.");
            }

            var animSo = new SerializedObject(animator);
            var controllerProp = animSo.FindProperty("m_Controller");
            if (controllerProp != null)
                controllerProp.objectReferenceValue = controller;
            else
                Debug.LogWarning("[Editor] Animator.m_Controller не найден.");

            var rootMotionProp = animSo.FindProperty("m_ApplyRootMotion");
            if (rootMotionProp != null)
                rootMotionProp.boolValue = false;
            animSo.ApplyModifiedProperties();
            Debug.Log("[Editor] Animator: controller = AC_HubCharacter, applyRootMotion = false.");

            if (animator.avatar == null)
                Debug.LogWarning("[Editor] У Animator аватара нет Avatar — humanoid-клипы Arcade Idle не сыграют.");

            // ── Прошивка полей контроллера (null не присваиваем) ──
            var hubSo = new SerializedObject(hub);
            SetRef(hubSo, "_characterController", cc);
            SetRef(hubSo, "_animator", animator);
            hubSo.ApplyModifiedProperties();
        }

        private static GameObject FindAvatarChild(GameObject root, GameObject avatarPrefab)
        {
            foreach (Transform child in root.transform)
            {
                if (!PrefabUtility.IsAnyPrefabInstanceRoot(child.gameObject))
                    continue;
                if (PrefabUtility.GetCorrespondingObjectFromSource(child.gameObject) == avatarPrefab)
                    return child.gameObject;
            }
            return null;
        }

        private static void StripPhysics(GameObject avatarInstance)
        {
            int removed = 0;

            foreach (var rb in avatarInstance.GetComponentsInChildren<Rigidbody>(true))
            {
                Object.DestroyImmediate(rb);
                removed++;
            }

            foreach (var col in avatarInstance.GetComponentsInChildren<Collider>(true))
            {
                Object.DestroyImmediate(col);
                removed++;
            }

            if (removed > 0)
                Debug.Log("[Editor] С аватара убрано физических компонентов: " + removed + ".");
        }

        private static void SetRef(SerializedObject so, string field, Object value)
        {
            var prop = so.FindProperty(field);
            if (prop == null)
            {
                Debug.LogWarning("[Editor] Поле " + so.targetObject.GetType().Name + "." + field +
                                 " не найдено (рантайм ещё не скомпилирован?).");
                return;
            }

            if (value == null)
            {
                Debug.LogWarning("[Editor] " + field + ": значение null — поле не тронуто.");
                return;
            }

            prop.objectReferenceValue = value;
        }

        private static void EnsureFolder(string parent, string folderName)
        {
            string full = parent + "/" + folderName;
            if (!AssetDatabase.IsValidFolder(full))
            {
                AssetDatabase.CreateFolder(parent, folderName);
                Debug.Log("[Editor] Создана папка: " + full);
            }
        }
    }
}
#endif
