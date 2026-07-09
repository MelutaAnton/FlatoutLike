using System;
using System.Collections.Generic;
using UnityEngine;

namespace FlatOutLike.Data.Configs.Car
{
    /// <summary>
    /// Каталог машин, доступных в Garage: список entries с ценой, статами и prefab'ом.
    /// Единственный источник правды для id машин (см. §8 game-design.md) — покупка,
    /// выбор и спавн игрока читают entries отсюда.
    /// </summary>
    [CreateAssetMenu(fileName = "CarCatalogConfig", menuName = "FlatOutLike/Car/CarCatalogConfig")]
    public sealed class CarCatalogConfig : ScriptableObject
    {
        [SerializeField] private List<CarCatalogEntry> _entries = new List<CarCatalogEntry>();

        public IReadOnlyList<CarCatalogEntry> Entries => _entries;

        /// <summary>Ищет entry по id (регистрозависимое сравнение — id считается техническим ключом).</summary>
        public bool TryGetEntry(string id, out CarCatalogEntry entry)
        {
            if (!string.IsNullOrEmpty(id))
            {
                for (int i = 0; i < _entries.Count; i++)
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
    }

    /// <summary>Одна машина каталога: идентификатор, отображаемые данные, статы и цена.</summary>
    [Serializable]
    public sealed class CarCatalogEntry
    {
        [SerializeField] private string _id;
        [SerializeField] private string _displayName;
        [SerializeField] private CarStatsConfig _stats;
        [SerializeField] private int _price;
        [SerializeField] private Sprite _icon;
        [SerializeField] private GameObject _prefab;

        public string Id => _id;
        public string DisplayName => _displayName;
        public CarStatsConfig Stats => _stats;
        public int Price => _price;
        public Sprite Icon => _icon;
        public GameObject Prefab => _prefab;
    }
}
