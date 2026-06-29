using System;
using System.Collections.Generic;
using FlatOutLike.Models;
using UnityEngine;

namespace FlatOutLike.Data.Configs.Pickups
{
    [CreateAssetMenu(fileName = "PickupConfig", menuName = "FlatOutLike/Pickups/PickupConfig")]
    public sealed class PickupConfig : ScriptableObject
    {
        [Header("Ремонт")]
        [SerializeField, Range(0f, 1f)] private float _repairFraction = 0.4f;

        [Header("Нитро")]
        [SerializeField, Range(0f, 1f)] private float _nitroFillFraction = 1.0f;

        [Header("Респавн")]
        [SerializeField] private float _respawnInterval = 8f;

        [Header("Пул пикапов по типам")]
        [SerializeField] private PickupEntry[] _entries;

        /// <summary>Доля MaxArmor, восстанавливаемая одним пикапом ремонта.</summary>
        public float RepairFraction    => _repairFraction;

        /// <summary>Доля ёмкости бака, заливаемая одним пикапом нитро.</summary>
        public float NitroFillFraction => _nitroFillFraction;

        /// <summary>Время в секундах до появления нового пикапа после подбора.</summary>
        public float RespawnInterval   => _respawnInterval;

        /// <summary>Список записей пула: тип → prefab → начальный размер пула.</summary>
        public IReadOnlyList<PickupEntry> Entries => _entries;

        [Serializable]
        public struct PickupEntry
        {
            public PickupType type;
            public GameObject prefab;
            [Tooltip("Количество инстансов, создаваемых в пуле при старте.")]
            public int poolSize;
        }
    }
}
