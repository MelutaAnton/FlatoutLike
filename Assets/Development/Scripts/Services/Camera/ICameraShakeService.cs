namespace FlatOutLike.Services.Camera
{
    /// <summary>
    /// Оркестрация тряски камеры от столкновений машин в дерби. Маркерный интерфейс:
    /// вся логика — подписка на <c>CarCollisionEvent</c> в конструкторе, публичных
    /// методов не требуется (аналог CameraService/DamageService).
    /// </summary>
    public interface ICameraShakeService
    {
    }
}
