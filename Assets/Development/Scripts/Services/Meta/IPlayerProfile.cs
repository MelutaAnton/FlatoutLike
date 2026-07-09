using System.Collections.Generic;
using FlatOutLike.Models;

namespace FlatOutLike.Services.Meta
{
    public interface IPlayerProfile
    {
        int Coins { get; set; }

        /// <summary>Личный рекорд трюка «High Jump» — пиковая ВЫСОТА в метрах
        /// (суть соревнования — высота; счёт с флипами идёт только на монеты).</summary>
        float BestHighJumpHeight { get; set; }

        /// <summary>Id выбранной машины из <c>CarCatalogConfig</c>. Пустая строка = не выбрана —
        /// потребители (CarFactory, Garage UI) берут первую entry каталога.</summary>
        string SelectedCarId { get; set; }

        /// <summary>Id всех купленных машин (стартовая бесплатная машина сюда не добавляется —
        /// её владение определяется по цене 0 в каталоге).</summary>
        IReadOnlyCollection<string> OwnedCarIds { get; }

        bool OwnsCar(string carId);
        void AddOwnedCar(string carId);

        int GetUpgradeLevel(UpgradeCategory category);
        void SetUpgradeLevel(UpgradeCategory category, int level);
    }
}
