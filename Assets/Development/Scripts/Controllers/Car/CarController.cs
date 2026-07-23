using FlatOutLike.Controllers.Driver;
using FlatOutLike.Core.Commands;
using FlatOutLike.Core.Events;
using FlatOutLike.Core.Input;
using FlatOutLike.Data.Configs.Car;
using FlatOutLike.Data.Configs.Driver;
using FlatOutLike.Models;
using FlatOutLike.Services.Car;
using FlatOutLike.Services.Combat;
using UnityEngine;
using VContainer;

namespace FlatOutLike.Controllers.Car
{
    public sealed class CarController : MonoBehaviour
    {
        [SerializeField] private int _carId = 0;
        [SerializeField] private Rigidbody _rigidbody;
        [SerializeField] private WheelCollider[] _driveWheels;
        [SerializeField] private WheelCollider[] _steerWheels;
        [SerializeField] private Transform[] _wheelVisuals;
        [SerializeField] private CarStatsConfig _config;
        [SerializeField] private DriverController _driver;

        private IInputProvider _input;
        private ICarMovementService _movement;
        private INitroService _nitro;
        private IEventBus _eventBus;
        private DriverEjectConfig _ejectConfig;
        private bool _initialized;
        private bool _wrecked;
        private float _currentSteerAngle;
        private bool _handbrakeActive;
        private bool _nitroActive; // текущее опубликованное состояние нитро — событие шлётся только при смене

        public CarId CarId   => new CarId(_carId);
        public float MaxArmor => _config != null ? _config.MaxArmor : 0f;

        /// <summary>Текущая скорость машины (м/с). Читается HUD-презентерами (Шаг 8d) через ICarRegistry.Player.</summary>
        public float Speed => _rigidbody != null ? _rigidbody.linearVelocity.magnitude : 0f;

        /// <summary>Машина уничтожена (врек) — читается CarEngineSoundBridge, чтобы заглушить двигатель.</summary>
        public bool IsWrecked => _wrecked;

        /// <summary>Максимальная скорость машины из конфига (м/с) — читается CarEngineSoundBridge для нормировки RPM.</summary>
        public float TopSpeed => _config != null ? _config.TopSpeed : 0f;

        /// <summary>Модуль вертикальной оси газа (0..1), обновляется в FixedUpdate. Читается CarEngineSoundBridge.</summary>
        public float ThrottleInput { get; private set; }

        public System.Collections.Generic.IReadOnlyList<Transform> WheelVisuals => _wheelVisuals;
        public DriverController Driver => _driver;

        /// <summary>
        /// Задаёт идентификатор и конфиг машины до инъекции VContainer.
        /// Вызывается фабрикой до InjectGameObject, чтобы Construct читал актуальные значения,
        /// а не дефолты из prefab (carId=0, config=null).
        /// </summary>
        public void Configure(int carId, CarStatsConfig stats)
        {
            _carId  = carId;
            _config = stats;

            // Страховка на случай переиспользования объекта (пул/респавн) без прохождения
            // врека: не тащим «активное нитро» прошлой жизни — иначе трейлы мигнут при спавне.
            // Без публикации события: Configure вызывается до InjectGameObject, шины ещё нет.
            _nitroActive = false;
        }

        /// <summary>
        /// Заменяет активный источник ввода в рантайме.
        /// Используется <c>EnemyCarController</c>, чтобы подменить <c>AIInputProvider</c>
        /// после того как VContainer заинжектил дефолтный <c>KeyboardInputProvider</c>.
        /// </summary>
        public void SetInputSource(IInputProvider provider)
        {
            if (provider != null)
                _input = provider;
        }

        [Inject]
        public void Construct(IInputProvider input, ICarMovementService movement,
            IDamageService damage, INitroService nitro, IEventBus eventBus, DriverEjectConfig ejectConfig = null)
        {
            // Не зависит от порядка: AI-обёртка (EnemyCarController) могла уже
            // установить AIInputProvider через SetInputSource до запуска этого Construct.
            // Никогда не перезаписываем существующий источник keyboard-синглтоном.
            _input ??= input;
            _movement = movement;
            _nitro    = nitro;
            _eventBus = eventBus;
            _ejectConfig = ejectConfig; // scope всегда регистрирует инстанс (fallback в тест-сценах); null-guard в CanExecute остаётся
            _eventBus.Subscribe<CarDestroyedEvent>(OnAnyCarDestroyed);

            if (_rigidbody != null && _config != null)
            {
                _rigidbody.mass = _config.Mass;
                _rigidbody.centerOfMass = _config.CenterOfMassOffset;
                // Камера читает позицию машины каждый render-кадр, а физика двигает её
                // на 50 Гц — без интерполяции цель «шагает» и камера дрожит на скорости.
                _rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            }

            if (_config != null)
            {
                damage.RegisterCar(CarId, _config.MaxArmor, _config.Mass);
                nitro.RegisterCar(CarId, _config.NitroCapacity);
            }

            ApplyWheelFriction();

            _initialized = true;
        }

