using FlatOutLike.Data.Configs.Car;
using FlatOutLike.Data.Configs.Upgrades;
using FlatOutLike.Models;
using FlatOutLike.Services.Meta;

namespace FlatOutLike.Services.Car
{
    /// <summary>
    /// Реализация <see cref="ICarStatsService"/>: суммирует effectValue купленных уровней
    /// (1..текущий) по каждой категории апгрейда, переводит сумму в множитель (1 + sum/100)
    /// и просит <see cref="CarStatsConfig"/> создать клон с применёнными множителями.
    /// </summary>
    public sealed class CarStatsService : ICarStatsService
    {
        private readonly IMetaProgressService _meta;
        private readonly UpgradeCatalogConfig _upgradeCatalog;

        public CarStatsService(IMetaProgressService meta, UpgradeCatalogConfig upgradeCatalog)
        {
            _meta = meta;
            _upgradeCatalog = upgradeCatalog;
        }

        public CarStatsConfig GetEffectiveStats(CarStatsConfig baseStats)
        {
            if (baseStats == null)
                return null;

            var mods = new CarStatsModifiers(
                armorMultiplier: 1f + SumEffectValue(UpgradeCategory.Armor) / 100f,
                powerMultiplier: 1f + SumEffectValue(UpgradeCategory.Power) / 100f,
                nitroMultiplier: 1f + SumEffectValue(UpgradeCategory.Nitro) / 100f);

            return baseStats.CreateModifiedClone(mods);
        }

        /// <summary>Сумма effectValue всех купленных уровней (1..текущий уровень) категории.</summary>
        private float SumEffectValue(UpgradeCategory category)
        {
            int currentLevel = _meta.Profile.GetUpgradeLevel(category);
            float sum = 0f;

            for (int level = 1; level <= currentLevel; level++)
            {
                if (_upgradeCatalog.TryGetLevel(category, level, out var entry))
                    sum += entry.EffectValue;
            }

            return sum;
        }
    }
}
