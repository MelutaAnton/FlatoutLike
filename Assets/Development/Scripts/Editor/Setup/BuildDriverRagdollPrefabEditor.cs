#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using FlatOutLike.Controllers.Driver;

namespace FlatOutLike.Editor.Setup
{
    /// <summary>
    /// Собирает Assets/Development/Prefabs/Characters/Driver_Default.prefab —
    /// ragdoll-водителя поверх гуманоид-аватара из Assets/ThirdParty/DriverAvatars/.
    ///
    /// Шаги:
    ///   a) Переносит SitDriver.anim из Assets/Content/Animations в
    ///      Assets/Development/Art/Animations (идемпотентно, по AssetDatabase.MoveAsset).
    ///   b) Создаёт AC_DriverSit.controller с единственным default-state "Sit" на клипе SitDriver.
    ///   c) Собирает Driver_Default.prefab: корень + вложенный prefab-instance аватара
    ///      (связь по GUID, контент ThirdParty не копируется), Animator с AC_DriverSit,
    ///      ragdoll-кости (Rigidbody+Collider+CharacterJoint) через HumanBodyBones
    ///      (fallback — поиск по именам), DriverController с заполненными ссылками.
    ///
    /// Источник аватара: выделенный в Project префаб (путь должен начинаться с
    /// Assets/ThirdParty/DriverAvatars/), либо дефолт
    /// Assets/ThirdParty/DriverAvatars/npc_casual_set_00/Prefabs/npc_csl_00_character_01m_01.prefab.
    ///
    /// Идемпотентность: если Driver_Default.prefab уже существует — пересборка
    /// только по подтверждению DisplayDialog.
    /// </summary>
    public static class BuildDriverRagdollPrefabEditor
    {
        private const string SourceClipPath      = "Assets/Content/Animations/SitDriver.anim";
        private const string DestClipPath         = "Assets/Development/Art/Animations/SitDriver.anim";
        private const string ControllerPath       = "Assets/Development/Art/Animations/AC_DriverSit.controller";
        private const string DriverPrefabPath     = "Assets/Development/Prefabs/Characters/Driver_Default.prefab";
        private const string DefaultAvatarPath    = "Assets/ThirdParty/DriverAvatars/npc_casual_set_00/Prefabs/npc_csl_00_character_01m_01.prefab";
        private const string ThirdPartyDriverRoot = "Assets/ThirdParty/DriverAvatars/";

        private const float TotalMassKg = 70f;

        [MenuItem("FlatOutLike/Setup/Build Driver Ragdoll Prefab")]
        public static void BuildDriverRagdollPrefab()
        {
            Undo.SetCurrentGroupName("Build Driver Ragdoll Prefab");
            int undoGroup = Undo.GetCurrentGroup();

            // ── 0. Идемпотентность: подтверждение пересборки ──────────────────
            bool alreadyExists = AssetDatabase.LoadAssetAtPath<GameObject>(DriverPrefabPath) != null;
            if (alreadyExists)
            {
                if (!EditorUtility.DisplayDialog("Build Driver Ragdoll Prefab",
                    "Driver_Default.prefab уже существует. Пересобрать?", "Да", "Отмена"))
                {
                    Debug.Log("[Editor] Build Driver Ragdoll Prefab отменён пользователем.");
                    return;
                }
            }

            // ── a) Переносим клип анимации ─────────────────────────────────────
            var clip = MoveOrLoadClip();

            // ── b) Создаём/обновляем AnimatorController ────────────────────────
            var controller = BuildAnimatorController(clip);

            // ── c) Определяем префаб-источник аватара ──────────────────────────
            var avatarPrefab = ResolveAvatarSource();
            if (avatarPrefab == null)
            {
                Debug.LogError("[Editor] Не удалось определить префаб-аватар. " +
                                "Выдели префаб под " + ThirdPartyDriverRoot + " в окне Project и запусти снова.");
                return;
            }

            // ── c) Собираем Driver_Default.prefab ───────────────────────────────
            EnsureFolder("Assets/Development/Prefabs", "Characters");
            var driverPrefab = BuildDriverPrefab(avatarPrefab, controller);

            Undo.CollapseUndoOperations(undoGroup);

            if (driverPrefab != null)
            {
                Selection.activeObject = driverPrefab;
                EditorGUIUtility.PingObject(driverPrefab);
            }

            Debug.Log("[Editor] Build Driver Ragdoll Prefab завершён: " + DriverPrefabPath);
        }

