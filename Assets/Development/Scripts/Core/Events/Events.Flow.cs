using FlatOutLike.Models;

namespace FlatOutLike.Core.Events
{
    public readonly struct GameStateChangedEvent
    {
        public readonly GameState From;
        public readonly GameState To;

        public GameStateChangedEvent(GameState from, GameState to)
        {
            From = from;
            To = to;
        }
    }

    public readonly struct GamePausedEvent { }

    public readonly struct GameResumedEvent { }

    /// <summary>
    /// Экран загрузки полностью закрыл старую сцену и началась асинхронная загрузка новой
    /// (Шаг 11d). Старую сцену можно замораживать (timeScale, звук) — игрок её уже не видит;
    /// новая сцена сбрасывает заморозку сама при сборке своего scope.
    /// </summary>
    public readonly struct SceneTransitionCoveredEvent { }
}
