using UnityEngine;

namespace FlatOutLike.Core.Input
{
    /// <summary>
    /// Pure C# input provider для мобильного UI (виртуальные стики + кнопки-триггеры).
    /// UI (<see cref="Controllers.Mobile.MobileInputCanvas"/>) пушит состояние публичными
    /// Set*/Queue*-методами каждый кадр; Tick() применяет контекст-гейтинг и латч-семантику
    /// одноразовых триггеров (аналог GetKeyDown у KeyboardInputProvider), см. CLAUDE.md
    /// про буферизацию одноразового ввода. Tick() должен вызываться ровно раз за кадр.
    /// </summary>
    public sealed class MobileInputProvider : IInputProvider
    {
        // Default = Driving (тот же резон, что у KeyboardInputProvider): пока GameFlowService
        // не опубликовал реальный GameStateChangedEvent, тест-сцена вождения должна работать.
        public InputContext CurrentContext { get; set; } = InputContext.Driving;

        // ── Сырые значения, пушатся UI каждый кадр ──────────────────────────────
        private Vector2 _rawMove;
        private Vector2 _rawLook;
        private bool _rawHandbrake;
        private bool _rawNitro;

        // ── Pending-флаги одноразовых триггеров, ставятся Queue*() ──────────────
        private bool _ejectQueued;
        private bool _midairBoostQueued;
        private bool _pauseQueued;

        // ── Кэшированные значения, читаются геттерами, пишутся только в Tick() ──
        private Vector2 _move;
        private Vector2 _look;
        private bool _handbrake;
        private bool _nitro;
        private bool _ejectPressed;
        private bool _midairBoostPressed;
        private bool _pausePressed;

        public Vector2 Move => _move;
        public Vector2 Look => _look;
        public bool Handbrake => _handbrake;
        public bool Nitro => _nitro;
        public bool EjectPressed => _ejectPressed;
        public bool MidairBoostPressed => _midairBoostPressed;
        public bool PausePressed => _pausePressed;

        // ── Continuous-ввод: UI пушит сырое состояние каждый кадр ────────────────

        public void SetMove(Vector2 move) => _rawMove = move;

        public void SetLook(Vector2 look) => _rawLook = look;

        public void SetHandbrake(bool held) => _rawHandbrake = held;

        public void SetNitro(bool held) => _rawNitro = held;

        // ── Одноразовые триггеры: UI ставит pending-флаг, Tick() выдаёт один кадр ──

        public void QueueEject() => _ejectQueued = true;

        public void QueueMidairBoost() => _midairBoostQueued = true;

        public void QueuePause() => _pauseQueued = true;

        public void Tick()
        {
            bool isDriving = CurrentContext == InputContext.Driving;
            bool isFlight = CurrentContext == InputContext.Flight;
            // Hub (BaseHub): персонаж бегает — нужен только Move; машинные действия гасятся.
            bool isHub = CurrentContext == InputContext.Hub;

            _move = isDriving || isHub ? _rawMove : Vector2.zero;
            _look = isFlight ? _rawLook : Vector2.zero;
            _handbrake = isDriving && _rawHandbrake;
            _nitro = isDriving && _rawNitro;

            // Латч на один кадр: значение выставляется в true, если было запрошено
            // с прошлого Tick(), и сразу сбрасывается — семантика GetKeyDown.
            _ejectPressed = isDriving && _ejectQueued;
            _ejectQueued = false;

            _midairBoostPressed = isFlight && _midairBoostQueued;
            _midairBoostQueued = false;

            // PausePressed — контекст-независим, как и у KeyboardInputProvider.
            _pausePressed = _pauseQueued;
            _pauseQueued = false;
        }
    }
}