        // ─────────────────────────────────────────────────────────────────────
        // a) Клип анимации
        // ─────────────────────────────────────────────────────────────────────
        private static AnimationClip MoveOrLoadClip()
        {
            var existingAtDest = AssetDatabase.LoadAssetAtPath<AnimationClip>(DestClipPath);
            if (existingAtDest != null)
            {
                Debug.Log("[Editor] SitDriver.anim уже перенесён: " + DestClipPath);
                return existingAtDest;
            }

            var sourceClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(SourceClipPath);
            if (sourceClip == null)
            {
                Debug.LogWarning("[Editor] SitDriver.anim не найден ни по " + SourceClipPath +
                                  ", ни по " + DestClipPath + ". AnimatorController будет создан без клипа.");
                return null;
            }

            EnsureFolder("Assets/Development", "Art");
            EnsureFolder("Assets/Development/Art", "Animations");

            string error = AssetDatabase.MoveAsset(SourceClipPath, DestClipPath);
            if (!string.IsNullOrEmpty(error))
            {
                Debug.LogError("[Editor] Не удалось перенести SitDriver.anim: " + error);
                return AssetDatabase.LoadAssetAtPath<AnimationClip>(SourceClipPath);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[Editor] SitDriver.anim перенесён: " + SourceClipPath + " → " + DestClipPath);
            return AssetDatabase.LoadAssetAtPath<AnimationClip>(DestClipPath);
        }

        // ─────────────────────────────────────────────────────────────────────
        // b) AnimatorController
        // ─────────────────────────────────────────────────────────────────────
        private static AnimatorController BuildAnimatorController(AnimationClip clip)
        {
            var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (existing != null)
            {
                Debug.Log("[Editor] AC_DriverSit.controller уже существует: " + ControllerPath);
                return existing;
            }

            EnsureFolder("Assets/Development", "Art");
            EnsureFolder("Assets/Development/Art", "Animations");

            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            var rootStateMachine = controller.layers[0].stateMachine;
            var sitState = rootStateMachine.AddState("Sit");
            if (clip != null)
                sitState.motion = clip;
            rootStateMachine.defaultState = sitState;

            AssetDatabase.SaveAssets();
            Undo.RegisterCreatedObjectUndo(controller, "Create AC_DriverSit");

            Debug.Log("[Editor] Создан AC_DriverSit.controller с default-state 'Sit'" +
                      (clip != null ? " (клип " + clip.name + ")." : " (клип не назначен)."));
            return controller;
        }

        // ─────────────────────────────────────────────────────────────────────
        // c) Определение аватара
        // ─────────────────────────────────────────────────────────────────────
        private static GameObject ResolveAvatarSource()
        {
            var selected = Selection.activeObject as GameObject;
            if (selected != null)
            {
                string path = AssetDatabase.GetAssetPath(selected);
                if (!string.IsNullOrEmpty(path) &&
                    path.StartsWith(ThirdPartyDriverRoot, System.StringComparison.OrdinalIgnoreCase))
                {
                    Debug.Log("[Editor] Используем выделенный аватар: " + path);
                    return selected;
                }
                Debug.LogWarning("[Editor] Выделенный объект не под " + ThirdPartyDriverRoot +
                                  " — используем дефолтный аватар.");
            }

            var fallback = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultAvatarPath);
            if (fallback != null)
                Debug.Log("[Editor] Используем дефолтный аватар: " + DefaultAvatarPath);
            return fallback;
        }

