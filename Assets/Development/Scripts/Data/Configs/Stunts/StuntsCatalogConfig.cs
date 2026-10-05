using System;
using System.Collections.Generic;
using FlatOutLike.Models;
using UnityEngine;

namespace FlatOutLike.Data.Configs.Stunts
{
    /// <summary>
    /// Каталог трюков (см. §6 game-design.md). Единственный источник правды для списка
    /// трюков — типы/подписи/превью не хардкодятся по местам. Отдельный экран Stunts Hub
    /// удалён (шаг 11a): точки входа в трюки — постройки BaseHub; целевая сцена трюка
    /// определяется GameStateSceneMap, а не строкой в каталоге.
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

    /// <summary>Один трюк каталога: тип, отображаемые данные и доступность.</summary>
    [Serializable]
    public sealed class StuntsCardEntry
    {
        [SerializeField] private StuntType _type;
        [SerializeField] private string _title;
        [SerializeField] private bool _enabled;
        [SerializeField] private Sprite _preview;

        public StuntType Type => _type;
        public string Title => _title;
        public bool Enabled => _enabled;
        public Sprite Preview => _preview;
    }
}
