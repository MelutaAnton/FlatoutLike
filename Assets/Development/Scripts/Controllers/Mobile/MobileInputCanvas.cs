using FlatOutLike.Core.Input;
using UnityEngine;
using VContainer;

namespace FlatOutLike.Controllers.Mobile
{
    /// <summary>
    /// Корневой Canvas мобильного управления (Шаг 9, game-design §12): схема «педали + стик
    /// руления» (стандарт мобильных гонок): газ/тормоз-реверс — кнопки-педали, левый стик
    /// рулит только по X; правый стик — руление в полёте (Stunts). Переключение видимости
    /// групп по текущему InputContext провайдера. Только Unity API — весь ввод пушится
    /// в MobileInputProvider, бизнес-логики (латч, гейтинг) здесь нет, она в провайдере.
    /// </summary>
    public sealed class MobileInputCanvas : MonoBehaviour
    {
        [SerializeField] private VirtualJoystickView _leftStick;
        [SerializeField] private VirtualJoystickView _rightStick;
        [SerializeField] private GameObject _drivingGroup;
        [SerializeField] private GameObject _flightGroup;
        [SerializeField] private HoldButtonView _gasButton;
        [SerializeField] private HoldButtonView _brakeButton;
        [SerializeField] private HoldButtonView _handbrakeButton;
        [SerializeField] private HoldButtonView _nitroButton;
        [SerializeField] private HoldButtonView _ejectButton;
        [SerializeField] private HoldButtonView _boostButton;
        [SerializeField] private HoldButtonView _pauseButton;

        private MobileInputProvider _provider;
        private bool _initialized;

        // Кэш последнего применённого контекста — SetActive дёргаем только при смене,
        // а не каждый Update().
        private InputContext _lastAppliedContext;
        private bool _hasAppliedContext;

        [Inject]
        public void Construct(MobileInputProvider provider)
        {
            _provider = provider;

            // Триггер-кнопки (одноразовые действия) подписываются на Queue*() провайдера.
            // Null-проверки явные — с Unity-объектами `?.`/`??` запрещены (CLAUDE.md, fake-null).
            if (_ejectButton != null) _ejectButton.OnPressed += provider.QueueEject;
            if (_boostButton != null) _boostButton.OnPressed += provider.QueueMidairBoost;
            if (_pauseButton != null) _pauseButton.OnPressed += provider.QueuePause;

            _initialized = true;
        }

        private void Update()
        {
            // OnEnable выполняется раньше Construct() (VContainer, см. CLAUDE.md) — до инъекции
            // выходим сразу, ничего не пушим и не трогаем группы.
            if (!_initialized) return;

            _provider.SetMove(ComposeMove());
            if (_rightStick != null) _provider.SetLook(_rightStick.Direction);
            if (_handbrakeButton != null) _provider.SetHandbrake(_handbrakeButton.IsHeld);
            if (_nitroButton != null) _provider.SetNitro(_nitroButton.IsHeld);

            ApplyContextVisibility(_provider.CurrentContext);
        }

        private void OnDestroy()
        {
            if (!_initialized) return;

            if (_ejectButton != null) _ejectButton.OnPressed -= _provider.QueueEject;
            if (_boostButton != null) _boostButton.OnPressed -= _provider.QueueMidairBoost;
            if (_pauseButton != null) _pauseButton.OnPressed -= _provider.QueuePause;
        }

        /// <summary>
        /// Move по схеме «педали + стик»: X — руление левым стиком, Y — газ (+1) / тормоз-реверс (−1)
        /// с педалей; обе зажаты → 0 (паритет с GetAxisRaw клавиатуры при W+S).
        /// </summary>
        private Vector2 ComposeMove()
        {
            float steer = _leftStick != null ? _leftStick.Direction.x : 0f;

            float throttle = 0f;
            if (_gasButton != null && _gasButton.IsHeld) throttle += 1f;
            if (_brakeButton != null && _brakeButton.IsHeld) throttle -= 1f;

            return new Vector2(steer, throttle);
        }

        private void ApplyContextVisibility(InputContext context)
        {
            if (_hasAppliedContext && context == _lastAppliedContext) return;

            bool drivingVisible = context == InputContext.Driving;
            bool flightVisible = context == InputContext.Flight;

            if (_drivingGroup != null) _drivingGroup.SetActive(drivingVisible);
            if (_flightGroup != null) _flightGroup.SetActive(flightVisible);

            _lastAppliedContext = context;
            _hasAppliedContext = true;
        }
    }
}
