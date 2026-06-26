using System;
using System.Collections.Generic;
using FlatOutLike.Models;
using UnityEngine;

namespace FlatOutLike.Data.Configs.Vfx
{
    [CreateAssetMenu(fileName = "VfxConfig", menuName = "FlatOutLike/Vfx/VfxConfig")]
    public sealed class VfxConfig : ScriptableObject
    {
        [SerializeField] private VfxEntry[] _entries;

        public IReadOnlyList<VfxEntry> Entries => _entries;

        [Serializable]
        public struct VfxEntry
        {
            public VfxEffectType type;
            public GameObject prefab;
            [Tooltip("Number of instances pre-warmed in the pool at startup.")]
            public int poolSize;
        }
    }
}
