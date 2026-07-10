using FlatOutLike.Models;

namespace FlatOutLike.Services.Combat
{
    public interface IDamageService
    {
        void RegisterCar(CarId id, float maxArmor, float mass);
        void UnregisterCar(CarId id);
        float GetHp(CarId id);

        /// <summary>Максимальный запас брони машины (для нормировки HP в HUD, 0 — не зарегистрирована).</summary>
        float GetMaxArmor(CarId id);

        /// <summary>
        /// Восстанавливает <paramref name="fraction"/> * MaxArmor HP (с cap по MaxArmor).
        /// Публикует <c>CarDamagedEvent</c> с отрицательным damage, чтобы
        /// <c>CarDamageVisualController</c> мог откатить визуал.
        /// Если машина не зарегистрирована — no-op.
        /// </summary>
        void Repair(CarId id, float fraction);
    }
}
