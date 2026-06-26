using System.Collections.Generic;
using FlatOutLike.Core.Events;
using FlatOutLike.Data.Configs.Car;
using FlatOutLike.Models;
using FlatOutLike.Services.Vfx;
using UnityEngine;
using VContainer;

namespace FlatOutLike.Controllers.Car
{
    public sealed class CarDamageVisualController : MonoBehaviour
    {
        [SerializeField] private CarStatsConfig _statsConfig;
        [SerializeField] private CarDamageVisualConfig _visualConfig;
        [SerializeField] private CarController _car;
        [SerializeField] private MeshFilter _bodyMeshFilter;             // optional: Strategy B
        [SerializeField] private SkinnedMeshRenderer _skinnedRenderer;  // optional: Strategy C
        [SerializeField] private DetachablePart[] _detachableParts;

        private IEventBus _eventBus;
        private IVfxService _vfx;
        private bool _initialized;
        private int _currentStageIndex = -1;

        // Strategy A runtime data — collected in Construct, not serialized.
        private Renderer[] _bodyRenderers;
        private Material[][] _originalMaterials;
        private HashSet<Material> _preserved;

        [Inject]
        public void Construct(IEventBus eventBus, IVfxService vfx)
        {
            _eventBus = eventBus;
            _vfx = vfx;
            _initialized = true;
            // OnEnable fires before Construct on scene start, so we subscribe here explicitly.
            _eventBus.Subscribe<CarDamagedEvent>(OnCarDamaged);

            CollectBodyRenderers();
        }

        /// <summary>
        /// Collects all Renderers in the car hierarchy, excluding:
        ///   (a) Subtrees rooted at wheel visual Transforms (tyres have their own materials).
        ///   (b) ParticleSystemRenderer components (VFX, not body).
        /// Results are stored in _bodyRenderers; original materials cached in _originalMaterials.
        /// </summary>
        private void CollectBodyRenderers()
        {
            var all = GetComponentsInChildren<Renderer>(true);
            var wheelVisuals = _car != null ? _car.WheelVisuals : null;

            var result = new List<Renderer>(all.Length);
            foreach (var r in all)
            {
                if (r is ParticleSystemRenderer) continue;

                if (wheelVisuals != null)
                {
                    bool isWheelChild = false;
                    foreach (var wv in wheelVisuals)
                    {
                        if (wv != null && r.transform.IsChildOf(wv))
                        {
                            isWheelChild = true;
                            break;
                        }
                    }
                    if (isWheelChild) continue;
                }

                result.Add(r);
            }

            _bodyRenderers = result.ToArray();

            _originalMaterials = new Material[_bodyRenderers.Length][];
            for (int i = 0; i < _bodyRenderers.Length; i++)
                _originalMaterials[i] = _bodyRenderers[i].sharedMaterials;

            // Build preserved-material set from config (glass, headlights, etc.).
            // Compared against original slot material so the check is stable across stage changes.
            _preserved = new HashSet<Material>();
            var preservedList = _visualConfig != null ? _visualConfig.PreservedMaterials : null;
            if (preservedList != null)
            {
                foreach (var m in preservedList)
                    if (m != null) _preserved.Add(m);
            }
        }

        private void OnEnable()
        {
            // Guard: OnEnable fires before Construct — do nothing until initialized.
            // On re-enable after OnDisable, re-subscribe.
            if (!_initialized) return;
            _eventBus.Subscribe<CarDamagedEvent>(OnCarDamaged);
        }

        private void OnDisable()
        {
            if (_eventBus == null) return;
            _eventBus.Unsubscribe<CarDamagedEvent>(OnCarDamaged);
        }

        private void OnDestroy()
        {
            if (_eventBus == null) return;
            _eventBus.Unsubscribe<CarDamagedEvent>(OnCarDamaged);
        }

        private void OnCarDamaged(CarDamagedEvent e)
        {
            if (_car == null || e.Target != _car.CarId) return;
            if (_visualConfig == null || _statsConfig == null) return;

            float maxArmor = _statsConfig.MaxArmor;
            if (maxArmor <= 0f) return;

            // Step 3 verification log (no HUD yet); remove on step 8 when DerbyHudView exists.
            Debug.Log($"[Damage] {e.Target} HP {e.RemainingHp:F0}/{maxArmor:F0} (-{e.Damage:F0})");

            float ratio = e.RemainingHp / maxArmor;
            int newIndex = FindStageIndex(ratio);

            if (newIndex == _currentStageIndex) return;

            // goingDeeper: entering a more-damaged stage (e.g. Pristine→Dented).
            // Used to guard one-time effects (decals, VFX) so they don't repeat on HP restore.
            bool goingDeeper = newIndex > _currentStageIndex;
            _currentStageIndex = newIndex;

            // Step 3 verification log; remove on step 8 (HUD).
            Debug.Log($"[Damage] {e.Target} stage → {newIndex} (ratio {ratio:P0}, {(goingDeeper ? "worse" : "repaired")})");

            if (newIndex >= 0 && newIndex < _visualConfig.Stages.Count)
                ApplyStage(_visualConfig.Stages[newIndex], goingDeeper);
        }

