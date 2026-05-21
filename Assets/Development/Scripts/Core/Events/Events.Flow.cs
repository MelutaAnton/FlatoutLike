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
}
