using UnityEngine;

namespace FlatOutLike.Core.Input
{
    public interface IInputProvider
    {
        InputContext CurrentContext { get; set; }

        Vector2 Move { get; }
        Vector2 Look { get; }
        bool Handbrake { get; }
        bool Nitro { get; }
        bool EjectPressed { get; }
        bool MidairBoostPressed { get; }
        bool PausePressed { get; }

        void Tick();
    }
}
