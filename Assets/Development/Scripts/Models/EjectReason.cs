namespace FlatOutLike.Models
{
    /// <summary>
    /// Причина катапультирования водителя из машины.
    /// CarDestroyed — машина уничтожена (Derby, Шаг 6); StuntRamp — трамплин (зарезервировано для Шага 7).
    /// </summary>
    public enum EjectReason
    {
        CarDestroyed,
        StuntRamp
    }
}
