namespace FlatOutLike.Controllers.Camera
{
    /// <summary>
    /// Приоритеты vcam при переключении режимов камеры через Priority.
    /// Единое значение для всех <see cref="ICameraBehaviour"/>: Brain блендит по приоритету.
    /// </summary>
    public static class CameraPriorities
    {
        public const int Active = 10;
        public const int Inactive = 0;
    }
}
