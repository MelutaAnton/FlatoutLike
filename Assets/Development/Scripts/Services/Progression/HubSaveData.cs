using System.Collections.Generic;
using FlatOutLike.Models;

namespace FlatOutLike.Services.Progression
{
    /// <summary>
    /// Save-блок хаба (отдельный от профиля MetaProgressService, cli-workflow «Шаг 12»):
    /// прогресс стройки по зданиям. Здание без записи — ничего не построено и не вложено.
    /// </summary>
    public sealed class HubSaveData
    {
        private readonly Dictionary<BuildingId, BuildingProgressRecord> _records =
            new Dictionary<BuildingId, BuildingProgressRecord>();

        /// <summary>Все записи (порядок не гарантирован).</summary>
        public IEnumerable<BuildingProgressRecord> Records => _records.Values;

        public bool TryGet(BuildingId id, out BuildingProgressRecord record) =>
            _records.TryGetValue(id, out record);

        /// <summary>Создаёт или перезаписывает запись здания.</summary>
        public void Set(BuildingId id, int completedStages, int investedInStage)
        {
            if (_records.TryGetValue(id, out var record))
            {
                record.CompletedStages = completedStages;
                record.InvestedInStage = investedInStage;
                return;
            }

            _records[id] = new BuildingProgressRecord(id, completedStages, investedInStage);
        }
    }
}
