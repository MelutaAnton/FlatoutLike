namespace FlatOutLike.Services.Stunts
{
    /// <summary>
    /// Управляет жизненным циклом одной попытки «High Jump»: переключает GameFlow между
    /// Driving/Flight/Scoring по событиям катапультирования и приземления водителя.
    /// </summary>
    public interface IHighJumpSessionService
    {
        /// <summary>Запускает сессию: переводит GameFlow в <c>GameState.StuntsHighJumpDriving</c>.</summary>
        void StartSession();
    }
}
