using FlatOutLike.Models;
using UnityEngine;

namespace FlatOutLike.Core.Events
{
    /// <summary>
    /// Публикуется EjectCommand (Core/Commands) в момент катапультирования водителя.
    /// Подписчики (камера, HUD, статистика) узнают, чья машина и с какой скоростью выбросила водителя.
    /// </summary>
    public readonly struct DriverEjectedEvent
    {
        public readonly CarId      Source;
        public readonly Vector3    EjectVelocity;
        public readonly EjectReason Reason;

        public DriverEjectedEvent(CarId source, Vector3 ejectVelocity, EjectReason reason)
        {
            Source        = source;
            EjectVelocity = ejectVelocity;
            Reason        = reason;
        }
    }

    /// <summary>
    /// Публикуется <c>DriverFlightService.NotifyLanded</c> при приземлении водителя после полёта.
    /// Подписчики (HighJumpSessionService, HUD) получают итоговую статистику попытки.
    /// </summary>
    public readonly struct DriverLandedEvent
    {
        public readonly float PeakHeight;
        public readonly int   FlipCount;
        public readonly float TotalAirTime;

        public DriverLandedEvent(float peakHeight, int flipCount, float totalAirTime)
        {
            PeakHeight   = peakHeight;
            FlipCount    = flipCount;
            TotalAirTime = totalAirTime;
        }
    }

    /// <summary>
    /// Публикуется <c>FlipDetectorService</c> каждый раз, когда накопленное вращение
    /// по одной из осей ragdoll-таза достигает полного оборота (360°).
    /// </summary>
    public readonly struct FlipDetectedEvent
    {
        public readonly FlipAxis Axis;
        public readonly float    SpeedDegPerSec;

        public FlipDetectedEvent(FlipAxis axis, float speedDegPerSec)
        {
            Axis           = axis;
            SpeedDegPerSec = speedDegPerSec;
        }
    }

    /// <summary>
    /// Публикуется <c>HighJumpScoreService</c> по результатам попытки: итоговый счёт,
    /// является ли он новым личным рекордом. Подписчики — HUD, метапрогрессия.
    /// </summary>
    public readonly struct StuntsScoredEvent
    {
        public readonly StuntType Type;
        public readonly int       Score;
        public readonly bool      IsNewRecord;

        public StuntsScoredEvent(StuntType type, int score, bool isNewRecord)
        {
            Type        = type;
            Score       = score;
            IsNewRecord = isNewRecord;
        }
    }
}
