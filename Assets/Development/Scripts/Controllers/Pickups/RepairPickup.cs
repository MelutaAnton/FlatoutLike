using System;
using FlatOutLike.Controllers.Car;
using FlatOutLike.Core.Events;
using FlatOutLike.Data.Configs.Pickups;
using FlatOutLike.Models;
using FlatOutLike.Services.Combat;
using UnityEngine;
using VContainer;

namespace FlatOutLike.Controllers.Pickups
{
    /// <summary>
    /// Пикап ремонта: при подборе восстанавливает долю MaxArmor машины.
    /// Trigg-коллайдер с кинематическим Rigidbody создаётся editor-скриптом на prefab'е.
    /// </summary>
    public sealed class RepairPickup : MonoBehaviour, IPickup
    {
        private IDamageService _damage;
        private IEventBus      _bus;
        private PickupConfig   _config;

        private bool _initialized;
        private bool _collected;
        private Action<IPickup> _onCollected;

        public PickupType Type => PickupType.Repair;

        [Inject]
        public void Construct(IDamageService damage, IEventBus bus, PickupConfig config)
        {
            _damage = damage;
            _bus    = bus;
            _config = config;
            _initialized = true;
        }

        /// <inheritdoc/>
        public void Activate(Action<IPickup> onCollected)
        {
            _onCollected = onCollected;
            _collected   = false;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!_initialized || _collected) return;

            var car = other.GetComponentInParent<CarController>();
            if (car == null) return;

            // Repair принимает долю MaxArmor; расчёт абсолюта — внутри DamageService.
            _damage.Repair(car.CarId, _config.RepairFraction);
            _bus.Publish(new PickupCollectedEvent(car.CarId, Type));

            _collected = true;
            _onCollected?.Invoke(this);
        }
    }
}
