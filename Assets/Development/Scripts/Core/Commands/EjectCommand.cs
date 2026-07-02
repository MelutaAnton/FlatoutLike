using FlatOutLike.Controllers.Driver;
using FlatOutLike.Core.Events;
using FlatOutLike.Data.Configs.Driver;
using FlatOutLike.Models;
using UnityEngine;

namespace FlatOutLike.Core.Commands
{
    /// <summary>
    /// Катапультирует водителя из машины: считает импульс из <see cref="DriverEjectConfig"/>,
    /// вызывает <see cref="DriverController.Eject"/> и публикует <see cref="DriverEjectedEvent"/>.
    /// </summary>
    public sealed class EjectCommand : ICommand
    {
        private readonly DriverController _driver;
        private readonly CarId _source;
        private readonly EjectReason _reason;
        private readonly DriverEjectConfig _config;
        private readonly IEventBus _eventBus;
        private readonly Transform _carRoot;
        private readonly Vector3 _carVelocity;

        public EjectCommand(DriverController driver, CarId source, EjectReason reason,
            DriverEjectConfig config, IEventBus eventBus, Transform carRoot, Vector3 carVelocity)
        {
            _driver      = driver;
            _source      = source;
            _reason      = reason;
            _config      = config;
            _eventBus    = eventBus;
            _carRoot     = carRoot;
            _carVelocity = carVelocity;
        }

        public bool CanExecute() => _driver != null && _config != null && !_driver.IsEjected;

        public void Execute()
        {
            // Базовый импульс вперёд-вверх + ослабленное наследование скорости машины
            // на момент вылета: водитель продолжает лететь по ходу движения.
            Vector3 velocity = _driver.transform.forward * _config.ForwardImpulse
                              + Vector3.up * _config.UpImpulse
                              + _carVelocity * _config.CarVelocityInheritance;

            _driver.Eject(velocity, _carRoot);
            _eventBus.Publish(new DriverEjectedEvent(_source, velocity, _reason));
        }
    }
}
