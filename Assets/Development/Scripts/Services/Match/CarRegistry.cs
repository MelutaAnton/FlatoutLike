using System.Collections.Generic;
using FlatOutLike.Controllers.Car;
using FlatOutLike.Models;

namespace FlatOutLike.Services.Match
{
    /// <summary>
    /// Реализация реестра машин. Pure C#, без MonoBehaviour.
    /// Внутренний список не экспонируется напрямую — только через IReadOnlyList.
    /// </summary>
    public sealed class CarRegistry : ICarRegistry
    {
        private readonly List<CarController> _cars = new List<CarController>();

        // Kill-feed (Шаг 10b): отображаемые имена машин по id. Чистится синхронно
        // с _cars — см. Unregister/Clear.
        private readonly Dictionary<CarId, string> _names = new Dictionary<CarId, string>();

        /// <inheritdoc/>
        public IReadOnlyList<CarController> Cars => _cars;

        /// <inheritdoc/>
        public CarController Player { get; private set; }

        /// <inheritdoc/>
        public bool TryGetPlayerCarId(out Models.CarId carId)
        {
            if (Player == null)
            {
                carId = default;
                return false;
            }

            carId = Player.CarId;
            return true;
        }

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
            _names.Remove(car.CarId);
            if (Player == car)
                Player = null;
        }

        /// <inheritdoc/>
        public void RegisterName(CarId id, string displayName) => _names[id] = displayName;

        /// <inheritdoc/>
        public bool TryGetName(CarId id, out string displayName) => _names.TryGetValue(id, out displayName);

        /// <inheritdoc/>
        public void Clear()
        {
            _cars.Clear();
            _names.Clear();
            Player = null;
        }
    }
}
