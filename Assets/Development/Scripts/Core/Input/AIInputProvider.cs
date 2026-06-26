using UnityEngine;

namespace FlatOutLike.Core.Input
{
    /// <summary>
    /// Writable input provider for AI-controlled cars.
    /// One instance per enemy — NOT registered in DI as a singleton.
    /// Values are pushed externally by EnemyCarController each FixedUpdate;
    /// <see cref="Tick"/> is intentionally a no-op (poll vs push inversion).
    /// </summary>
    public sealed class AIInputProvider : IInputProvider
    {
        public InputContext CurrentContext { get; set; } = InputContext.Driving;

        private Vector2 _move;
        private bool    _handbrake;
        private bool    _nitro;

        public Vector2 Move               => _move;
        public Vector2 Look               => Vector2.zero;
        public bool    Handbrake          => _handbrake;
        public bool    Nitro              => _nitro;
        public bool    EjectPressed       => false;
        public bool    MidairBoostPressed => false;
        public bool    PausePressed       => false;

        /// <summary>
        /// Writes the next physics-frame inputs for the sibling <c>CarController</c>.
        /// Called by <c>EnemyCarController.FixedUpdate</c> after the AI tick.
        /// </summary>
        public void Set(Vector2 move, bool handbrake, bool nitro)
        {
            _move      = move;
            _handbrake = handbrake;
            _nitro     = nitro;
        }

        // No polling needed — values are pushed externally.
        public void Tick() { }
    }
}
