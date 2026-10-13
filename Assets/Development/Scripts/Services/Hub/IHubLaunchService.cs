using FlatOutLike.Models;

namespace FlatOutLike.Services.Hub
{
    /// <summary>
    /// Запуск режимов из BaseHub (game-design §6 «Запуск режимов»): персонаж заходит в зону
    /// запуска постройки, стоит в ней LaunchConfirmSeconds — и GameFlow переключается в режим зоны.
    /// Триггеры (LaunchZoneController) только сообщают о входе/выходе; таймер и решение — здесь.
    /// Запуск доступен только у построенного здания (IBuildProgressService.IsBuilt, шаг 12a): пока
    /// здание строится, та же зона принимает вложения. Завершение стройки запуск не начинает —
    /// персонаж перезаходит в зону.
    /// </summary>
    public interface IHubLaunchService
    {
        /// <summary>Персонаж вошёл в зону постройки. Зона без HasLaunchZone, выключенная или недостроенная — игнор.</summary>
        void EnterZone(BuildingId id);

        /// <summary>
        /// Персонаж вышел из зоны: зона убирается из занятых. Выход из активной зоны переключает
        /// отсчёт на последнюю оставшуюся занятую (с нуля); выход из неактивной активную не меняет.
        /// </summary>
        void ExitZone(BuildingId id);

        /// <summary>Есть ли зона, в которой сейчас идёт отсчёт подтверждения.</summary>
        bool TryGetActiveZone(out BuildingId id);

        /// <summary>Прогресс подтверждения активной зоны 0..1 (0 — нет активной зоны).</summary>
        float Progress01 { get; }
    }
}
