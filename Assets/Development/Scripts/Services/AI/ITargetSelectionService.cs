using FlatOutLike.Controllers.Car;
using FlatOutLike.Models;

namespace FlatOutLike.Services.AI
{
    /// <summary>
    /// Выбирает текущую цель для AI-машины по заданному режиму из профиля личности.
    /// Учитывает только живых кандидатов (GetHp > 0) и не включает self в выборку.
    /// </summary>
    public interface ITargetSelectionService
    {
        /// <summary>
        /// Возвращает лучшую цель для <paramref name="self"/> согласно <paramref name="mode"/>.
        /// Возвращает <c>null</c>, если кандидатов нет.
        /// </summary>
        CarController SelectTarget(CarController self, TargetSelectionMode mode);
    }
}
