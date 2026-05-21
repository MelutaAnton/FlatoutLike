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
        public InputContext CurrentContext { get; set; } = InputContext.UI;

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

            _look = isFlight
                ? new Vector2(
                    UnityEngine.Input.GetAxis("Mouse X"),
                    UnityEngine.Input.GetAxis("Mouse Y"))
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