        // ─────────────────────────────────────────────────────────────────────
        // c) Сборка Driver_Default.prefab
        // ─────────────────────────────────────────────────────────────────────
        private static GameObject BuildDriverPrefab(GameObject avatarPrefab, AnimatorController controller)
        {
            var root = new GameObject("Driver_Default");
            Undo.RegisterCreatedObjectUndo(root, "Create Driver_Default Root");

            // Вложенный prefab-instance аватара — связь по GUID, ThirdParty-контент не копируется.
            var avatarInstance = (GameObject)PrefabUtility.InstantiatePrefab(avatarPrefab);
            Undo.RegisterCreatedObjectUndo(avatarInstance, "Instantiate Driver Avatar");
            avatarInstance.transform.SetParent(root.transform, false);
            Debug.Log("[Editor] Вложен аватар '" + avatarPrefab.name + "' как prefab-instance.");

            // ── Animator: берём существующий с аватара, иначе добавляем на корень ──
            var animator = avatarInstance.GetComponent<Animator>();
            if (animator == null)
                animator = root.GetComponent<Animator>();
            if (animator == null)
                animator = Undo.AddComponent<Animator>(root);

            var so = new SerializedObject(animator);
            var controllerProp = so.FindProperty("m_Controller");
            if (controllerProp != null)
                controllerProp.objectReferenceValue = controller;
            so.ApplyModifiedProperties();
            Debug.Log("[Editor] Animator._controller = AC_DriverSit.");

            // ── Ragdoll: кости через HumanBodyBones, fallback — поиск по именам ──
            var bones = ResolveRagdollBones(animator, avatarInstance.transform);
            var boneRigidbodies = BuildRagdoll(bones);

            // ── DriverController на корне ───────────────────────────────────────
            var driverController = root.GetComponent<DriverController>();
            if (driverController == null)
                driverController = Undo.AddComponent<DriverController>(root);

            var dcSo = new SerializedObject(driverController);
            var animProp = dcSo.FindProperty("_animator");
            if (animProp != null) animProp.objectReferenceValue = animator;

            var bonesProp = dcSo.FindProperty("_boneRigidbodies");
            if (bonesProp != null)
            {
                bonesProp.arraySize = boneRigidbodies.Count;
                for (int i = 0; i < boneRigidbodies.Count; i++)
                    bonesProp.GetArrayElementAtIndex(i).objectReferenceValue = boneRigidbodies[i];
            }

            var pelvisProp = dcSo.FindProperty("_pelvis");
            if (pelvisProp != null && bones.TryGetValue(HumanBodyBones.Hips, out var hips) && hips != null)
                pelvisProp.objectReferenceValue = hips.GetComponent<Rigidbody>();

            dcSo.ApplyModifiedProperties();
            Debug.Log("[Editor] DriverController: _animator, _boneRigidbodies[" + boneRigidbodies.Count +
                      "], _pelvis заполнены.");

            // ── DriverFlightController (Шаг 7): управление полётом после eject ──
            if (root.GetComponent<DriverFlightController>() == null)
            {
                Undo.AddComponent<DriverFlightController>(root);
                Debug.Log("[Editor] DriverFlightController добавлен на корень водителя.");
            }

            // ── Сохраняем как prefab-ассет ──────────────────────────────────────
            var prefabAsset = PrefabUtility.SaveAsPrefabAssetAndConnect(
                root, DriverPrefabPath, InteractionMode.AutomatedAction);
            Object.DestroyImmediate(root);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return prefabAsset;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Ragdoll: поиск костей
        // ─────────────────────────────────────────────────────────────────────
        private static readonly HumanBodyBones[] RequiredBones =
        {
            HumanBodyBones.Hips,
            HumanBodyBones.Spine,
            HumanBodyBones.Head,
            HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm,
            HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm,
            HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg,
            HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg,
        };

        private static readonly Dictionary<HumanBodyBones, string[]> NameFallbacks = new Dictionary<HumanBodyBones, string[]>
        {
            { HumanBodyBones.Hips,           new[] { "hips", "pelvis" } },
            { HumanBodyBones.Spine,          new[] { "spine" } },
            { HumanBodyBones.Head,           new[] { "head" } },
            { HumanBodyBones.LeftUpperArm,   new[] { "upperarm_l", "arm_l_up", "leftupperarm", "upper_arm_l" } },
            { HumanBodyBones.LeftLowerArm,   new[] { "lowerarm_l", "forearm_l", "leftlowerarm", "lower_arm_l" } },
            { HumanBodyBones.RightUpperArm,  new[] { "upperarm_r", "arm_r_up", "rightupperarm", "upper_arm_r" } },
            { HumanBodyBones.RightLowerArm,  new[] { "lowerarm_r", "forearm_r", "rightlowerarm", "lower_arm_r" } },
            { HumanBodyBones.LeftUpperLeg,   new[] { "upperleg_l", "thigh_l", "leftupperleg", "upper_leg_l" } },
            { HumanBodyBones.LeftLowerLeg,   new[] { "lowerleg_l", "calf_l", "leftlowerleg", "shin_l" } },
            { HumanBodyBones.RightUpperLeg,  new[] { "upperleg_r", "thigh_r", "rightupperleg", "upper_leg_r" } },
            { HumanBodyBones.RightLowerLeg,  new[] { "lowerleg_r", "calf_r", "rightlowerleg", "shin_r" } },
        };

        private static Dictionary<HumanBodyBones, Transform> ResolveRagdollBones(Animator animator, Transform avatarRoot)
        {
            var result = new Dictionary<HumanBodyBones, Transform>();
            var used = new HashSet<Transform>();
            bool isHumanoid = animator != null && animator.isHuman;

            foreach (var bone in RequiredBones)
            {
                Transform t = null;
                if (isHumanoid)
                    t = animator.GetBoneTransform(bone);

                if (t == null && NameFallbacks.TryGetValue(bone, out var keywords))
                    t = FindBoneFallback(avatarRoot, keywords, used);

                if (t == null)
                {
                    Debug.LogWarning("[Editor] Кость " + bone + " не найдена (ни через HumanBodyBones, " +
                                      "ни по имени). Ragdoll-часть для неё не будет создана.");
                }
                else
                {
                    used.Add(t);
                }
                result[bone] = t;
            }
            return result;
        }

        private static Transform FindBoneFallback(Transform root, string[] keywords, HashSet<Transform> used)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (used.Contains(t)) continue;
                string n = t.name.ToLowerInvariant();
                foreach (var kw in keywords)
                    if (n.Contains(kw)) return t;
            }
            return null;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Ragdoll: построение Rigidbody/Collider/CharacterJoint
        // ─────────────────────────────────────────────────────────────────────
        private struct BoneSetup
        {
            public HumanBodyBones Bone;
            public HumanBodyBones? Parent; // null == корень ragdoll'а (Hips), joint не создаётся
            public HumanBodyBones? SizeRef; // кость, до которой меряем размер коллайдера
            public float MassKg;
            public ColliderShape Shape;
        }

