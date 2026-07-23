namespace FlatOutLike.Models
{
    /// <summary>
    /// Идентификаторы музыкальных треков проекта. Соответствие GameState → трек
    /// задаётся в <c>AudioConfig.StateMusicEntry</c>.
    /// </summary>
    public enum MusicTrackId
    {
        None = 0,
        Menu,
        Derby,
        HighJump
    }
}
