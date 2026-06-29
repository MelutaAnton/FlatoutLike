using System.Collections.Generic;
using FlatOutLike.Controllers.Car;

namespace FlatOutLike.Services.Match
{
    /// <summary>
    /// Реализация реестра машин. Pure C#, без MonoBehaviour.
    /// Внутренний список не экспонируется напрямую — только через IReadOnlyList.
    /// </summary>
    public sealed class CarRegistry : ICarRegistry
    {
        private readonly List<CarController> _cars = new List<CarController>();

        /// <inheritdoc/>
        public IReadOnlyList<CarController> Cars => _cars;

        /// <inheritdoc/>
        public CarController Player { get; private set; }

        /// <inheritdoc/>
        public void Register(CarController car, bool isPlayer)
        {
            if (car == null || _cars.Contains(car)) return;

            _cars.Add(car);
            if (isPlayer)
                Player = car;
        }

        /// <inheritdoc/>
        public void Unregister(CarController car)
        {
            if (car == null) return;

            _cars.Remove(car);
            if (Player == car)
                Player = null;
        }

        /// <inheritdoc/>
        public void Clear()
        {
            _cars.Clear();
            Player = null;
        }
    }
}
