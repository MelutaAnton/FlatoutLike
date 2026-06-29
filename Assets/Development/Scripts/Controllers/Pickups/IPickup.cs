using System;
using FlatOutLike.Models;

namespace FlatOutLike.Controllers.Pickups
{
    /// <summary>
    /// Контракт компонента пикапа. Реализуют <see cref="RepairPickup"/> и <see cref="NitroPickup"/>.
    /// Фабрика вызывает <see cref="Activate"/> после взятия объекта из пула.
    /// </summary>
    public interface IPickup
    {
        PickupType Type { get; }

        /// <summary>
        /// Подготавливает пикап к использованию после взятия из пула.
        /// Сбрасывает флаг собранности и сохраняет колбэк для оповещения фабрики.
        /// </summary>
        void Activate(Action<IPickup> onCollected);
    }
}
