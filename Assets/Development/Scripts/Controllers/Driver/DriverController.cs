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

        private void Awake()
        {
            // Кэшируем коллайдеры костей и выключаем их на время поездки —
            // физика сидящего ragdoll не должна взаимодействовать с кузовом.
            GetComponentsInChildren(includeInactive: true, _boneColliders);
            SetBoneCollidersEnabled(false);
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

            // Ragdoll уводит кости далеко от запечённых bounds SkinnedMeshRenderer'а —
            // без пересчёта по вершинам фрустум-каллинг вырезает меш при части ракурсов
            // (тело исчезает, остаётся только причёска на кости головы).
            foreach (var skinned in GetComponentsInChildren<SkinnedMeshRenderer>(true))
                skinned.updateWhenOffscreen = true;

            SetBoneCollidersEnabled(true);

            if (_boneRigidbodies != null)
            {
                foreach (var bone in _boneRigidbodies)
                {
                    if (bone == null || bone == _pelvis) continue; // таз обрабатывается отдельно ниже
                    bone.isKinematic = false;
                    bone.detectCollisions = true;
                    bone.linearVelocity = velocity;
                }
            }

            if (_pelvis != null)
            {
                _pelvis.isKinematic = false;
                _pelvis.detectCollisions = true;
                _pelvis.AddForce(velocity, ForceMode.VelocityChange);
            }
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
