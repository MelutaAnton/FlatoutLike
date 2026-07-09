using System;
using System.Collections.Generic;
using FlatOutLike.Models;
using UnityEngine;

namespace FlatOutLike.Data.Configs.Upgrades
{
    /// <summary>
    /// Каталог апгрейдов Garage: по категории (Armor/Power/Nitro) — линейный список уровней
    /// с ценой и процентным эффектом (см. §8 game-design.md). Единственный источник правды
    /// для цен и эффектов апгрейдов — EconomyService и CarStatsService читают его отсюда.
    /// </summary>
    [CreateAssetMenu(fileName = "UpgradeCatalogConfig", menuName = "FlatOutLike/Upgrades/UpgradeCatalogConfig")]
    public sealed class UpgradeCatalogConfig : ScriptableObject
    {
        [SerializeField] private List<UpgradeCategoryEntry> _categories = new List<UpgradeCategoryEntry>();

        /// <summary>Ищет уровень апгрейда по категории и номеру уровня (уровни нумеруются с 1).</summary>
        public bool TryGetLevel(UpgradeCategory cat, int level, out UpgradeLevelEntry entry)
        {
            for (int i = 0; i < _categories.Count; i++)
            {
                var category = _categories[i];
                if (category == null || category.Category != cat)
                    continue;

                var levels = category.Levels;
                if (levels == null)
                    break;

                for (int j = 0; j < levels.Count; j++)
                {
                    if (levels[j] != null && levels[j].Level == level)
                    {
                        entry = levels[j];
                        return true;
                    }
                }

                break;
            }

            entry = null;
            return false;
        }

        /// <summary>Максимальный доступный уровень категории (0, если категории нет в каталоге).</summary>
        public int GetMaxLevel(UpgradeCategory cat)
        {
            for (int i = 0; i < _categories.Count; i++)
            {
                var category = _categories[i];
                if (category == null || category.Category != cat)
                    continue;

                var levels = category.Levels;
                if (levels == null || levels.Count == 0)
                    return 0;

                int max = 0;
                for (int j = 0; j < levels.Count; j++)
                    if (levels[j] != null && levels[j].Level > max)
                        max = levels[j].Level;

                return max;
            }

            return 0;
        }
    }

    /// <summary>Одна категория апгрейда (Armor/Power/Nitro) и её линейный список уровней.</summary>
    [Serializable]
    public sealed class UpgradeCategoryEntry
    {
        [SerializeField] private UpgradeCategory _category;
        [SerializeField] private List<UpgradeLevelEntry> _levels = new List<UpgradeLevelEntry>();

        public UpgradeCategory Category => _category;
        public IReadOnlyList<UpgradeLevelEntry> Levels => _levels;
    }

    /// <summary>
    /// Один уровень апгрейда: номер, процентный эффект (10 = +10% к соответствующим статам)
    /// и цена перехода на этот уровень.
    /// </summary>
    [Serializable]
    public sealed class UpgradeLevelEntry
    {
        [SerializeField] private int _level;
        [SerializeField] private float _effectValue;
        [SerializeField] private int _price;

        public int Level => _level;
        public float EffectValue => _effectValue;
        public int Price => _price;
    }
}