        private void ApplyWheelFriction()
        {
            if (_config == null) return;
            ApplyFrictionTo(_driveWheels);
            ApplyFrictionTo(_steerWheels);
        }

        private void ApplyFrictionTo(WheelCollider[] wheels)
        {
            if (wheels == null) return;
            foreach (var wc in wheels)
            {
                if (wc == null) continue;

                var fwd = wc.forwardFriction;
                fwd.stiffness = _config.WheelFriction;
                wc.forwardFriction = fwd;

                var side = wc.sidewaysFriction;
                side.stiffness = _config.WheelFriction;
                wc.sidewaysFriction = side;
            }
        }

        private void FixedUpdate()
        {
            if (!_initialized) return;
            if (_wrecked)
            {
                // Машина уничтожена: газ/руль/нитро больше не применяются, машина катится по инерции.
                // Ручник мог быть зажат в момент гибели — вернуть заднее сцепление, чтобы обломок
                // не скользил хвостом с дрифтовым стиффнессом до конца матча.
                ThrottleInput = 0f; // двигатель заглох — CarEngineSoundBridge читает это как gasPedalPressing=false
                UpdateHandbrakeFriction(false);
                ApplyMotor(0f);
                ApplySteer(0f);
                return;
            }
            if (_config == null || _rigidbody == null) return;

            float throttle = _input.Move.y;
            float steerInput = _input.Move.x;
            bool handbrake = _input.Handbrake;

            ThrottleInput = Mathf.Clamp01(Mathf.Abs(throttle));

            float forwardSpeed = Vector3.Dot(_rigidbody.linearVelocity, transform.forward);

            float motor = _movement.CalculateMotorTorque(throttle, forwardSpeed, _config);
            // Сглаженный руль: _currentSteerAngle — состояние конкретной машины, сервис его не хранит.
            _currentSteerAngle = _movement.CalculateSmoothedSteerAngle(_currentSteerAngle, steerInput, forwardSpeed, Time.fixedDeltaTime, _config);
            float brake = _movement.CalculateBrakeTorque(handbrake, throttle, forwardSpeed, _config);

            UpdateHandbrakeFriction(handbrake);

            ApplyMotor(motor);
            ApplySteer(_currentSteerAngle);
            ApplyBrake(brake);
            ApplyNitro();
        }

        /// <summary>
        /// Ручник-дрифт: пока зажат, боковое сцепление задних колёс (привод, <see cref="_driveWheels"/>)
        /// снижается множителем HandbrakeGripFactor — машина срывается в занос вместо блокировки колёс.
        /// Стиффнесс переключается только при смене состояния (не каждый тик — WheelFrictionCurve копируется по значению).
        /// </summary>
        private void UpdateHandbrakeFriction(bool handbrake)
        {
            if (handbrake == _handbrakeActive) return;
            _handbrakeActive = handbrake;

            float stiffness = handbrake ? _config.WheelFriction * _config.HandbrakeGripFactor : _config.WheelFriction;
            ApplySidewaysStiffness(_driveWheels, stiffness);
        }

        private void ApplySidewaysStiffness(WheelCollider[] wheels, float stiffness)
        {
            if (wheels == null) return;
            foreach (var wc in wheels)
            {
                if (wc == null) continue;
                var side = wc.sidewaysFriction;
                side.stiffness = stiffness;
                wc.sidewaysFriction = side;
            }
        }

        /// <summary>
        /// Применяет импульс нитро если кнопка зажата и бак не пуст.
        /// Расход делегируется в <see cref="INitroService"/> — логика вне контроллера.
        /// При смене состояния публикует <see cref="NitroStateChangedEvent"/> — на него подписаны
        /// NitroTrailsController (визуальные трейлы) и NitroCameraEffectController (FOV/блюр игрока).
        /// </summary>
        private void ApplyNitro()
        {
            bool active = _input.Nitro && _nitro.TryConsume(CarId, _config.NitroDrainRate * Time.fixedDeltaTime);
            if (active)
                _rigidbody.AddForce(transform.forward * _config.NitroForce);

            SetNitroActive(active);
        }

