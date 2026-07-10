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
        /// Возвращает статы с применёнными множителями апгрейдов КОНКРЕТНОЙ машины (per-car
        /// прогресс, §8/§14.3 game-design.md). При отсутствии купленных уровней (множители 1.0)
        /// или пустом <paramref name="carId"/> возвращает исходный <paramref name="baseStats"/>
        /// без клонирования.
        /// </summary>
        CarStatsConfig GetEffectiveStats(string carId, CarStatsConfig baseStats);
    }
}
