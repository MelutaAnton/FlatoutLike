using FlatOutLike.Models;

namespace FlatOutLike.Services.Progression
{
    /// <summary>
    /// Стадийная стройка BaseHub (game-design §6): прогресс этапов зданий и потоковое вложение
    /// монет, пока персонаж стоит в зоне недостроенного здания. Триггеры (InvestZoneController)
    /// только сообщают о входе/выходе; поток, списание и сохранение — здесь. Разблокировки
    /// подписываются на BuildingCompletedEvent — сервис GameFlow не вызывает.
    /// </summary>
    public interface IBuildProgressService
    {
        /// <summary>Число завершённых этапов здания (0 — нет в каталоге или ничего не построено).</summary>
        int GetCompletedStages(BuildingId id);

        /// <summary>Число этапов здания по каталогу (0 — нет в каталоге или построено со старта).</summary>
        int GetTotalStages(BuildingId id);

        /// <summary>
        /// Построено ли здание: нет в каталоге или выключено — false; без этапов — true;
        /// иначе все этапы завершены.
        /// </summary>
        bool IsBuilt(BuildingId id);

        /// <summary>
        /// Здание строится: есть в каталоге, включено и не построено (значит, есть незавершённые
        /// этапы). Ровно те здания, чья зона принимает вложения и у чьего ценника виден прогресс.
        /// </summary>
        bool IsUnderConstruction(BuildingId id);

        /// <summary>Сколько монет вложено в текущий этап (0, если построено).</summary>
        int GetInvestedInStage(BuildingId id);

        /// <summary>Цена текущего этапа; 0, если здание построено или его нет в каталоге.</summary>
        int GetCurrentStageCost(BuildingId id);

        /// <summary>Доход последнего завершённого этапа, монет/мин; 0, если этапы не завершены.</summary>
        int GetCurrentIncomePerMinute(BuildingId id);

        /// <summary>Персонаж вошёл в зону здания. Нет в каталоге / выключено / построено — игнор.</summary>
        void EnterZone(BuildingId id);

        /// <summary>Персонаж вышел из зоны здания. Выход из активной зоны останавливает поток.</summary>
        void ExitZone(BuildingId id);
    }
}