        /// <summary>
        /// Returns the index of the stage with the minimum hpThreshold that is still
        /// >= the current HP ratio. As HP drops, we progressively enter deeper stages.
        /// Stages must be sorted descending by hpThreshold (e.g. 1.0, 0.66, 0.33).
        /// </summary>
        private int FindStageIndex(float ratio)
        {
            var stages = _visualConfig.Stages;
            if (stages == null || stages.Count == 0) return -1;

            int result = -1;
            float minThreshold = float.MaxValue;

            for (int i = 0; i < stages.Count; i++)
            {
                float threshold = stages[i].hpThreshold;
                if (threshold >= ratio && threshold < minThreshold)
                {
                    minThreshold = threshold;
                    result = i;
                }
            }

            // Fallback: ratio is above all thresholds (e.g. freshly spawned) — use first stage.
            if (result == -1)
                result = 0;

            return result;
        }

        /// <param name="goingDeeper">True when entering a more-damaged stage; false on HP restore.</param>
        private void ApplyStage(CarDamageVisualConfig.DamageStage stage, bool goingDeeper)
        {
            // --- Idempotent state setters: apply on ANY stage change (including HP restore) ---

            // Strategy A: Material swap across the whole car body.
            if (_bodyRenderers != null && _bodyRenderers.Length > 0)
            {
                if (stage.damageMaterial != null)
                {
                    // Primary: paint damageMaterial over each slot, but keep preserved materials
                    // (glass, headlights) intact. Compare against original slot to avoid
                    // false-positives when a slot was already painted by a previous stage.
                    for (int i = 0; i < _bodyRenderers.Length; i++)
                    {
                        var orig = _originalMaterials[i];
                        var arr = new Material[orig.Length];
                        for (int s = 0; s < arr.Length; s++)
                            arr[s] = _preserved.Contains(orig[s]) ? orig[s] : stage.damageMaterial;
                        _bodyRenderers[i].sharedMaterials = arr;
                    }
                }
                else if (stage.materials != null && stage.materials.Length > 0)
                {
                    // Legacy fallback: apply materials[] to the first body renderer only.
                    _bodyRenderers[0].sharedMaterials = stage.materials;
                }
                else
                {
                    // Restore originals (e.g. returning to Pristine after repair).
                    for (int i = 0; i < _bodyRenderers.Length; i++)
                        _bodyRenderers[i].sharedMaterials = _originalMaterials[i];
                }
            }

            // Strategy B: Mesh swap
            if (stage.damagedMesh != null && _bodyMeshFilter != null)
                _bodyMeshFilter.sharedMesh = stage.damagedMesh;

            // Strategy C: Blend shape weights
            if (stage.blendShapeWeights != null && stage.blendShapeWeights.Length > 0
                && _skinnedRenderer != null && _skinnedRenderer.sharedMesh != null)
            {
                foreach (var bsw in stage.blendShapeWeights)
                {
                    int idx = _skinnedRenderer.sharedMesh.GetBlendShapeIndex(bsw.name);
                    if (idx >= 0)
                        _skinnedRenderer.SetBlendShapeWeight(idx, bsw.weight);
                    else
                        Debug.LogWarning($"[CarDamageVisual] Blend shape '{bsw.name}' not found on {_skinnedRenderer.name}");
                }
            }

            // Detachable parts toggle (idempotent: just sets joint breakForce)
            if (_detachableParts != null)
            {
                foreach (var part in _detachableParts)
                {
                    if (part != null)
                        part.SetBreakable(stage.enableDetachableParts);
                }
            }

            // --- One-time effects: only when entering a deeper (more-damaged) stage ---
            // Prevents duplicate decals / VFX when HP is restored (step 5 repair pickup).
            if (!goingDeeper) return;

            // Decals: instantiate as persistent children (not pooled)
            if (stage.decalPrefabsToSpawn != null)
            {
                foreach (var decalPrefab in stage.decalPrefabsToSpawn)
                {
                    if (decalPrefab != null)
                        Instantiate(decalPrefab, transform);
                }
            }

            // VFX: play via service (controllers never call IVfxFactory directly)
            if (stage.vfxToActivate != null && _vfx != null)
            {
                foreach (var vfxType in stage.vfxToActivate)
                    _vfx.PlayEffect(vfxType, transform.position, transform.rotation);
            }
        }
    }
}
