using System;
using System.Collections.Generic;
using FlatOutLike.Models;
using UnityEngine;

namespace FlatOutLike.Data.Configs.Stunts
{
    /// <summary>
    /// Каталог карточек Stunts Hub (см. §6 game-design.md). Единственный источник правды
    /// для списка трюков — StuntsHubPresenter читает entries отсюда и не хардкодит
    /// типы/сцены/подписи по местам.
    /// </summary>
    [CreateAssetMenu(fileName = "StuntsCatalogConfig", menuName = "FlatOutLike/Stunts/StuntsCatalogConfig")]
    public sealed class StuntsCatalogConfig : ScriptableObject
    {
        [SerializeField] private List<StuntsCardEntry> _entries = new List<StuntsCardEntry>();

        public IReadOnlyList<StuntsCardEntry> Entries => _entries;

        /// <summary>Ищет entry по типу трюка. Каждый StuntType встречается в каталоге не более раза.</summary>
        public bool TryGetEntry(StuntType type, out StuntsCardEntry entry)
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i] != null && _entries[i].Type == type)
                {
                    entry = _entries[i];
                    return true;
                }
            }

            entry = null;
            return false;
        }
    }

    /// <summary>Одна карточка Stunts Hub: тип трюка, отображаемые данные, целевая сцена и доступность.</summary>
    [Serializable]
    public sealed class StuntsCardEntry
    {
        [SerializeField] private StuntType _type;
        [SerializeField] private string _title;
        [SerializeField] private string _sceneKey;
        [SerializeField] private bool _enabled;
        [SerializeField] private Sprite _preview;

        public StuntType Type => _type;
        public string Title => _title;
        public string SceneKey => _sceneKey;
        public bool Enabled => _enabled;
        public Sprite Preview => _preview;
    }
}
