using System;
using FlatOutLike.Controllers.Camera;
using FlatOutLike.Core.Events;
using FlatOutLike.Data.Configs.Camera;
using FlatOutLike.Services.Match;

namespace FlatOutLike.Services.Camera
{
    /// <summary>
    /// Pure C# оркестрация тряски камеры (Cinemachine Impulse) от столкновений машин в дерби.
    /// Слушает <see cref="CarCollisionEvent"/>: если в столкновении участвует машина игрока
    /// (как атакующий или как жертва) и скорость удара выше порога — инициирует тряску
    /// через <see cref="ICameraShaker"/> с силой, пропорциональной избыточной скорости удара.
    /// </summary>
    public sealed class CameraShakeService : ICameraShakeService, IDisposable
    {
        private readonly IEventBus _eventBus;
        private readonly ICarRegistry _registry;
        private readonly ICameraShaker _shaker;
        private readonly CameraShakeConfig _config;

        public CameraShakeService(IEventBus eventBus, ICarRegistry registry, ICameraShaker shaker, CameraShakeConfig config)
        {
            _eventBus = eventBus;
            _registry = registry;
            _shaker = shaker;
            _config = config;

            _eventBus.Subscribe<CarCollisionEvent>(OnCarCollision);
        }

        private void OnCarCollision(CarCollisionEvent evt)
        {
            var player = _registry.Player;
            if (player == null) return;

            // Тряска релевантна, только если игрок участвует в столкновении — как атакующий
            // (протаранил кого-то) или как жертва (в него въехали/он влетел в стену).
            bool involvesPlayer = player.CarId == evt.Attacker || player.CarId == evt.Victim;
            if (!involvesPlayer) return;

            if (evt.ImpactSpeed < _config.MinImpactSpeed) return;

            float excessSpeed = evt.ImpactSpeed - _config.MinImpactSpeed;
            float force = Math.Min(_config.MaxForce, excessSpeed * _config.SpeedToForce);

            // Ровно на пороге (или при нулевом SpeedToForce) сила = 0 — тряски нет,
            // лишний вызов шейкера не делаем.
            if (force <= 0f) return;

            _shaker.Shake(evt.Point, force);
        }

        public void Dispose()
        {
            _eventBus.Unsubscribe<CarCollisionEvent>(OnCarCollision);
        }
    }
}