        /// <summary>
        /// Публикует событие только при реальной смене состояния (не каждый физтик).
        /// </summary>
        private void SetNitroActive(bool active)
        {
            if (active == _nitroActive) return;
            _nitroActive = active;
            _eventBus.Publish(new NitroStateChangedEvent(CarId, _nitroActive));
        }

        private void ApplyMotor(float torque)
        {
            if (_driveWheels == null) return;
            foreach (var wc in _driveWheels)
                if (wc != null) wc.motorTorque = torque;
        }

        private void ApplySteer(float angle)
        {
            if (_steerWheels == null) return;
            foreach (var wc in _steerWheels)
                if (wc != null) wc.steerAngle = angle;
        }

        private void ApplyBrake(float torque)
        {
            if (_driveWheels == null) return;
            foreach (var wc in _driveWheels)
                if (wc != null) wc.brakeTorque = torque;
        }

        private void Update()
        {
            if (!_initialized) return;
            if (_wheelVisuals == null || _wheelVisuals.Length == 0) return;

            int visualIdx = 0;
            SyncVisuals(_driveWheels, ref visualIdx);
            SyncVisuals(_steerWheels, ref visualIdx);
        }

        private void SyncVisuals(WheelCollider[] wheels, ref int visualIdx)
        {
            if (wheels == null) return;
            foreach (var wc in wheels)
            {
                if (visualIdx >= _wheelVisuals.Length) return;
                var visual = _wheelVisuals[visualIdx];
                visualIdx++;
                if (wc == null || visual == null) continue;
                wc.GetWorldPose(out Vector3 pos, out Quaternion rot);
                visual.SetPositionAndRotation(pos, rot);
            }
        }

        private void OnCollisionEnter(Collision c)
        {
            if (!_initialized || _eventBus == null) return;

            float impactSpeed = c.relativeVelocity.magnitude;
            var otherCar = c.gameObject.GetComponentInParent<CarController>();
            if (otherCar == this) return; // игнорируем собственные оторванные детали, отскочившие назад
            CarId attacker = otherCar != null ? otherCar.CarId : CarId.None;
            Vector3 point = c.contactCount > 0 ? c.GetContact(0).point : transform.position;

            _eventBus.Publish(new CarCollisionEvent(attacker, CarId, impactSpeed, point, c.gameObject.layer));
        }

        /// <summary>
        /// Помечает машину уничтоженной, если событие относится к ней самой.
        /// Дальше FixedUpdate перестаёт применять газ/руль/нитро — машина катится по инерции.
        /// Заодно катапультирует водителя (Шаг 6) — команда вызывается из контроллера,
        /// а не из сервиса, т.к. решение "катапультировать" привязано к Unity-событию машины.
        /// </summary>
        private void OnAnyCarDestroyed(CarDestroyedEvent e)
        {
            if (e.Target != CarId) return;
            if (_wrecked) return; // повторное событие по той же машине — eject уже отработал

            _wrecked = true;

            // FixedUpdate/ApplyNitro больше не выполнится после врека — если нитро было активно,
            // состояние зависнет навсегда (трейлы/FOV-кик не выключатся). Сбрасываем принудительно.
            SetNitroActive(false);

            // transform = корень машины: водитель игнорирует столкновения с её коллайдерами
            // при вылете «через лобовое» (иначе включение ragdoll-коллайдеров в салоне взрывает физику).
            // Скорость машины в момент гибели — водитель наследует её долю (полишинг вылета).
            Vector3 carVelocity = _rigidbody != null ? _rigidbody.linearVelocity : Vector3.zero;
            var ejectCommand = new EjectCommand(_driver, CarId, EjectReason.CarDestroyed, _ejectConfig, _eventBus, transform, carVelocity);
            // Стекло разбивает WindshieldController сам — по DriverEjectedEvent своей машины
            // (единый путь для Derby и трамплина High Jump).
            if (ejectCommand.CanExecute())
                ejectCommand.Execute();
        }

        private void OnDestroy()
        {
            if (_eventBus != null)
                _eventBus.Unsubscribe<CarDestroyedEvent>(OnAnyCarDestroyed);
        }
    }
}
