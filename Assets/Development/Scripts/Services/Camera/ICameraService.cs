using UnityEngine;

namespace FlatOutLike.Services.Camera
{
    /// <summary>
    /// Управляет выбором активной камеры арены: chase-режим на старте матча,
    /// переключение на orbit при катапультировании водителя игрока.
    /// </summary>
    public interface ICameraService
    {
        /// <summary>Включает chase-камеру на машину игрока (вызывается при старте матча).</summary>
        void FollowPlayerCar(Transform target);
    }
}
