using FlatOutLike.Core.Input;
using FlatOutLike.Services.Driver;
using UnityEngine;
using VContainer;

namespace FlatOutLike.Controllers.Driver
{
    /// <summary>
    /// Вешается на корень префаба водителя рядом с <see cref="DriverController"/>.
    /// Только Unity API: читает ввод и прикладывает силы к тазу ragdoll в полёте,
    /// бизнес-решения (сколько ускорения, сколько бустов) — в <see cref="IDriverFlightService"/>.
    /// Безвреден в Derby-сцене: вне Flight-контекста Look/Boost нулевые (см. KeyboardInputProvider),
    /// а без запущенной HighJump-сессии NotifyLanded никогда не вызывается — DriverLandedEvent не публикуется.
    /// </summary>
    public sealed class DriverFlightController : MonoBehaviour
    {
        private IInputProvider _input;
        private IDriverFlightService _flight;
        private IFlipDetectorService _flipDetector;
        private DriverController _driver;

        private bool _initialized;
        // Одноразовый флаг: полёт начинается на первом кадре после eject.
        // Сбрасывается только пересозданием сцены — одна попытка = одна сцена.
        private bool _flightStarted;

        private void Awake()
        {
            _driver = GetComponent<DriverController>();
        }

        [Inject]
        public void Construct(IInputProvider input, IDriverFlightService flight, IFlipDetectorService flipDetector)
        {
            _input = input;
            _flight = flight;
            _flipDetector = flipDetector;
            _initialized = true;
        }

        private void FixedUpdate()
        {
            if (!_initialized) return;
            if (_driver == null) return;
            if (!_driver.IsEjected) return;

            Rigidbody pelvis = _driver.Pelvis;
            if (pelvis == null) return;

            if (!_flightStarted)
            {
                _flightStarted = true;
                _flight.BeginFlight(pelvis.position);
            }

            // После приземления (и в Derby после успокоения ragdoll) делать нечего —
            // не гоняем AddForce(0)/Feed каждый кадр на каждом выброшенном водителе.
            if (!_flight.IsInFlight) return;

            // Силы прикладываем РАВНОМЕРНО ко всем костям (Acceleration/VelocityChange не зависят
            // от массы): ускорение только таза растягивает CharacterJoint'ы → «иглы» на меше.
            Vector3 steer = _flight.CalculateSteerAcceleration(_input.Look, pelvis.linearVelocity);
            ApplyToAllBones(steer, ForceMode.Acceleration);

            if (_input.MidairBoostPressed && _flight.TryConsumeBoost())
            {
                Vector3 velocity = pelvis.linearVelocity;
                Vector3 direction = velocity.sqrMagnitude > 0.0001f ? velocity.normalized : Vector3.up;
                ApplyToAllBones(direction * _flight.BoostImpulse, ForceMode.VelocityChange);
            }

            _flight.Track(pelvis.position, Time.fixedDeltaTime);
            _flipDetector.Feed(pelvis.rotation, Time.fixedDeltaTime);
        }

        private void ApplyToAllBones(Vector3 force, ForceMode mode)
        {
            var bones = _driver.Bones;
            if (bones == null) return;

            foreach (var bone in bones)
            {
                if (bone != null)
                    bone.AddForce(force, mode);
            }
        }
    }
}
