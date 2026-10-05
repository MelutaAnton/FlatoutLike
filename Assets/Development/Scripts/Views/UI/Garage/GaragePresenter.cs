using System;
using FlatOutLike.Core.Events;
using FlatOutLike.Data.Configs.Car;
using FlatOutLike.Data.Configs.Upgrades;
using FlatOutLike.Models;
using FlatOutLike.Services.Flow;
using FlatOutLike.Services.Meta;

namespace FlatOutLike.Views.UI.Garage
{
    /// <summary>
    /// Presenter экрана Garage (pure C#). Заполняет карточки машин/апгрейдов данными каталогов,
    /// делегирует покупку/выбор в IEconomyService (единственная точка мутации баланса) и
    /// рефрешит View по событиям экономики. Строки апгрейдов индексируются порядком значений
    /// enum UpgradeCategory — единственный источник порядка категорий, дублировать его в SO
    /// не нужно.
    /// </summary>
    public sealed class GaragePresenter : IDisposable
    {
        private static readonly UpgradeCategory[] UpgradeCategories =
            (UpgradeCategory[])Enum.GetValues(typeof(UpgradeCategory));

        private readonly GarageView _view;
        private readonly IEconomyService _economy;
        private readonly IEventBus _eventBus;
        private readonly CarCatalogConfig _carCatalog;
        private readonly UpgradeCatalogConfig _upgradeCatalog;
        private readonly IGameFlowService _gameFlow;

        public GaragePresenter(
            GarageView view,
            IEconomyService economy,
            IEventBus eventBus,
            CarCatalogConfig carCatalog,
            UpgradeCatalogConfig upgradeCatalog,
            IGameFlowService gameFlow)
        {
            _view = view;
            _economy = economy;
            _eventBus = eventBus;
            _carCatalog = carCatalog;
            _upgradeCatalog = upgradeCatalog;
            _gameFlow = gameFlow;

            _view.OnBackClicked += HandleBackClicked;
            _view.OnCarCardClicked += HandleCarCardClicked;
            _view.OnUpgradeBuyClicked += HandleUpgradeBuyClicked;

            _eventBus.Subscribe<CurrencyChangedEvent>(OnCurrencyChanged);
            _eventBus.Subscribe<CarPurchasedEvent>(OnCarPurchased);
            _eventBus.Subscribe<UpgradePurchasedEvent>(OnUpgradePurchased);

            BuildCarCards();
            RefreshAll();
        }

        public void Dispose()
        {
            _view.OnBackClicked -= HandleBackClicked;
            _view.OnCarCardClicked -= HandleCarCardClicked;
            _view.OnUpgradeBuyClicked -= HandleUpgradeBuyClicked;

            _eventBus.Unsubscribe<CurrencyChangedEvent>(OnCurrencyChanged);
            _eventBus.Unsubscribe<CarPurchasedEvent>(OnCarPurchased);
            _eventBus.Unsubscribe<UpgradePurchasedEvent>(OnUpgradePurchased);
        }

        // ── Построение и рефреш ────────────────────────────────────────────────

        /// <summary>Статичные данные карточек (id/название/цена/иконка) — заполняются один раз.</summary>
        private void BuildCarCards()
        {
            var cards = _view.CarCards;
            var entries = _carCatalog.Entries;
            if (cards == null) return;

            int count = Math.Min(cards.Count, entries.Count);
            for (int i = 0; i < count; i++)
            {
                var entry = entries[i];
                if (entry == null || cards[i] == null) continue;

                // Иконка entry может отсутствовать — CarCardView.SetData сам обрабатывает null.
                cards[i].SetData(entry.Id, entry.DisplayName, entry.Price, entry.Icon);
            }
        }

        private void RefreshAll()
        {
            RefreshCoins();
            RefreshCarCards();
            RefreshUpgradeRows();
        }

        private void RefreshCoins() => _view.SetCoins(_economy.Coins);

        private void RefreshCarCards()
        {
            var cards = _view.CarCards;
            var entries = _carCatalog.Entries;
            if (cards == null) return;

            int count = Math.Min(cards.Count, entries.Count);
            string selected = _economy.SelectedCarId;

            for (int i = 0; i < count; i++)
            {
                var entry = entries[i];
                if (entry == null || cards[i] == null) continue;

                bool owned = _economy.IsCarOwned(entry.Id);
                bool isSelected = entry.Id == selected;
                bool canAfford = _economy.CanBuyCar(entry.Id);
                cards[i].SetState(owned, isSelected, canAfford);
            }
        }

        private void RefreshUpgradeRows()
        {
            var rows = _view.UpgradeRows;
            if (rows == null) return;

            int count = Math.Min(rows.Count, UpgradeCategories.Length);
            for (int i = 0; i < count; i++)
            {
                if (rows[i] == null) continue;

                var category = UpgradeCategories[i];
                int level = _economy.GetUpgradeLevel(category);
                int maxLevel = _upgradeCatalog.GetMaxLevel(category);
                bool maxed = level >= maxLevel;

                int nextPrice = 0;
                if (!maxed && _upgradeCatalog.TryGetLevel(category, level + 1, out var nextLevel))
                    nextPrice = nextLevel.Price;

                bool canAfford = _economy.CanBuyUpgrade(category);
                rows[i].SetData(category, level, maxLevel, nextPrice, canAfford, maxed);
            }
        }

        // ── Обработчики кликов View ─────────────────────────────────────────────

        private void HandleCarCardClicked(string carId)
        {
            if (!_economy.IsCarOwned(carId))
            {
                // Успешная покупка сразу выбирает машину — игроку не нужен второй клик.
                if (_economy.TryBuyCar(carId))
                    _economy.TrySelectCar(carId);
            }
            else
            {
                _economy.TrySelectCar(carId);
            }

            // TrySelectCar не публикует событий (см. EconomyService) — рефрешим явно.
            // Апгрейды теперь per-car (§8/§14.3 game-design.md) — строки прокачки обязаны
            // перерисоваться под НОВУЮ выбранную машину, а не только маркер выбора карточки.
            RefreshCarCards();
            RefreshUpgradeRows();
        }

        private void HandleUpgradeBuyClicked(UpgradeCategory category)
        {
            _economy.TryBuyUpgrade(category);
            RefreshUpgradeRows();
        }

        private void HandleBackClicked() => _gameFlow.ChangeState(GameState.BaseHub);

        // ── Обработчики событий экономики ───────────────────────────────────────

        private void OnCurrencyChanged(CurrencyChangedEvent e) => RefreshAll();

        // Покупка машины (см. EconomyService.TryBuyCar) публикуется ДО TrySelectCar — на этот
        // момент SelectedCarId в профиле ещё не сменился, но рефрешим и апгрейды тоже: событие
        // публичное, и любой будущий вызывающий код (не только текущий флоу HandleCarCardClicked)
        // не должен полагаться на скрытый побочный рефреш из другого обработчика.
        private void OnCarPurchased(CarPurchasedEvent e)
        {
            RefreshCarCards();
            RefreshUpgradeRows();
        }

        private void OnUpgradePurchased(UpgradePurchasedEvent e) => RefreshUpgradeRows();
    }
}
