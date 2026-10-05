namespace FlatOutLike.Models
{
    /// <summary>
    /// Идентификаторы построек BaseHub (game-design §6). Значения ЯВНЫЕ и append-only:
    /// enum сериализуется числом в BuildingCatalogConfig (и далее — в сейве прогресса стройки),
    /// поэтому существующие числа не менять, новые постройки — только в конец с новым числом.
    /// </summary>
    public enum BuildingId
    {
        DerbyArena = 0,
        Garage = 1,
        HighJumpTower = 2,
        TicketBooth = 3,
        Tribunes = 4,
        LongJump = 5,
        FireRings = 6,
        Bowling = 7,
        FieldGoal = 8,
        HighCard = 9
    }
}
