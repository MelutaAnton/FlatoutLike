using FlatOutLike.Core.Events;

namespace FlatOutLike.Services.Stunts
{
    /// <summary>
    /// Считает очки за попытку «High Jump», обновляет личный рекорд игрока и публикует награду.
    /// </summary>
    public interface IHighJumpScoreService
    {
        /// <summary>
        /// Считает итоговый счёт попытки, обновляет рекорд высоты
        /// <c>IPlayerProfile.BestHighJumpHeight</c> (рекорд — по метрам, счёт — только на монеты)
        /// и публикует <see cref="StuntsScoredEvent"/> + <see cref="RewardEarnedEvent"/>.
        /// Возвращает посчитанный счёт.
        /// </summary>
        int ScoreAttempt(DriverLandedEvent landed);
    }
}
