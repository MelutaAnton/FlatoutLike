namespace FlatOutLike.Models
{
    /// <summary>
    /// Имена физических слоёв проекта.
    /// Единственный источник правды — не дублировать строки слоёв в других файлах.
    /// </summary>
    public static class Layers
    {
        public const string Vehicle = "Vehicle";
        public const string Wall    = "Wall";
        public const string Hazard  = "Hazard";
        public const string Pickup  = "Pickup";
    }
}
