namespace FlatOutLike.Services.Meta
{
    public interface IPlayerProfile
    {
        int Coins { get; set; }

        /// <summary>Личный рекорд трюка «High Jump» — пиковая ВЫСОТА в метрах
        /// (суть соревнования — высота; счёт с флипами идёт только на монеты).</summary>
        float BestHighJumpHeight { get; set; }
    }
}
