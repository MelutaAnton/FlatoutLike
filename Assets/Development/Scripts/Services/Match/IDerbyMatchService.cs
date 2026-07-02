using System.Collections.Generic;
using FlatOutLike.Controllers.Car;
using UnityEngine;

namespace FlatOutLike.Services.Match
{
    /// <summary>
    /// Управляет жизненным циклом одного Derby-матча: спавн участников,
    /// отслеживание живых машин и завершение матча при определении победителя.
    /// </summary>
    public interface IDerbyMatchService
    {
        /// <summary>
        /// Спавнит участников матча через <see cref="FlatOutLike.Factories.Car.ICarFactory"/>,
        /// начинает отслеживание живых машин и переводит GameFlow в <c>GameState.DerbyMatch</c>.
        /// </summary>
        IReadOnlyList<CarController> StartMatch(IReadOnlyList<Transform> spawnPoints);
    }
}
