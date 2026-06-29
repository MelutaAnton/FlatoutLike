using FlatOutLike.Models;
using UnityEngine;

namespace FlatOutLike.Core.Events
{
    public readonly struct CarCollisionEvent
    {
        public readonly CarId Attacker;
        public readonly CarId Victim;
        public readonly float ImpactSpeed;
        public readonly Vector3 Point;

        public CarCollisionEvent(CarId attacker, CarId victim, float impactSpeed, Vector3 point)
        {
            Attacker = attacker;
            Victim = victim;
            ImpactSpeed = impactSpeed;
            Point = point;
        }
    }

    public readonly struct CarDamagedEvent
    {
        public readonly CarId Target;
        public readonly float Damage;
        public readonly float RemainingHp;

        public CarDamagedEvent(CarId target, float damage, float remainingHp)
        {
            Target = target;
            Damage = damage;
            RemainingHp = remainingHp;
        }
    }

    public readonly struct CarDestroyedEvent
    {
        public readonly CarId Target;
        public readonly CarId Killer;

        public CarDestroyedEvent(CarId target, CarId killer)
        {
            Target = target;
            Killer = killer;
        }
    }

    /// <summary>
    /// Публикуется пикапом при подборе машиной.
    /// Подписчики (HUD, статистика матча) узнают, кто и что подобрал.
    /// </summary>
    public readonly struct PickupCollectedEvent
    {
        public readonly CarId      Collector;
        public readonly PickupType Type;

        public PickupCollectedEvent(CarId collector, PickupType type)
        {
            Collector = collector;
            Type      = type;
        }
    }
}
