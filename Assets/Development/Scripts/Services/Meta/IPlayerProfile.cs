using System.Collections.Generic;
using FlatOutLike.Models;

namespace FlatOutLike.Services.Meta
{
    public interface IPlayerProfile
    {
        int Coins { get; set; }

        /// <summary>Личный рекорд трюка «High Jump» — пиковая ВЫСОТА в метрах,
        /// единственная метрика соревнования (композитный счёт удалён; монеты — от высоты).</summary>
        float BestHighJumpHeight { get; set; }

        /// <summary>Id выбранной машины из <c>CarCatalogConfig</c>. Пустая строка = не выбрана —
        /// потребители (CarFactory, Garage UI) берут первую entry каталога.</summary>
        string SelectedCarId { get; set; }

        /// <summary>Id всех купленных машин (стартовая бесплатная машина сюда не добавляется —
        /// её владение определяется по цене 0 в каталоге).</summary>
        IReadOnlyCollection<string> OwnedCarIds { get; }

        bool OwnsCar(string carId);
        void AddOwnedCar(string carId);

        /// <summary>Уровень апгрейда конкретной машины (per-car прогресс, §8/§14.3 game-design.md).
        /// Пустой/null <paramref name="carId"/> → 0 (нет машины — нет прокачки).</summary>
        int GetUpgradeLevel(string carId, UpgradeCategory category);

        /// <summary>Пустой/null <paramref name="carId"/> → запись игнорируется.</summary>
        void SetUpgradeLevel(string carId, UpgradeCategory category, int level);

        /// <summary>Перечисление всех сохранённых уровней апгрейдов по всем машинам —
        /// нужно хранилищу для сериализации (PlayerPrefs не итерируем по ключам).</summary>
        IReadOnlyCollection<(string CarId, UpgradeCategory Category, int Level)> GetAllUpgradeLevels();
    }
}
