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

        /// <summary>Включает/выключает LateUpdate-логику поведения (через enabled компонента).</summary>
        void SetBehaviourActive(bool active);
    }
}
