using System;
using FlatOutLike.Controllers.Camera;
using FlatOutLike.Core.Events;
using FlatOutLike.Services.Match;
using UnityEngine;

namespace FlatOutLike.Services.Camera
{
    /// <summary>
    /// Pure C# оркестрация камеры арены. Слушает <see cref="DriverEjectedEvent"/>: если
    /// катапультировался водитель игрока — переключает <see cref="ICameraRig"/> на orbit.
    /// Возврата к chase после орбиты нет — орбита держится до конца матча.
    /// </summary>
    public sealed class CameraService : ICameraService, IDisposable
    {
        private readonly ICameraRig    _rig;
        private readonly ICarRegistry  _registry;
        private readonly IEventBus     _eventBus;

        public CameraService(ICameraRig rig, ICarRegistry registry, IEventBus eventBus)
        {
            _rig      = rig;
            _registry = registry;
            _eventBus = eventBus;

            _eventBus.Subscribe<DriverEjectedEvent>(OnDriverEjected);
        }

        public void FollowPlayerCar(Transform target) => _rig.ActivateChase(target);

        private void OnDriverEjected(DriverEjectedEvent e)
        {
            var player = _registry.Player;
            if (player == null || player.CarId != e.Source) return;
            if (player.Driver == null) return;

            _rig.ActivateOrbit(player.Driver.CameraTarget);
        }

        public void Dispose()
        {
            _eventBus.Unsubscribe<DriverEjectedEvent>(OnDriverEjected);
        }
    }
}
