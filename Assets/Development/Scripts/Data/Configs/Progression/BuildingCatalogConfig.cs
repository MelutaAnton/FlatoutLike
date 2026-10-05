using System;
using System.Collections.Generic;
using FlatOutLike.Models;
using UnityEngine;

namespace FlatOutLike.Data.Configs.Progression
{
    /// <summary>
    /// Каталог построек BaseHub (game-design §6): где стоит каждая постройка, доступна ли она
    /// и каким префабом рисуется, плюс точка спавна персонажа. В 11a каталог потребляет только
    /// editor-сборка сцены; этапы стройки/цены/доход/зоны запуска — шаги 11b/12/13.
    /// </summary>
    [CreateAssetMenu(fileName = "BuildingCatalogConfig", menuName = "FlatOutLike/Progression/BuildingCatalogConfig")]
    public sealed class BuildingCatalogConfig : ScriptableObject
    {
        [SerializeField] private BuildingEntry[] _entries;

        [Tooltip("Мировая позиция спавна персонажа в BaseHub.")]
        [SerializeField] private Vector3 _characterSpawnPosition;

        [Tooltip("Поворот персонажа при спавне вокруг Y, град.")]
        [SerializeField] private float _characterSpawnYRotation;

        [Tooltip("Подпись на заглушке недоступной постройки.")]
        [SerializeField] private string _comingSoonLabel = "COMING SOON";

        public IReadOnlyList<BuildingEntry> Entries =>
            _entries != null ? _entries : Array.Empty<BuildingEntry>();

        public Vector3 CharacterSpawnPosition => _characterSpawnPosition;
        public float CharacterSpawnYRotation => _characterSpawnYRotation;
        public string ComingSoonLabel => _comingSoonLabel;

        /// <summary>Ищет постройку по id. Каждый BuildingId встречается в каталоге не более раза.</summary>
        public bool TryGetEntry(BuildingId id, out BuildingEntry entry)
        {
            if (_entries != null)
            {
                for (int i = 0; i < _entries.Length; i++)
                {
                    if (_entries[i] != null && _entries[i].Id == id)
                    {
                        entry = _entries[i];
                        return true;
                    }
                }
            }

            entry = null;
            return false;
        }

        /// <summary>Одна постройка базы: id, размещение, доступность и визуальный префаб.</summary>
        [Serializable]
        public sealed class BuildingEntry
        {
            public BuildingId Id;
            public Vector3 Position;
            public float YRotation;
            public bool Enabled = true;
            public GameObject VisualPrefab;
        }
    }
}
