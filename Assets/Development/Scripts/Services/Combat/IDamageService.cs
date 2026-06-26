using FlatOutLike.Models;

namespace FlatOutLike.Services.Combat
{
    public interface IDamageService
    {
        void RegisterCar(CarId id, float maxArmor, float mass);
        void UnregisterCar(CarId id);
        float GetHp(CarId id);
    }
}
