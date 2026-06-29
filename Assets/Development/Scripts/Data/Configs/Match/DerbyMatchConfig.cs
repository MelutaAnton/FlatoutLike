using System;
using System.Collections.Generic;
using FlatOutLike.Data.Configs.Car;
using FlatOutLike.Data.Configs.Enemies;
using UnityEngine;

namespace FlatOutLike.Data.Configs.Match
{
    /// <summary>
    /// Конфиг одного Derby-матча: определяет машину игрока и список врагов.
    /// Число врагов = длина массива _enemies (настраивается в инспекторе, рекомендуется 3–6).
    /// </summary>
    [CreateAssetMenu(fileName = "DerbyMatchConfig", menuName = "FlatOutLike/Match/DerbyMatchConfig")]
    public sealed class DerbyMatchConfig : ScriptableObject
    {
        [SerializeField] private GameObject      _playerCarPrefab;
        [SerializeField] private CarStatsConfig  _playerStats;
        [SerializeField] private EnemySpawnEntry[] _enemies;

        public GameObject     PlayerCarPrefab => _playerCarPrefab;
        public CarStatsConfig PlayerStats     => _playerStats;

        public IReadOnlyList<EnemySpawnEntry> Enemies => _enemies;

        /// <summary>
        /// Параметры одного врага в матче: профиль поведения и боевые статы.
        /// Стиль вложенного Serializable-struct соответствует <c>DamageStage</c> в CarDamageVisualConfig.
        /// </summary>
        [Serializable]
        public struct EnemySpawnEntry
        {
            [Tooltip("Prefab машины этого врага (своя модель/визуал). У каждого слота может быть свой.")]
            public GameObject carPrefab;

            [Tooltip("Профиль поведения AI (агрессивность, дистанции, тюнинг управления).")]
            public EnemyPersonalityConfig profile;

            [Tooltip("Боевые статы машины этого врага (скорость, броня, масса...).")]
            public CarStatsConfig stats;
        }
    }
}
