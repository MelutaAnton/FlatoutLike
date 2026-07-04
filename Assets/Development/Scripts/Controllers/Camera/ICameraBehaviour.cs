using UnityEngine;

namespace FlatOutLike.Controllers.Camera
{
    /// <summary>
    /// Общий контракт для конкретных камера-поведений (chase, orbit), которыми управляет
    /// <see cref="ICameraRig"/>.
    /// </summary>
    public interface ICameraBehaviour
    {
        void SetTarget(Transform target);

        /// <summary>
        /// Делает поведение активным/неактивным. Как именно — деталь реализации:
        /// Cinemachine-адаптеры переключают Priority виртуальной камеры (Brain блендит сам).
        /// </summary>
        void SetBehaviourActive(bool active);
    }
}
