using FlatOutLike.Models;

namespace FlatOutLike.Services.Progression
{
    /// <summary>
    /// Сохраняемый прогресс стройки одного здания BaseHub: сколько этапов завершено
    /// и сколько монет уже вложено в текущий (незавершённый) этап.
    /// </summary>
    public sealed class BuildingProgressRecord
    {
        public BuildingId Id;
        public int CompletedStages;
        public int InvestedInStage;

        public BuildingProgressRecord(BuildingId id, int completedStages, int investedInStage)
        {
            Id = id;
            CompletedStages = completedStages;
            InvestedInStage = investedInStage;
        }
    }
}
