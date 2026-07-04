using UnityEngine;

namespace FlatOutLike.Core.Input
{
    /// <summary>
    /// Pure C# input provider using legacy UnityEngine.Input (Active Input Handling = Both).
    /// All Input.* calls happen only in Tick(), which must be called once per frame.
    /// Getters read cached values — safe to call multiple times per frame.
    /// </summary>
    public sealed class KeyboardInputProvider : IInputProvider
    {
        // Default = Driving (first enum value). Until GameFlowService publishes a
        // real GameStateChangedEvent, the test driving scene needs working input.
        // Will be overwritten by InputRouter on the first FSM transition.
        public InputContext CurrentContext { get; set; } = InputContext.Driving;

        // Cached values — written in Tick(), read by getters
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

        public void Tick()
        {
            bool isDriving = CurrentContext == InputContext.Driving;
            bool isFlight = CurrentContext == InputContext.Flight;

            _move = isDriving
                ? new Vector2(
                    UnityEngine.Input.GetAxisRaw("Horizontal"),
                    UnityEngine.Input.GetAxisRaw("Vertical"))
                : Vector2.zero;

            // WASD/стрелки вместо мыши: дельта мыши (GetAxis("Mouse X"/"Y")) даёт микрозначения
            // и только при активном движении мыши — коррекция в полёте физически не ощущалась.
            // GetAxisRaw по Horizontal/Vertical даёт полноценный вектор -1..1 постоянно, пока
            // зажата клавиша — руление в воздухе ощущается как в FlatOut 2.
            _look = isFlight
                ? new Vector2(
                    UnityEngine.Input.GetAxisRaw("Horizontal"),
                    UnityEngine.Input.GetAxisRaw("Vertical"))
                : Vector2.zero;

            _handbrake = isDriving && UnityEngine.Input.GetKey(KeyCode.Space);
            _nitro = isDriving && UnityEngine.Input.GetKey(KeyCode.LeftShift);
            _ejectPressed = isDriving && UnityEngine.Input.GetKeyDown(KeyCode.E);
            _midairBoostPressed = isFlight && UnityEngine.Input.GetKeyDown(KeyCode.Mouse0);

            // PausePressed is context-independent
            _pausePressed = UnityEngine.Input.GetKeyDown(KeyCode.Escape);
        }
    }
}
