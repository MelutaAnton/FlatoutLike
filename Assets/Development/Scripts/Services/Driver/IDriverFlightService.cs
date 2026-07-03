using UnityEngine;

namespace FlatOutLike.Services.Driver
{
    /// <summary>
    /// Управляет состоянием свободного полёта водителя после катапультирования на трамплине:
    /// боковая steer-коррекция, буст, отслеживание пика высоты и времени полёта.
    /// </summary>
    public interface IDriverFlightService
    {
        /// <summary>Идёт ли сейчас полёт (между BeginFlight и NotifyLanded).</summary>
        bool IsInFlight { get; }

        /// <summary>Максимальная высота над стартовой точкой полёта (метры).</summary>
        float PeakHeight { get; }

        /// <summary>Время, прошедшее с начала полёта (секунды).</summary>
        float AirTime { get; }

        /// <summary>Импульс буста (м/с) — читается контроллером для ForceMode.VelocityChange.</summary>
        float BoostImpulse { get; }

        /// <summary>Начинает полёт: сбрасывает пик/время/бусты и детектор флипов.</summary>
        void BeginFlight(Vector3 startPosition);

        /// <summary>
        /// Боковая steer-коррекция полёта по вводу Look: X — поперёк горизонтальной проекции
        /// скорости, Y — вдоль неё. Включает штраф торможения при активной коррекции.
        /// </summary>
        Vector3 CalculateSteerAcceleration(Vector2 look, Vector3 currentVelocity);

        /// <summary>Пытается списать один буст. Возвращает false, если лимит на полёт исчерпан.</summary>
        bool TryConsumeBoost();

        /// <summary>Обновляет пик высоты и время полёта. Не в полёте — не делает ничего.</summary>
        void Track(Vector3 position, float dt);

        /// <summary>
        /// Одноразово завершает полёт и публикует <see cref="FlatOutLike.Core.Events.DriverLandedEvent"/>.
        /// Повторный вызов вне полёта игнорируется.
        /// </summary>
        void NotifyLanded();
    }
}
