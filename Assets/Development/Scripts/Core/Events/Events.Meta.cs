using FlatOutLike.Models;

namespace FlatOutLike.Core.Events
{
    /// <summary>
    /// Публикуется <c>EconomyService</c> при любом изменении баланса монет
    /// (покупка машины/апгрейда, начисление награды). HUD/Garage обновляют
    /// отображаемый баланс без опроса сервиса каждый кадр.
    /// </summary>
    public readonly struct CurrencyChangedEvent
    {
        public readonly int OldValue;
        public readonly int NewValue;
        public readonly int Delta;

        public CurrencyChangedEvent(int oldValue, int newValue, int delta)
        {
            OldValue = oldValue;
            NewValue = newValue;
            Delta    = delta;
        }
    }

    /// <summary>
    /// Публикуется <c>EconomyService</c> при успешной покупке машины из каталога.
    /// Подписчики (Garage UI) обновляют состояние карточки машины (locked → owned).
    /// </summary>
    public readonly struct CarPurchasedEvent
    {
        public readonly string CarId;

        public CarPurchasedEvent(string carId)
        {
            CarId = carId;
        }
    }

    /// <summary>
    /// Публикуется <c>EconomyService</c> при успешной покупке следующего уровня апгрейда.
    /// Подписчики (Garage UI) обновляют строку прокачки соответствующей категории.
    /// </summary>
    public readonly struct UpgradePurchasedEvent
    {
        public readonly UpgradeCategory Category;
        public readonly int             NewLevel;

        public UpgradePurchasedEvent(UpgradeCategory category, int newLevel)
        {
            Category = category;
            NewLevel = newLevel;
        }
    }
}
