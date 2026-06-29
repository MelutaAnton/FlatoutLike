using System.Collections.Generic;
using FlatOutLike.Controllers.Car;
using FlatOutLike.Data.Configs.Match;
using UnityEngine;

namespace FlatOutLike.Factories.Car
{
    /// <summary>
    /// Фабрика машин. Инстанцирует и конфигурирует игрока + врагов для одного матча.
    /// </summary>
    public interface ICarFactory
    {
        /// <summary>
        /// Инстанцирует машину игрока (spawnPoints[0]) и всех врагов (spawnPoints[1..]).
        /// Возвращает список всех заспавненных <see cref="CarController"/> в порядке спавна
        /// (индекс 0 = игрок).
        /// </summary>
        IReadOnlyList<CarController> SpawnMatch(DerbyMatchConfig config, IReadOnlyList<Transform> spawnPoints);
    }
}
