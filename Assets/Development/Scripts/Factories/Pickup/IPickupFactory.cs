using System;
using FlatOutLike.Controllers.Pickups;
using FlatOutLike.Models;
using UnityEngine;

namespace FlatOutLike.Factories.Pickup
{
    /// <summary>
    /// Фабрика пикапов. Единственное место, работающее с пулом напрямую.
    /// </summary>
    public interface IPickupFactory
    {
        /// <summary>
        /// Берёт пикап из пула, размещает в <paramref name="position"/> и активирует.
        /// <paramref name="onCollected"/> вызывается пикапом при подборе машиной.
        /// </summary>
        IPickup Spawn(PickupType type, Vector3 position, Action<IPickup> onCollected);

        /// <summary>Возвращает пикап в пул.</summary>
        void Despawn(IPickup pickup);
    }
}
