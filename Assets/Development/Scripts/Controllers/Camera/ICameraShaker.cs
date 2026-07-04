using UnityEngine;

namespace FlatOutLike.Controllers.Camera
{
    /// <summary>
    /// Абстракция тряски камеры. Позволяет <c>CameraShakeService</c> (pure C#) инициировать
    /// тряску, не зная о Cinemachine — реализация (<see cref="CinemachineCameraShaker"/>)
    /// изолирована в слое Controllers.
    /// </summary>
    public interface ICameraShaker
    {
        /// <summary>
        /// Инициирует тряску камеры.
        /// </summary>
        /// <param name="position">Мировая точка, из которой исходит импульс (место удара).</param>
        /// <param name="force">Сила импульса, уже посчитанная сервисом (0..MaxForce из конфига).</param>
        void Shake(Vector3 position, float force);
    }
}
