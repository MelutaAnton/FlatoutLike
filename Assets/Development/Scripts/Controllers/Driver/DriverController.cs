using System.Collections.Generic;
using UnityEngine;

namespace FlatOutLike.Controllers.Driver
{
    /// <summary>
    /// Водитель, сидящий в машине. В обычном состоянии кости — kinematic, коллайдеры костей
    /// ВЫКЛЮЧЕНЫ (включённые коллайдеры внутри кузова непрерывно депенетрируют с коллайдерами
    /// машины → тряска и перевороты), Animator играет анимацию вождения.
    /// При катапультировании (<see cref="Eject"/>) отсоединяется от машины, Animator выключается,
    /// столкновения со своей машиной игнорируются (вылет «через лобовое»), коллайдеры включаются,
    /// кости ragdoll становятся динамическими и получают импульс.
    /// Только Unity API — бизнес-решение "катапультировать или нет" принимает EjectCommand.
    /// </summary>
    public sealed class DriverController : MonoBehaviour
    {
        [SerializeField] private Animator _animator;
        [SerializeField] private Rigidbody[] _boneRigidbodies;
        [SerializeField] private Rigidbody _pelvis;

        private readonly List<Collider> _boneColliders = new List<Collider>();

        public bool IsEjected { get; private set; }

        /// <summary>Таз ragdoll — используется как цель орбитальной камеры после катапультирования.</summary>
        public Transform CameraTarget => _pelvis != null ? _pelvis.transform : transform;

        /// <summary>
        /// Rigidbody таза ragdoll — точка приложения steer-коррекции и буста в полёте
        /// (см. <c>DriverFlightController</c>). Может быть <c>null</c> до настройки префаба.
        /// </summary>
        public Rigidbody Pelvis => _pelvis;

        /// <summary>
        /// Все кости ragdoll (включая таз) — полётные силы прикладываются РАВНОМЕРНО ко всем,
        /// иначе разгоняемый таз растягивает CharacterJoint'ы и скиннед-меш даёт «иглы».
        /// </summary>
        public IReadOnlyList<Rigidbody> Bones => _boneRigidbodies;

        private void Awake()
        {
            // Кэшируем коллайдеры костей и выключаем их на время поездки —
            // физика сидящего ragdoll не должна взаимодействовать с кузовом.
            GetComponentsInChildren(includeInactive: true, _boneColliders);
            SetBoneCollidersEnabled(false);

            // Сидячее состояние — принудительно kinematic, НЕ полагаемся на флаги префаба:
            // ragdoll, собранный Wizard'ом, приходит dynamic+gravity, и физика всю поездку
            // копит скорость падения в костях → на eject водителя вбивает в землю.
            if (_boneRigidbodies != null)
            {
                foreach (var bone in _boneRigidbodies)
                {
                    if (bone != null)
                        bone.isKinematic = true;
                }
            }
        }

        /// <summary>
        /// Отсоединяет водителя от машины и переводит ragdoll в динамический режим
        /// с приложением импульса. <paramref name="carRoot"/> — корень своей машины:
        /// столкновения с её коллайдерами игнорируются навсегда, чтобы включение
        /// коллайдеров внутри салона не вызвало депенетрационный взрыв.
        /// Вызывается один раз — повторный вызов игнорируется
        /// (guard на стороне EjectCommand через IsEjected).
        /// </summary>
        public void Eject(Vector3 velocity, Transform carRoot)
        {
            if (IsEjected) return;
            IsEjected = true;

            IgnoreOwnCarCollisions(carRoot);

            transform.SetParent(null, worldPositionStays: true);

            if (_animator != null)
                _animator.enabled = false;

            // «Иглы» на меше в полёте: служебные кости скелета, лежащие ПОД контейнером "Root"
            // (а не под физкостями), после eject остаются в точке вылета — вершины одежды
            // с их весами растягиваются от тела до места старта (чем дальше улетел, тем длиннее).
            // Перепарентование: таз наверх, контейнер скелета — под таз; worldPositionStays=true,
            // ничего не сдвигается, но служебные кости теперь едут вместе с телом.
            if (_pelvis != null)
            {
                Transform skeletonRoot = _pelvis.transform.parent;
                if (skeletonRoot != null && skeletonRoot != transform)
                {
                    _pelvis.transform.SetParent(transform, worldPositionStays: true);
                    skeletonRoot.SetParent(_pelvis.transform, worldPositionStays: true);
                }
            }

            // Ragdoll уводит кости далеко от запечённых bounds SkinnedMeshRenderer'а —
            // без пересчёта по вершинам фрустум-каллинг вырезает меш при части ракурсов
            // (тело исчезает, остаётся только причёска на кости головы).
            foreach (var skinned in GetComponentsInChildren<SkinnedMeshRenderer>(true))
                skinned.updateWhenOffscreen = true;

            // Joint projection жёстко ограничивает растяжение костей при больших скоростях —
            // без неё длинный полёт с ускорением вытягивает конечности (визуальные «иглы»).
            foreach (var joint in GetComponentsInChildren<CharacterJoint>(true))
            {
                joint.enableProjection = true;
                joint.projectionDistance = 0.05f;
            }

            SetBoneCollidersEnabled(true);

            if (_boneRigidbodies != null)
            {
                foreach (var bone in _boneRigidbodies)
                {
                    if (bone == null || bone == _pelvis) continue; // таз обрабатывается отдельно ниже
                    HardenBonePhysics(bone);
                    bone.isKinematic = false;
                    bone.detectCollisions = true;
                    bone.linearVelocity = velocity;
                }
            }

            if (_pelvis != null)
            {
                HardenBonePhysics(_pelvis);
                _pelvis.isKinematic = false;
                _pelvis.detectCollisions = true;
                // Скорость ЗАДАЁТСЯ, а не добавляется (AddForce): у dynamic-собранного ragdoll
                // в костях может сидеть накопленная скорость — итог был бы непредсказуем.
                _pelvis.linearVelocity = velocity;
            }
        }

        /// <summary>
        /// Анти-взрыв для скоростных приземлений (High Jump, ~30 м/с): мелкие быстрые кости
        /// туннелируют в землю, депенетрация выстреливает их от тела быстрее, чем джойнт-солвер
        /// сходится — конечности «вытягиваются в иглы». CCD убирает туннелирование,
        /// лимит депенетрации гасит выталкивающий импульс, доп. итерации дают солверу сойтись.
        /// </summary>
        private static void HardenBonePhysics(Rigidbody bone)
        {
            bone.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            bone.maxDepenetrationVelocity = 3f;
            bone.solverIterations = 12;
            bone.solverVelocityIterations = 12;
        }

        private void SetBoneCollidersEnabled(bool value)
        {
            foreach (var boneCollider in _boneColliders)
            {
                if (boneCollider != null)
                    boneCollider.enabled = value;
            }
        }

        private void IgnoreOwnCarCollisions(Transform carRoot)
        {
            if (carRoot == null) return;

            foreach (var carCollider in carRoot.GetComponentsInChildren<Collider>(true))
            {
                // Коллайдеры самого водителя — дети машины до детача; пропускаем их,
                // иначе выключим ragdoll'у самостолкновения.
                if (carCollider == null || carCollider.transform.IsChildOf(transform)) continue;

                foreach (var boneCollider in _boneColliders)
                {
                    if (boneCollider != null)
                        Physics.IgnoreCollision(boneCollider, carCollider, true);
                }
            }
        }
    }
}
