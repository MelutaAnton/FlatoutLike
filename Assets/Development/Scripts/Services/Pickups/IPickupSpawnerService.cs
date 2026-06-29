using System.Collections.Generic;

namespace FlatOutLike.Services.Pickups
{
    /// <summary>
    /// Управляет спавном и респавном пикапов по зонам арены.
    /// </summary>
    public interface IPickupSpawnerService
    {
        /// <summary>
        /// Запускает систему: сразу спавнит пикап в каждой зоне.
        /// Вызывается один раз из <c>GameLifetimeScope</c> после построения контейнера.
        /// </summary>
        void StartSpawning(IReadOnlyList<PickupZone> zones);
    }
}