        private enum ColliderShape { Box, Sphere, Capsule }

        private static readonly BoneSetup[] Setup =
        {
            new BoneSetup { Bone = HumanBodyBones.Hips,          Parent = null,                              SizeRef = HumanBodyBones.Spine,         MassKg = 12f, Shape = ColliderShape.Box },
            new BoneSetup { Bone = HumanBodyBones.Spine,         Parent = HumanBodyBones.Hips,               SizeRef = HumanBodyBones.Head,          MassKg = 15f, Shape = ColliderShape.Box },
            new BoneSetup { Bone = HumanBodyBones.Head,          Parent = HumanBodyBones.Spine,              SizeRef = null,                          MassKg = 5f,  Shape = ColliderShape.Sphere },
            new BoneSetup { Bone = HumanBodyBones.LeftUpperArm,  Parent = HumanBodyBones.Spine,              SizeRef = HumanBodyBones.LeftLowerArm,  MassKg = 3f,  Shape = ColliderShape.Capsule },
            new BoneSetup { Bone = HumanBodyBones.LeftLowerArm,  Parent = HumanBodyBones.LeftUpperArm,       SizeRef = null,                          MassKg = 2f,  Shape = ColliderShape.Capsule },
            new BoneSetup { Bone = HumanBodyBones.RightUpperArm, Parent = HumanBodyBones.Spine,              SizeRef = HumanBodyBones.RightLowerArm, MassKg = 3f,  Shape = ColliderShape.Capsule },
            new BoneSetup { Bone = HumanBodyBones.RightLowerArm, Parent = HumanBodyBones.RightUpperArm,      SizeRef = null,                          MassKg = 2f,  Shape = ColliderShape.Capsule },
            new BoneSetup { Bone = HumanBodyBones.LeftUpperLeg,  Parent = HumanBodyBones.Hips,               SizeRef = HumanBodyBones.LeftLowerLeg,  MassKg = 10f, Shape = ColliderShape.Capsule },
            new BoneSetup { Bone = HumanBodyBones.LeftLowerLeg,  Parent = HumanBodyBones.LeftUpperLeg,       SizeRef = null,                          MassKg = 4f,  Shape = ColliderShape.Capsule },
            new BoneSetup { Bone = HumanBodyBones.RightUpperLeg, Parent = HumanBodyBones.Hips,               SizeRef = HumanBodyBones.RightLowerLeg, MassKg = 10f, Shape = ColliderShape.Capsule },
            new BoneSetup { Bone = HumanBodyBones.RightLowerLeg, Parent = HumanBodyBones.RightUpperLeg,      SizeRef = null,                          MassKg = 4f,  Shape = ColliderShape.Capsule },
        };

