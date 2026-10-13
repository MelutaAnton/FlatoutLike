using System;
using FlatOutLike.Models;
using UnityEngine;

namespace FlatOutLike.Services.Progression
{
    /// <summary>
    /// Save-блок хаба в PlayerPrefs. Ключи — только здесь:
    /// <c>FlatOutLike.Hub.Build.&lt;(int)BuildingId&gt;.Stages</c> и <c>.Invested</c>.
    /// Числовой id (а не имя enum) — переименование значения BuildingId не теряет сейв.
    /// </summary>
    public sealed class PlayerPrefsHubSaveStorage : IHubSaveStorage
    {
        // Единственный источник ключей save-блока хаба.
        private const string BuildKeyPrefix = "FlatOutLike.Hub.Build.";
        private const string StagesKeySuffix = ".Stages";
        private const string InvestedKeySuffix = ".Invested";

        private static readonly BuildingId[] AllBuildings =
            (BuildingId[])Enum.GetValues(typeof(BuildingId));

        public HubSaveData Load()
        {
            var data = new HubSaveData();

            foreach (var id in AllBuildings)
            {
                string stagesKey = StagesKey(id);
                string investedKey = InvestedKey(id);

                // Нет ни одного ключа — здание в сейве не встречалось, запись не заводим.
                if (!PlayerPrefs.HasKey(stagesKey) && !PlayerPrefs.HasKey(investedKey))
                    continue;

                data.Set(id, PlayerPrefs.GetInt(stagesKey, 0), PlayerPrefs.GetInt(investedKey, 0));
            }

            return data;
        }

        public void Save(HubSaveData data)
        {
            if (data == null) return;

            foreach (var record in data.Records)
            {
                PlayerPrefs.SetInt(StagesKey(record.Id), record.CompletedStages);
                PlayerPrefs.SetInt(InvestedKey(record.Id), record.InvestedInStage);
            }

            PlayerPrefs.Save();
        }

        private static string StagesKey(BuildingId id) => BuildKeyPrefix + (int)id + StagesKeySuffix;

        private static string InvestedKey(BuildingId id) => BuildKeyPrefix + (int)id + InvestedKeySuffix;
    }
}
