using FlatOutLike.Core.Events;

namespace FlatOutLike.Services.Stunts
{
    /// <summary>
    /// Фиксирует результат попытки «High Jump» (пиковая высота — единственная метрика),
    /// обновляет личный рекорд игрока и публикует награду.
    /// </summary>
    public interface IHighJumpScoreService
    {
        /// <summary>
        /// Обновляет рекорд высоты <c>IPlayerProfile.BestHighJumpHeight</c>, начисляет монеты
        /// от высоты и публикует <see cref="StuntsScoredEvent"/> + <see cref="RewardEarnedEvent"/>.
        /// </summary>
        void ScoreAttempt(DriverLandedEvent landed);
    }
}
