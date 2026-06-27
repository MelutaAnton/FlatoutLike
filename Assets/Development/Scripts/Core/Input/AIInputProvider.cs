using UnityEngine;

namespace FlatOutLike.Core.Input
{
    /// <summary>
    /// Записываемый провайдер ввода для машин под управлением AI.
    /// Один экземпляр на врага — НЕ регистрируется в DI как singleton.
    /// Значения пушатся снаружи из EnemyCarController каждый FixedUpdate;
    /// <see cref="Tick"/> намеренно пустой (инверсия poll vs push).
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
        /// Записывает ввод для следующего физического кадра для соседнего <c>CarController</c>.
        /// Вызывается из <c>EnemyCarController.FixedUpdate</c> после AI-тика.
        /// </summary>
        public void Set(Vector2 move, bool handbrake, bool nitro)
        {
            _move      = move;
            _handbrake = handbrake;
            _nitro     = nitro;
        }

        // Polling не нужен — значения пушатся снаружи.
        public void Tick() { }
    }
}
