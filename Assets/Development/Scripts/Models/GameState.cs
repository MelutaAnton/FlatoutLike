namespace FlatOutLike.Models
{
    /// <summary>
    /// Состояния игрового FSM (game-design §9). Значения ЯВНЫЕ: enum сериализуется числом
    /// (AudioConfig — маппинг GameState→трек), поэтому существующие числа не перенумеровывать.
    /// Допустимо только добавление новых значений с новыми, ещё не занятыми числами
    /// (2 — занято удалённым состоянием хаба трюков, не переиспользовать).
    /// </summary>
    public enum GameState
    {
        BaseHub = 0,
        GarageMeta = 1,
        DerbyMatch = 3,
        StuntsHighJumpDriving = 4,
        StuntsHighJumpFlight = 5,
        StuntsHighJumpScoring = 6,
        MatchEnd = 7,
        Paused = 8
    }
}
