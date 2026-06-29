using FlatOutLike.Controllers.Car;
using FlatOutLike.Models;
using FlatOutLike.Services.Combat;
using FlatOutLike.Services.Match;
using UnityEngine;

namespace FlatOutLike.Services.AI
{
    /// <summary>
    /// Реализация выбора цели. Pure C#; зависит только от реестра и сервиса урона.
    /// Все методы работают по снапшоту <c>registry.Cars</c> на момент вызова —
    /// гонок нет, т.к. реестр обновляется только в главном потоке Unity.
    /// </summary>
    public sealed class TargetSelectionService : ITargetSelectionService
    {
        private readonly ICarRegistry   _registry;
        private readonly IDamageService _damage;

        public TargetSelectionService(ICarRegistry registry, IDamageService damage)
        {
            _registry = registry;
            _damage   = damage;
        }

        /// <inheritdoc/>
        public CarController SelectTarget(CarController self, TargetSelectionMode mode)
        {
            switch (mode)
            {
                case TargetSelectionMode.Player:
                    return SelectPlayer(self);

                case TargetSelectionMode.Weakest:
                    return SelectWeakest(self);

                default: // Nearest (mode == 0)
                    return SelectNearest(self);
            }
        }

        // ── Стратегии выбора ─────────────────────────────────────────────────────

        /// <summary>
        /// Возвращает машину игрока, если та жива и не является self.
        /// Иначе откатывается к стратегии Nearest.
        /// </summary>
        private CarController SelectPlayer(CarController self)
        {
            var player = _registry.Player;
            if (player != null && player != self && _damage.GetHp(player.CarId) > 0f)
                return player;

            // Откат: игрок мёртв / недоступен → ищем ближайшего
            return SelectNearest(self);
        }

        /// <summary>
        /// Ближайший живой не-self кандидат по евклидовому расстоянию.
        /// </summary>
        private CarController SelectNearest(CarController self)
        {
            CarController best     = null;
            float         bestDist = float.MaxValue;

            foreach (var car in _registry.Cars)
            {
                if (car == null || car == self) continue;
                if (_damage.GetHp(car.CarId) <= 0f) continue;

                float dist = Vector3.Distance(self.transform.position, car.transform.position);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best     = car;
                }
            }

            return best;
        }

        /// <summary>
        /// Живой не-self кандидат с наименьшим соотношением HP / MaxArmor.
        /// Машина с MaxArmor=0 трактуется как не имеющая брони (делитель = 0.0001),
        /// что делает её кандидатом с очень высоким отношением — фактически игнорируемым.
        /// </summary>
        private CarController SelectWeakest(CarController self)
        {
            CarController best      = null;
            float         bestRatio = float.MaxValue;

            foreach (var car in _registry.Cars)
            {
                if (car == null || car == self) continue;
                float hp = _damage.GetHp(car.CarId);
                if (hp <= 0f) continue;

                float ratio = hp / Mathf.Max(0.0001f, car.MaxArmor);
                if (ratio < bestRatio)
                {
                    bestRatio = ratio;
                    best      = car;
                }
            }

            return best;
        }
    }
}
