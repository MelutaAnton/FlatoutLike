using FlatOutLike.Data.Configs.Destructible;
using UnityEngine;

namespace FlatOutLike.Factories.Destructible
{
    /// <summary>
    /// Контракт фабрики разрушаемых объектов.
    /// Единственная точка доступа к пулу обломков.
    /// </summary>
    public interface IDestructibleFactory
    {
        /// <summary>
        /// Спавнит обломки из пула вокруг <paramref name="origin"/>
        /// с параметрами из <paramref name="config"/>.
        /// </summary>
        void SpawnDebris(Vector3 origin, DestructibleConfig config);

        /// <summary>
        /// То же, но каждый обломок дополнительно наследует <paramref name="baseVelocity"/> —
        /// используется, когда источник разрушения движется (например, лобовое стекло
        /// при вылете водителя: осколки летят по ходу машины).
        /// </summary>
        void SpawnDebris(Vector3 origin, DestructibleConfig config, Vector3 baseVelocity);
    }
}
