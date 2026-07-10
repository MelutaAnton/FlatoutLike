using System.Collections.Generic;
using FlatOutLike.Models;

namespace FlatOutLike.Services.Meta
{
    public sealed class PlayerProfile : IPlayerProfile
    {
        public int Coins { get; set; }
        public float BestHighJumpHeight { get; set; }
        public string SelectedCarId { get; set; } = string.Empty;

        private readonly HashSet<string> _ownedCarIds = new HashSet<string>();

        // Составной ключ (carId, category) — per-car прогресс апгрейдов (§8/§14.3 game-design.md).
        private readonly Dictionary<(string CarId, UpgradeCategory Category), int> _upgradeLevels =
            new Dictionary<(string, UpgradeCategory), int>();

        public IReadOnlyCollection<string> OwnedCarIds => _ownedCarIds;

        public bool OwnsCar(string carId) => !string.IsNullOrEmpty(carId) && _ownedCarIds.Contains(carId);

        public void AddOwnedCar(string carId)
        {
            if (!string.IsNullOrEmpty(carId))
                _ownedCarIds.Add(carId);
        }

        public int GetUpgradeLevel(string carId, UpgradeCategory category)
        {
            if (string.IsNullOrEmpty(carId))
                return 0;

            return _upgradeLevels.TryGetValue((carId, category), out var level) ? level : 0;
        }

        public void SetUpgradeLevel(string carId, UpgradeCategory category, int level)
        {
            if (string.IsNullOrEmpty(carId))
                return;

            _upgradeLevels[(carId, category)] = level;
        }

        public IReadOnlyCollection<(string CarId, UpgradeCategory Category, int Level)> GetAllUpgradeLevels()
        {
            var result = new List<(string CarId, UpgradeCategory Category, int Level)>(_upgradeLevels.Count);
            foreach (var kvp in _upgradeLevels)
                result.Add((kvp.Key.CarId, kvp.Key.Category, kvp.Value));

            return result;
        }

        /// <summary>Заполняет владение машинами при загрузке из хранилища (см. <see cref="PlayerPrefsProfileStorage"/>).</summary>
        internal void SetOwnedCarIds(IEnumerable<string> ids)
        {
            _ownedCarIds.Clear();
            if (ids == null)
                return;

            foreach (var id in ids)
                if (!string.IsNullOrEmpty(id))
                    _ownedCarIds.Add(id);
        }
    }
}
