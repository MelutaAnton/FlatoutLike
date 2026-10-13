using System;
using System.Collections.Generic;
using FlatOutLike.Core.Events;
using FlatOutLike.Models;
using FlatOutLike.Services.Progression;

namespace FlatOutLike.Views.UI.Hub
{
    /// <summary>
    /// Presenter ценников зданий BaseHub (pure C#, шаг 12a). Начальное состояние каждой панели —
    /// из IBuildProgressService; дальше обновление по событиям стройки: пачка вложения → «X LEFT»,
    /// завершённый этап / здание → следующий этап и растущий rate. Прогресс виден только у
    /// строящегося здания (включено, с этапами, не построено).
    /// </summary>
    public sealed class BuildingPanelPresenter : IDisposable
    {
        private readonly IReadOnlyList<BuildingPanelView> _views;
        private readonly IBuildProgressService _build;
        private readonly IEventBus _eventBus;

        public BuildingPanelPresenter(
            BuildingPanelViews views,
            IBuildProgressService build,
            IEventBus eventBus)
        {
            _views = views.Items;
            _build = build;
            _eventBus = eventBus;

            for (int i = 0; i < _views.Count; i++)
                Refresh(_views[i]);

            _eventBus.Subscribe<BuildingInvestProgressEvent>(OnInvestProgress);
            _eventBus.Subscribe<BuildingStageInvestedEvent>(OnStageInvested);
            _eventBus.Subscribe<BuildingCompletedEvent>(OnCompleted);
        }

        public void Dispose()
        {
            _eventBus.Unsubscribe<BuildingInvestProgressEvent>(OnInvestProgress);
            _eventBus.Unsubscribe<BuildingStageInvestedEvent>(OnStageInvested);
            _eventBus.Unsubscribe<BuildingCompletedEvent>(OnCompleted);
        }

        private void OnInvestProgress(BuildingInvestProgressEvent e)
        {
            for (int i = 0; i < _views.Count; i++)
            {
                var view = _views[i];
                if (view == null || view.BuildingId != e.Id) continue;

                view.SetProgress(e.InvestedInStage, e.StageCost);
            }
        }

        private void OnStageInvested(BuildingStageInvestedEvent e) => RefreshById(e.Id);

        private void OnCompleted(BuildingCompletedEvent e) => RefreshById(e.Id);

        private void RefreshById(BuildingId id)
        {
            for (int i = 0; i < _views.Count; i++)
            {
                var view = _views[i];
                if (view == null || view.BuildingId != id) continue;

                Refresh(view);
            }
        }

        /// <summary>Полное состояние панели по сервису: видимость/значение прогресса и доход.</summary>
        private void Refresh(BuildingPanelView view)
        {
            if (view == null) return;

            var id = view.BuildingId;
            // Только строящееся здание: выключенная заглушка (Enabled = false) с этапами в каталоге
            // не построена, но и не строится — прогресс у неё не показываем.
            bool inProgress = _build.IsUnderConstruction(id);

            view.SetProgressVisible(inProgress);
            if (inProgress)
                view.SetProgress(_build.GetInvestedInStage(id), _build.GetCurrentStageCost(id));

            view.SetRate(_build.GetCurrentIncomePerMinute(id));
        }
    }
}
