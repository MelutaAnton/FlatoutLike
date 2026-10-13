using System;
using FlatOutLike.Data.Configs.Progression;
using FlatOutLike.Services.Hub;
using UnityEngine;

namespace FlatOutLike.Views.UI.Hub
{
    /// <summary>
    /// Presenter круга подтверждения запуска (pure C#, Шаг 11b). Каждый тик View опрашивает
    /// IHubLaunchService: есть активная зона — круг над её центром с текущим прогрессом,
    /// нет — круг скрыт. Hide вызывается только при смене «показан → скрыт», без спама.
    /// </summary>
    public sealed class LaunchConfirmPresenter : IDisposable
    {
        private readonly LaunchConfirmView _view;
        private readonly IHubLaunchService _launch;
        private readonly BuildingCatalogConfig _catalog;

        private bool _shown;

        public LaunchConfirmPresenter(LaunchConfirmView view, IHubLaunchService launch, BuildingCatalogConfig catalog)
        {
            _view = view;
            _launch = launch;
            _catalog = catalog;

            _view.OnTick += HandleTick;
        }

        public void Dispose() => _view.OnTick -= HandleTick;

        private void HandleTick()
        {
            if (_launch.TryGetActiveZone(out var id) && _catalog.TryGetEntry(id, out var entry))
            {
                var position = entry.GetZoneWorldCenter() + Vector3.up * _catalog.LaunchIndicatorHeight;
                _view.ShowAt(position, _launch.Progress01);
                _shown = true;
                return;
            }

            if (!_shown) return;

            _view.Hide();
            _shown = false;
        }
    }
}
