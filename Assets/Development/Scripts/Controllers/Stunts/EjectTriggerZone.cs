using FlatOutLike.Controllers.Car;
using FlatOutLike.Core.Commands;
using FlatOutLike.Core.Events;
using FlatOutLike.Data.Configs.Driver;
using FlatOutLike.Models;
using UnityEngine;
using VContainer;

namespace FlatOutLike.Controllers.Stunts
{
    /// <summary>
    /// Триггер-зона на верхушке трамплина. Катапультирует водителя первой влетевшей машины
    /// через <see cref="EjectCommand"/> с причиной <see cref="EjectReason.StuntRamp"/>.
    /// Только Unity API — решение "можно ли катапультировать" остаётся в EjectCommand.CanExecute.
    /// </summary>
    public sealed class EjectTriggerZone : MonoBehaviour
    {
        private DriverEjectConfig _config;
        private IEventBus _eventBus;
        private bool _initialized;

        // Одноразовый триггер: повторный въезд той же (или другой) машины не катапультирует снова.
        private bool _fired;

        [Inject]
        public void Construct(DriverEjectConfig config, IEventBus eventBus)
        {
            _config = config;
            _eventBus = eventBus;
            _initialized = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!_initialized) return;
            if (_fired) return;

            var car = other.GetComponentInParent<CarController>();
            if (car == null) return;

            _fired = true;

            Rigidbody carRb = other.attachedRigidbody;
            if (carRb == null)
                carRb = car.GetComponent<Rigidbody>();

            Vector3 carVelocity = carRb != null ? carRb.linearVelocity : Vector3.zero;

            var ejectCommand = new EjectCommand(car.Driver, car.CarId, EjectReason.StuntRamp,
                _config, _eventBus, car.transform, carVelocity);

            if (ejectCommand.CanExecute())
                ejectCommand.Execute();
        }
    }
}
