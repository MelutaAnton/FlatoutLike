using System;
using FlatOutLike.Data.Configs.Stunts;
using FlatOutLike.Models;
using FlatOutLike.Services.Flow;

namespace FlatOutLike.Views.UI.Stunts
{
    /// <summary>
    /// Presenter Stunts Hub (pure C#). Заполняет карточки из StuntsCatalogConfig; по клику
    /// enabled-карточки HighJump переключает GameState на StuntsHighJumpDriving. Остальные
    /// StuntType в этом срезе — заглушки ("Coming soon"), их карточки disabled на уровне View
    /// (см. StuntsCardView.SetData) и клик по ним сюда не проходит логикой перехода.
    /// </summary>
    public sealed class StuntsHubPresenter : IDisposable
    {
        private readonly StuntsHubView _view;
        private readonly StuntsCatalogConfig _catalog;
        private readonly IGameFlowService _gameFlow;

        public StuntsHubPresenter(StuntsHubView view, StuntsCatalogConfig catalog, IGameFlowService gameFlow)
        {
            _view = view;
            _catalog = catalog;
            _gameFlow = gameFlow;

            _view.OnBackClicked += HandleBackClicked;
            _view.OnCardClicked += HandleCardClicked;

            BuildCards();
        }

        public void Dispose()
        {
            _view.OnBackClicked -= HandleBackClicked;
            _view.OnCardClicked -= HandleCardClicked;
        }

        private void BuildCards()
        {
            var cards = _view.Cards;
            var entries = _catalog.Entries;
            if (cards == null) return;

            int count = Math.Min(cards.Count, entries.Count);
            for (int i = 0; i < count; i++)
            {
                var entry = entries[i];
                if (entry == null || cards[i] == null) continue;

                cards[i].SetData(entry.Type, entry.Title, entry.Enabled, entry.Preview);
            }
        }

        private void HandleCardClicked(StuntType type)
        {
            if (!_catalog.TryGetEntry(type, out var entry) || !entry.Enabled)
                return; // disabled/неизвестная entry — no-op (карточка и так некликабельна)

            // Единственный реализованный трюк в этом срезе — HighJump; остальные типы
            // в каталоге помечены Enabled=false и сюда не доходят.
            if (type == StuntType.HighJump)
                _gameFlow.ChangeState(GameState.StuntsHighJumpDriving);
        }

        private void HandleBackClicked() => _gameFlow.ChangeState(GameState.MainMenu);
    }
}
