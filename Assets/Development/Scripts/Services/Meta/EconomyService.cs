using FlatOutLike.Core.Events;
using FlatOutLike.Data.Configs.Car;
using FlatOutLike.Data.Configs.Upgrades;
using FlatOutLike.Models;

namespace FlatOutLike.Services.Meta
{
    /// <summary>
    /// Единственная точка мутации баланса монет (см. <see cref="IEconomyService"/>).
    /// Читает каталоги машин/апгрейдов, проверяет доступность покупки, списывает монеты,
    /// обновляет профиль и публикует события (<see cref="CurrencyChangedEvent"/> и т.д.).
    /// </summary>
    public sealed class EconomyService : IEconomyService
    {
        private readonly IEventBus _eventBus;
        private readonly IMetaProgressService _meta;
        private readonly CarCatalogConfig _carCatalog;
        private readonly UpgradeCatalogConfig _upgradeCatalog;

        public EconomyService(
            IEventBus eventBus,
            IMetaProgressService meta,
            CarCatalogConfig carCatalog,
            UpgradeCatalogConfig upgradeCatalog)
        {
            _eventBus = eventBus;
            _meta = meta;
            _carCatalog = carCatalog;
            _upgradeCatalog = upgradeCatalog;
        }

        public int Coins => _meta.Profile.Coins;

        public string SelectedCarId
        {
            get
            {
                var selected = _meta.Profile.SelectedCarId;
                if (!string.IsNullOrEmpty(selected) && _carCatalog.TryGetEntry(selected, out _))
                    return selected;

                // Невалидный/пустой id в профиле — берём первую ВЛАДЕЕМУЮ entry (бесплатную
                // или уже купленную). Раньше здесь безусловно бралась entries[0] — если она
                // платная и не куплена, CarFactory/Garage подставляли бы машину, которой у
                // игрока нет (gap Шага 8a, закрыт в Шаге 8c).
                var entries = _carCatalog.Entries;
                for (int i = 0; i < entries.Count; i++)
                {
                    var entry = entries[i];
                    if (entry != null && (entry.Price == 0 || _meta.Profile.OwnsCar(entry.Id)))
                        return entry.Id;
                }

                // Владеемых entries нет вообще (например, все машины платные и профиль пуст) —
                // фоллбэк на первую entry каталога, как раньше.
                return entries.Count > 0 ? entries[0].Id : string.Empty;
            }
        }

        public bool IsCarOwned(string carId)
        {
            if (!_carCatalog.TryGetEntry(carId, out var entry))
                return false;

            // Стартовая машина (цена 0) владеема без покупки.
            return entry.Price == 0 || _meta.Profile.OwnsCar(carId);
        }

        public bool CanBuyCar(string carId)
        {
            if (!_carCatalog.TryGetEntry(carId, out var entry))
                return false;

            if (IsCarOwned(carId))
                return false;

            return Coins >= entry.Price;
        }

        public bool TryBuyCar(string carId)
        {
            if (!CanBuyCar(carId))
                return false;

            _carCatalog.TryGetEntry(carId, out var entry);

            int oldValue = Coins;
            int newValue = oldValue - entry.Price;
            _meta.Profile.Coins = newValue;
            _meta.Profile.AddOwnedCar(carId);
            _meta.Save();

            _eventBus.Publish(new CarPurchasedEvent(carId));
            _eventBus.Publish(new CurrencyChangedEvent(oldValue, newValue, -entry.Price));

            return true;
        }

        public bool TrySelectCar(string carId)
        {
            if (!IsCarOwned(carId))
                return false;

            _meta.Profile.SelectedCarId = carId;
            _meta.Save();
            return true;
        }

        // Публичные сигнатуры GetUpgradeLevel/CanBuyUpgrade/TryBuyUpgrade не меняются — они всегда
        // работают с ТЕКУЩЕЙ выбранной машиной (SelectedCarId), per-car хранение — деталь профиля.
        public int GetUpgradeLevel(UpgradeCategory category) => _meta.Profile.GetUpgradeLevel(SelectedCarId, category);

        public bool CanBuyUpgrade(UpgradeCategory category)
        {
            int currentLevel = GetUpgradeLevel(category);
            int maxLevel = _upgradeCatalog.GetMaxLevel(category);

            if (currentLevel >= maxLevel)
                return false;

            if (!_upgradeCatalog.TryGetLevel(category, currentLevel + 1, out var nextLevel))
                return false;

            return Coins >= nextLevel.Price;
        }

        public bool TryBuyUpgrade(UpgradeCategory category)
        {
            if (!CanBuyUpgrade(category))
                return false;

            string carId = SelectedCarId;
            int currentLevel = GetUpgradeLevel(category);
            _upgradeCatalog.TryGetLevel(category, currentLevel + 1, out var nextLevel);

            int oldValue = Coins;
            int newValue = oldValue - nextLevel.Price;
            _meta.Profile.Coins = newValue;
            _meta.Profile.SetUpgradeLevel(carId, category, nextLevel.Level);
            _meta.Save();

            _eventBus.Publish(new UpgradePurchasedEvent(category, nextLevel.Level, carId));
            _eventBus.Publish(new CurrencyChangedEvent(oldValue, newValue, -nextLevel.Price));

            return true;
        }

        public void Credit(int coinAmount)
        {
            if (coinAmount <= 0)
                return;

            int oldValue = Coins;
            _meta.Profile.Coins = oldValue + coinAmount;
            _meta.Save();

            _eventBus.Publish(new CurrencyChangedEvent(oldValue, _meta.Profile.Coins, coinAmount));
        }

        public bool TrySpend(int amount) => TrySpend(amount, true);

        public bool TrySpend(int amount, bool persist)
        {
            if (amount <= 0)
                return false;

            int oldValue = Coins;
            if (oldValue < amount)
                return false;

            // Тот же паттерн, что у покупок: профиль → Save → событие баланса.
            // persist = false — Save делает вызывающий в своей точке flush (см. IEconomyService).
            int newValue = oldValue - amount;
            _meta.Profile.Coins = newValue;
            if (persist)
                _meta.Save();

            _eventBus.Publish(new CurrencyChangedEvent(oldValue, newValue, -amount));

            return true;
        }
    }
}
