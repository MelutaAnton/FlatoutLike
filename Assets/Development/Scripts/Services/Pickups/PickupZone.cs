using System;
using FlatOutLike.Models;
using UnityEngine;

namespace FlatOutLike.Services.Pickups
{
    /// <summary>
    /// Точка спавна пикапа на арене: позиция в мире и тип пикапа.
    /// Serializable, чтобы вставлять прямо в Inspector <c>GameLifetimeScope</c>.
    /// </summary>
    [Serializable]
    public struct PickupZone
    {
        [Tooltip("Transform точки появления пикапа.")]
        public Transform point;

        [Tooltip("Тип пикапа, который будет появляться в этой точке.")]
        public PickupType type;
    }
}