        /// <summary>
        /// Создаёт Rigidbody (kinematic) + Collider + CharacterJoint на каждой найденной
        /// кости ragdoll'а. Массы в сумме дают ~70 кг (см. TotalMassKg). Возвращает список
        /// всех созданных Rigidbody в порядке Setup — для DriverController._boneRigidbodies.
        /// </summary>
        private static List<Rigidbody> BuildRagdoll(Dictionary<HumanBodyBones, Transform> bones)
        {
            var rigidbodies = new List<Rigidbody>();

            // Сначала создаём Rigidbody на всех найденных костях — CharacterJoint.connectedBody
            // требует, чтобы Rigidbody родителя уже существовал.
            var boneRb = new Dictionary<HumanBodyBones, Rigidbody>();
            foreach (var entry in Setup)
            {
                if (!bones.TryGetValue(entry.Bone, out var t) || t == null) continue;

                var rb = t.GetComponent<Rigidbody>();
                if (rb == null)
                    rb = Undo.AddComponent<Rigidbody>(t.gameObject);
                rb.isKinematic = true;
                rb.mass        = entry.MassKg;
                boneRb[entry.Bone] = rb;
                rigidbodies.Add(rb);
            }

            // Затем коллайдеры и джойнты.
            foreach (var entry in Setup)
            {
                if (!bones.TryGetValue(entry.Bone, out var t) || t == null) continue;

                Transform sizeRefT = null;
                if (entry.SizeRef.HasValue)
                    bones.TryGetValue(entry.SizeRef.Value, out sizeRefT);

                switch (entry.Shape)
                {
                    case ColliderShape.Box:
                        SetupBoxCollider(t, sizeRefT);
                        break;
                    case ColliderShape.Sphere:
                        SetupSphereCollider(t);
                        break;
                    case ColliderShape.Capsule:
                        SetupCapsuleCollider(t, sizeRefT);
                        break;
                }

                if (entry.Parent.HasValue &&
                    boneRb.TryGetValue(entry.Parent.Value, out var parentRb) &&
                    parentRb != null)
                {
                    var joint = t.gameObject.GetComponent<CharacterJoint>();
                    if (joint == null)
                        joint = Undo.AddComponent<CharacterJoint>(t.gameObject);
                    joint.connectedBody = parentRb;
                }
            }

            return rigidbodies;
        }

        // Размеры коллайдеров — приблизительные, по расстоянию до дочерней кости.
        // Артисту рекомендуется подправить их вручную в Inspector после генерации.
        private static void SetupBoxCollider(Transform bone, Transform sizeRef)
        {
            // Не использовать ?? с Unity-объектами: fake-null обходит перегруженный ==.
            var box = bone.gameObject.GetComponent<BoxCollider>();
            if (box == null)
                box = Undo.AddComponent<BoxCollider>(bone.gameObject);
            if (sizeRef != null)
            {
                Vector3 local = bone.InverseTransformPoint(sizeRef.position);
                float dist = Mathf.Max(local.magnitude, 0.1f);
                box.center = local * 0.5f;
                box.size   = new Vector3(0.3f, dist, 0.2f);
            }
            else
            {
                box.size = new Vector3(0.3f, 0.25f, 0.2f);
            }
        }

        private static void SetupSphereCollider(Transform bone)
        {
            var sphere = bone.gameObject.GetComponent<SphereCollider>();
            if (sphere == null)
                sphere = Undo.AddComponent<SphereCollider>(bone.gameObject);
            sphere.radius = 0.12f;
        }

        private static void SetupCapsuleCollider(Transform bone, Transform sizeRef)
        {
            var capsule = bone.gameObject.GetComponent<CapsuleCollider>();
            if (capsule == null)
                capsule = Undo.AddComponent<CapsuleCollider>(bone.gameObject);
            if (sizeRef != null)
            {
                Vector3 local = bone.InverseTransformPoint(sizeRef.position);
                float dist = Mathf.Max(local.magnitude, 0.05f);
                capsule.direction = GetDominantAxis(local);
                capsule.height    = dist;
                capsule.radius    = 0.07f;
                capsule.center    = local * 0.5f;
            }
            else
            {
                capsule.height = 0.2f;
                capsule.radius = 0.06f;
            }
        }

        private static int GetDominantAxis(Vector3 v)
        {
            float ax = Mathf.Abs(v.x), ay = Mathf.Abs(v.y), az = Mathf.Abs(v.z);
            if (ax >= ay && ax >= az) return 0;
            if (ay >= ax && ay >= az) return 1;
            return 2;
        }

        // ─────────────────────────────────────────────────────────────────────
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
