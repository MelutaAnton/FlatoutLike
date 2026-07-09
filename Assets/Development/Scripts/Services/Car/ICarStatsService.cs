using FlatOutLike.Data.Configs.Car;

namespace FlatOutLike.Services.Car
{
    /// <summary>
    /// Собирает effective stats машины из базового конфига и текущих уровней апгрейдов
    /// профиля игрока (§8 game-design.md).
    /// </summary>
    public interface ICarStatsService
    {
        /// <summary>
        /// Возвращает статы с применёнными множителями апгрейдов. При отсутствии купленных
        /// уровней (множители 1.0) возвращает исходный <paramref name="baseStats"/> без клонирования.
        /// </summary>
        CarStatsConfig GetEffectiveStats(CarStatsConfig baseStats);
    }
}
