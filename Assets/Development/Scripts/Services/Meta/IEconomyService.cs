using FlatOutLike.Models;

namespace FlatOutLike.Services.Meta
{
    /// <summary>
    /// Единственная точка мутации баланса монет игрока (§8 game-design.md).
    /// Покупка машин/апгрейдов и начисление наград идут только через этот сервис —
    /// это гарантирует, что <see cref="Core.Events.CurrencyChangedEvent"/> публикуется
    /// при любом изменении баланса.
    /// </summary>
    public interface IEconomyService
    {
        int Coins { get; }

        /// <summary>Id текущей выбранной машины. Пустая/невалидная запись в профиле →
        /// id первой entry каталога (или пустая строка, если каталог пуст).</summary>
        string SelectedCarId { get; }

        /// <summary>Стартовая машина (цена 0) считается владеемой без покупки.</summary>
        bool IsCarOwned(string carId);
        bool CanBuyCar(string carId);
        bool TryBuyCar(string carId);

        /// <summary>Выбирает машину, если она уже владеема. Невалидный/неcвой id — no-op, false.</summary>
        bool TrySelectCar(string carId);

        int GetUpgradeLevel(UpgradeCategory category);
        bool CanBuyUpgrade(UpgradeCategory category);
        bool TryBuyUpgrade(UpgradeCategory category);

        /// <summary>Начисляет монеты (например, награду за матч). Значения ≤0 игнорируются.</summary>
        void Credit(int coinAmount);

        /// <summary>
        /// Списывает монеты на нужды вне каталогов (потоковое вложение в стройку BaseHub).
        /// amount ≤ 0 или баланса не хватает — false без изменений; иначе списывает, сохраняет
        /// профиль и публикует <see cref="Core.Events.CurrencyChangedEvent"/> с отрицательной дельтой.
        /// Эквивалент <c>TrySpend(amount, persist: true)</c>.
        /// </summary>
        bool TrySpend(int amount);

        /// <summary>
        /// То же, что <see cref="TrySpend(int)"/>, но при <paramref name="persist"/> = false профиль
        /// НЕ сохраняется на диск: баланс меняется в памяти и <see cref="Core.Events.CurrencyChangedEvent"/>
        /// публикуется как обычно. Для частых мелких списаний (поток вложения в стройку), где запись
        /// на каждое списание даёт фризы на мобиле. Вызывающий ОБЯЗАН сам сохранить профиль в своей
        /// точке flush (через <see cref="IMetaProgressService.Save"/>) — иначе списание потеряется
        /// при выходе из игры.
        /// </summary>
        bool TrySpend(int amount, bool persist);
    }
}
