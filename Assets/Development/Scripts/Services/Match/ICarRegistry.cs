using System.Collections.Generic;
using FlatOutLike.Controllers.Car;

namespace FlatOutLike.Services.Match
{
    /// <summary>
    /// Реестр всех машин текущего матча. Единственный источник списка участников для AI-сервисов.
    /// Заполняется <c>CarFactory</c> после спавна, очищается перед повторным спавном.
    /// </summary>
    public interface ICarRegistry
    {
        /// <summary>Все зарегистрированные машины (живые и мёртвые).</summary>
        IReadOnlyList<CarController> Cars { get; }

        /// <summary>
        /// Машина игрока. Последняя зарегистрированная с <c>isPlayer = true</c>.
        /// Может быть <c>null</c>, если игрок не зарегистрирован или реестр очищен.
        /// </summary>
        CarController Player { get; }

        /// <summary>
        /// Id машины игрока без выдачи MonoBehaviour наружу — для pure C# потребителей
        /// (presenter'ы и т.п.), которым нужен только идентификатор, а не контроллер.
        /// <c>false</c>, если игрок не зарегистрирован.
        /// </summary>
        bool TryGetPlayerCarId(out Models.CarId carId);

        /// <summary>
        /// Регистрирует машину. Если машина уже есть в реестре — игнорирует (без дублей).
        /// </summary>
        void Register(CarController car, bool isPlayer);

        /// <summary>Удаляет машину из реестра (для будущего использования при уничтожении).</summary>
        void Unregister(CarController car);

        /// <summary>
        /// Регистрирует отображаемое имя машины для kill-feed (Шаг 10b) — единственный источник
        /// имён участников матча. Повторный вызов с тем же id перезаписывает имя.
        /// </summary>
        void RegisterName(Models.CarId id, string displayName);

        /// <summary>
        /// Отображаемое имя машины, если оно было зарегистрировано. <c>false</c>, если для id
        /// имени нет — вызывающая сторона (presenter) сама решает фоллбэк (например, "CAR &lt;id&gt;").
        /// </summary>
        bool TryGetName(Models.CarId id, out string displayName);

        /// <summary>
        /// Очищает весь реестр и сбрасывает ссылку на игрока.
        /// Вызывается перед повторным спавном, чтобы не было дублей.
        /// </summary>
        void Clear();
    }
}
