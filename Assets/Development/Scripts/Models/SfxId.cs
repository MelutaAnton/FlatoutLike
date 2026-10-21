namespace FlatOutLike.Models
{
    /// <summary>
    /// Идентификаторы звуковых эффектов проекта. Единственный источник — клипы
    /// назначаются в <c>AudioConfig</c> (Data/Configs/Audio), нигде не дублируется строками.
    /// </summary>
    public enum SfxId
    {
        None = 0,
        UiClick,
        CrashLight,
        CrashHeavy,
        CarWrecked,
        NitroLoop,
        PickupRepair,
        PickupNitro,
        Eject,
        MidairBoost,
        Landing,
        NewRecord,
        MatchWin,
        MatchLose,
        Purchase,
        BuildInvestTick,
        BuildStageComplete,
        BuildingCompleted
    }
}
