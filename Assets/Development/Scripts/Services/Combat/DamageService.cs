using System;
using System.Collections.Generic;
using FlatOutLike.Core.Events;
using FlatOutLike.Data.Configs.Combat;
using FlatOutLike.Models;

namespace FlatOutLike.Services.Combat
{
    public sealed class DamageService : IDamageService, IDisposable
    {
        private readonly IEventBus _eventBus;
        private readonly DamageConfig _config;

        private readonly Dictionary<CarId, float> _hp = new Dictionary<CarId, float>();
        private readonly Dictionary<CarId, float> _mass = new Dictionary<CarId, float>();
        private readonly HashSet<CarId> _destroyed = new HashSet<CarId>();

        public DamageService(IEventBus eventBus, DamageConfig config)
        {
            _eventBus = eventBus;
            _config = config;
            _eventBus.Subscribe<CarCollisionEvent>(OnCarCollision);
        }

        public void RegisterCar(CarId id, float maxArmor, float mass)
        {
            _hp[id] = maxArmor;
            _mass[id] = mass;
            _destroyed.Remove(id);
        }

        public void UnregisterCar(CarId id)
        {
            _hp.Remove(id);
            _mass.Remove(id);
            _destroyed.Remove(id);
        }

        public float GetHp(CarId id) => _hp.TryGetValue(id, out var hp) ? hp : 0f;

        private void OnCarCollision(CarCollisionEvent evt)
        {
            if (evt.Attacker == evt.Victim) return; // self-collision guard (defence in depth)

            var victim = evt.Victim;

            if (!_hp.TryGetValue(victim, out var currentHp)) return;
            if (_destroyed.Contains(victim)) return;

            float massRatio;
            if (_mass.TryGetValue(evt.Attacker, out var attackerMass)
                && _mass.TryGetValue(victim, out var victimMass)
                && victimMass > 0f)
            {
                massRatio = attackerMass / victimMass;
            }
            else
            {
                massRatio = _config.WallMassRatio;
            }

            float rawSpeed = Math.Max(0f, evt.ImpactSpeed - _config.MinImpactSpeed);
            float damage = rawSpeed * massRatio * _config.DamageScale;

            if (damage <= 0f) return;

            float newHp = Math.Max(0f, currentHp - damage);
            _hp[victim] = newHp;

            _eventBus.Publish(new CarDamagedEvent(victim, damage, newHp));

            if (newHp <= 0f)
            {
                _destroyed.Add(victim);
                _eventBus.Publish(new CarDestroyedEvent(victim, evt.Attacker));
            }
        }

        public void Dispose()
        {
            _eventBus.Unsubscribe<CarCollisionEvent>(OnCarCollision);
        }
    }
}
