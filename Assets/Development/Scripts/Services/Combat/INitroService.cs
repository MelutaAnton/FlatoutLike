using FlatOutLike.Models;

namespace FlatOutLike.Services.Combat
{
    /// <summary>
    /// Управляет запасом нитро для каждой машины.
    /// </summary>
    public interface INitroService
    {
        /// <summary>Регистрирует машину с заданной ёмкостью бака. Начальный уровень — 0.</summary>
        void RegisterCar(CarId id, float capacity);

        /// <summary>
        /// Пытается списать <paramref name="amount"/> единиц нитро.
        /// Возвращает true и списывает, если хватает; иначе false, уровень не меняется.
        /// </summary>
        bool TryConsume(CarId id, float amount);

        /// <summary>
        /// Доливает <paramref name="fraction"/> * capacity нитро (с cap по ёмкости бака).
        /// </summary>
        void Refill(CarId id, float fraction);

        /// <summary>Текущий уровень нитро в долях 0..1.</summary>
        float GetNitro01(CarId id);
    }
}
