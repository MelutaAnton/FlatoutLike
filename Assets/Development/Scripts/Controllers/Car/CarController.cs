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

        public CarId CarId   => new CarId(_carId);
        public float MaxArmor => _config != null ? _config.MaxArmor : 0f;
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
                ApplyMotor(0f);
                ApplySteer(0f);
                return;
            }
            if (_config == null || _rigidbody == null) return;

            float throttle = _input.Move.y;
            float steerInput = _input.Move.x;
            bool handbrake = _input.Handbrake;

            float forwardSpeed = Vector3.Dot(_rigidbody.linearVelocity, transform.forward);

            float motor = _movement.CalculateMotorTorque(throttle, forwardSpeed, _config);
            float steer = _movement.CalculateSteerAngle(steerInput, forwardSpeed, _config);
            float brake = _movement.CalculateBrakeTorque(handbrake, throttle, forwardSpeed, _config);

            ApplyMotor(motor);
            ApplySteer(steer);
            ApplyBrake(brake);
            ApplyNitro();
        }

        /// <summary>
        /// Применяет импульс нитро если кнопка зажата и бак не пуст.
        /// Расход делегируется в <see cref="INitroService"/> — логика вне контроллера.
        /// </summary>
        private void ApplyNitro()
        {
            if (_input.Nitro && _nitro.TryConsume(CarId, _config.NitroDrainRate * Time.fixedDeltaTime))
                _rigidbody.AddForce(transform.forward * _config.NitroForce);
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

            _eventBus.Publish(new CarCollisionEvent(attacker, CarId, impactSpeed, point));
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
