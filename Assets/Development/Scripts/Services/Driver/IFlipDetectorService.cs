using FlatOutLike.Models;
using UnityEngine;

namespace FlatOutLike.Services.Driver
{
    /// <summary>
    /// Детектор флипов водителя в полёте: копит угловое вращение ragdoll-таза по трём
    /// локальным осям (Pitch/Roll/Yaw) и засчитывает флип на каждый полный оборот (360°).
    /// </summary>
    public interface IFlipDetectorService
    {
        /// <summary>Число засчитанных флипов с последнего <see cref="Reset"/>.</summary>
        int FlipCount { get; }

        /// <summary>Сбрасывает накопленное вращение и счётчик флипов — вызывается на старте полёта.</summary>
        void Reset();

        /// <summary>
        /// Скармливает текущий поворот кадра. Считает дельту вращения относительно
        /// предыдущего вызова и аккумулирует её по осям, если угловая скорость
        /// не ниже <c>DriverFlightConfig.RotationBonusThreshold</c>.
        /// </summary>
        void Feed(Quaternion rotation, float dt);
    }
}
