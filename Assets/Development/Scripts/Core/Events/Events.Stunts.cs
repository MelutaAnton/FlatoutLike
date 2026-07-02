using FlatOutLike.Models;
using UnityEngine;

namespace FlatOutLike.Core.Events
{
    /// <summary>
    /// Публикуется EjectCommand (Core/Commands) в момент катапультирования водителя.
    /// Подписчики (камера, HUD, статистика) узнают, чья машина и с какой скоростью выбросила водителя.
    /// </summary>
    public readonly struct DriverEjectedEvent
    {
        public readonly CarId      Source;
        public readonly Vector3    EjectVelocity;
        public readonly EjectReason Reason;

        public DriverEjectedEvent(CarId source, Vector3 ejectVelocity, EjectReason reason)
        {
            Source        = source;
            EjectVelocity = ejectVelocity;
            Reason        = reason;
        }
    }
}
