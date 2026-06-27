using System;
using System.Collections.Generic;
using FlatOutLike.Models;
using UnityEngine;

namespace FlatOutLike.Data.Configs.Car
{
    [CreateAssetMenu(fileName = "CarDamageVisualConfig", menuName = "FlatOutLike/Car/CarDamageVisualConfig")]
    public sealed class CarDamageVisualConfig : ScriptableObject
    {
        [SerializeField] private DamageStage[] _stages;

        /// <summary>
        /// Материалы, которые никогда не перезаписываются damageMaterial (например, стекло, фары).
        /// Сравнение идёт по ссылке на исходный материал слота, а не по текущему состоянию renderer.
        /// </summary>
        [SerializeField] private Material[] _preservedMaterials;

        [Header("Continuous Shader Driver")]
        [Tooltip("Speed at which _DamageAmount lerps toward the target value (units/sec).")]
        [SerializeField] private float _damageLerpSpeed = 3f;
        [Tooltip("Maps normalised HP-loss (0=full HP, 1=dead) to _DamageAmount fed to the shader. Default: linear.")]
        [SerializeField] private AnimationCurve _damageResponse = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        public IReadOnlyList<DamageStage> Stages => _stages;
        public IReadOnlyList<Material> PreservedMaterials => _preservedMaterials;
        public float DamageLerpSpeed => _damageLerpSpeed;
        public AnimationCurve DamageResponse => _damageResponse;

        /// <summary>
        /// Стадия повреждения задаёт набор визуальных стратегий, применяемых параллельно,
        /// когда HP падает до hpThreshold или ниже. Пустые/null-поля игнорируются.
        /// Стадии должны идти по убыванию hpThreshold (например, 1.0, 0.66, 0.33).
        /// </summary>
        [Serializable]
        public struct DamageStage
        {
            [Tooltip("Normalized HP fraction (0..1). Stage activates when HP ratio falls to or below this value.")]
            public float hpThreshold;

            [Header("Strategy A: Material / Texture Swap")]
            [Tooltip("Single damage material painted over ALL slots of ALL body renderers. Null = use materials[] or restore originals.")]
            public Material damageMaterial;
            [Tooltip("Legacy fallback: applied only to bodyRenderers[0] when damageMaterial is null. Null = restore originals.")]
            public Material[] materials;
            [Tooltip("Decal prefabs to Instantiate as persistent children of the car.")]
            public GameObject[] decalPrefabsToSpawn;

            [Header("Strategy B: Mesh Swap")]
            [Tooltip("Replacement mesh for the body MeshFilter. Null = no change.")]
            public Mesh damagedMesh;

            [Header("Strategy C: Blend Shapes")]
            [Tooltip("Named blend shape weights to apply on a SkinnedMeshRenderer.")]
            public BlendShapeWeight[] blendShapeWeights;

            [Header("Common")]
            [Tooltip("VFX effects to trigger via IVfxService when this stage is entered.")]
            public VfxEffectType[] vfxToActivate;
            [Tooltip("When true, DetachablePart joints have finite break force.")]
            public bool enableDetachableParts;
        }

        [Serializable]
        public struct BlendShapeWeight
        {
            [Tooltip("Blend shape name as exported from Blender/DCC tool.")]
            public string name;
            [Range(0f, 100f)]
            public float weight;
        }
    }
}
