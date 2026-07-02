using UnityEngine;

namespace FlatOutLike.Controllers.Camera
{
    /// <summary>
    /// Переключатель активного камера-поведения на арене. Активирует одно поведение,
    /// выключая остальные — единственная точка входа для смены камеры (chase / orbit).
    /// </summary>
    public interface ICameraRig
    {
        /// <summary>Включает chase-камеру (следование за машиной) с целью target.</summary>
        void ActivateChase(Transform target);

        /// <summary>Включает орбитальную камеру (вокруг ragdoll-водителя) с целью target.</summary>
        void ActivateOrbit(Transform target);
    }
}
