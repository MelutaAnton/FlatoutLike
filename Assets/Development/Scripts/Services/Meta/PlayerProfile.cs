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
        private readonly Dictionary<UpgradeCategory, int> _upgradeLevels = new Dictionary<UpgradeCategory, int>();

        public IReadOnlyCollection<string> OwnedCarIds => _ownedCarIds;

        public bool OwnsCar(string carId) => !string.IsNullOrEmpty(carId) && _ownedCarIds.Contains(carId);

        public void AddOwnedCar(string carId)
        {
            if (!string.IsNullOrEmpty(carId))
                _ownedCarIds.Add(carId);
        }

        public int GetUpgradeLevel(UpgradeCategory category) =>
            _upgradeLevels.TryGetValue(category, out var level) ? level : 0;

        public void SetUpgradeLevel(UpgradeCategory category, int level) =>
            _upgradeLevels[category] = level;

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
