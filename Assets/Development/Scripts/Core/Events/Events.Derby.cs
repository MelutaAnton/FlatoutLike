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

        // Индекс физического слоя объекта, о который ударилась машина-жертва
        // (c.gameObject.layer из CarController.OnCollisionEnter). Нужен DamageService,
        // чтобы отфильтровать удары об окружение на слоях из DamageConfig.DamageIgnoreLayers
        // (например пол арены). 0 (Default) — источник слоя не уточнён; не влияет на 7
        // существующих вызовов конструктора в тестах, у которых параметр не указан.
        public readonly int OtherLayer;

        public CarCollisionEvent(CarId attacker, CarId victim, float impactSpeed, Vector3 point, int otherLayer = 0)
        {
            Attacker = attacker;
            Victim = victim;
            ImpactSpeed = impactSpeed;
            Point = point;
            OtherLayer = otherLayer;
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

    /// <summary>
    /// Публикуется <c>DerbyMatchService</c> по завершении матча (остался один живой участник).
    /// Winner = <see cref="CarId.None"/>, если единственного выжившего определить не удалось.
    /// </summary>
    public readonly struct DerbyMatchEndedEvent
    {
        public readonly CarId Winner;
        public readonly int   PlayerKills;
        public readonly float Duration;

        public DerbyMatchEndedEvent(CarId winner, int playerKills, float duration)
        {
            Winner      = winner;
            PlayerKills = playerKills;
            Duration    = duration;
        }
    }

    /// <summary>
    /// Публикуется <c>CarController</c> при смене состояния нитро (нажата/отпущена, либо
    /// принудительно сброшена при вреке машины). Подписчики: <c>NitroTrailsController</c>
    /// (визуальные трейлы у любой машины), <c>NitroCameraEffectController</c>
    /// (FOV-кик + радиальный блюр — только для машины игрока).
    /// </summary>
    public readonly struct NitroStateChangedEvent
    {
        public readonly CarId CarId;
        public readonly bool  IsActive;

        public NitroStateChangedEvent(CarId carId, bool isActive)
        {
            CarId    = carId;
            IsActive = isActive;
        }
    }
}
