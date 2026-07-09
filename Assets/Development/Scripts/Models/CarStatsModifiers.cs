namespace FlatOutLike.Models
{
    /// <summary>
    /// Множители статов машины, собранные из уровней апгрейдов (§8 game-design.md).
    /// 1.0 по каждому полю — отсутствие бонуса (машина без апгрейдов).
    /// </summary>
    public readonly struct CarStatsModifiers
    {
        public readonly float ArmorMultiplier;
        public readonly float PowerMultiplier;
        public readonly float NitroMultiplier;

        public CarStatsModifiers(float armorMultiplier, float powerMultiplier, float nitroMultiplier)
        {
            ArmorMultiplier = armorMultiplier;
            PowerMultiplier = powerMultiplier;
            NitroMultiplier = nitroMultiplier;
        }
    }
}
