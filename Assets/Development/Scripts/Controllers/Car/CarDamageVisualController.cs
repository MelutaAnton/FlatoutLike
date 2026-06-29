using System.Collections.Generic;
using FlatOutLike.Core.Events;
using FlatOutLike.Data.Configs.Car;
using FlatOutLike.Models;
using FlatOutLike.Services.Combat;
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
        [SerializeField] private MeshFilter _bodyMeshFilter;             // опционально: Стратегия B
        [SerializeField] private SkinnedMeshRenderer _skinnedRenderer;  // опционально: Стратегия C
        [SerializeField] private DetachablePart[] _detachableParts;

        private IEventBus _eventBus;
        private IVfxService _vfx;
        private IDamageStageResolver _stageResolver;
        private bool _initialized;
        private int _currentStageIndex = -1;

        // Непрерывный shader-драйвер (_DamageAmount в FlatOutLike/CarDamageLit).
        private static readonly int DamageAmountId = Shader.PropertyToID("_DamageAmount");
        private float _targetDamage01;
        private float _currentDamage01;
        private MaterialPropertyBlock _mpb;
        private Renderer[] _damageRenderers; // body-рендереры, получающие _DamageAmount
        private HashSet<Material> _preserved; // стекло/фары — исключены из _damageRenderers

        [Inject]
        public void Construct(IEventBus eventBus, IVfxService vfx, IDamageStageResolver stageResolver)
        {
            _eventBus = eventBus;
            _vfx = vfx;
            _stageResolver = stageResolver;
            _initialized = true;
            // OnEnable срабатывает до Construct на старте сцены, поэтому подписываемся здесь явно.
            _eventBus.Subscribe<CarDamagedEvent>(OnCarDamaged);

            CollectBodyRenderers();
        }

        /// <summary>
        /// Собирает _damageRenderers: body-рендереры, которые получают непрерывное
        /// shader-свойство _DamageAmount. Исключает:
        ///   (a) Поддеревья под Transform'ами визуалов колёс (у шин свои материалы).
        ///   (b) Компоненты ParticleSystemRenderer (VFX, не body).
        ///   (c) Рендереры, у которых ВСЕ исходные слоты материалов в _preserved (стекло, фары).
        /// </summary>
        private void CollectBodyRenderers()
        {
            // Сначала собираем множество preserved-материалов, чтобы проверка ниже могла его использовать.
            _preserved = new HashSet<Material>();
            var preservedList = _visualConfig != null ? _visualConfig.PreservedMaterials : null;
            if (preservedList != null)
                foreach (var m in preservedList)
                    if (m != null) _preserved.Add(m);

            var all = GetComponentsInChildren<Renderer>(true);
            var wheelVisuals = _car != null ? _car.WheelVisuals : null;

            var damageList = new List<Renderer>(all.Length);
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

                // Исключаем полностью preserved-рендереры (стекло, фары — все слоты в preserved).
                if (_preserved.Count > 0)
                {
                    var slots = r.sharedMaterials;
                    bool allPreserved = slots.Length > 0;
                    foreach (var m in slots)
                        if (!_preserved.Contains(m)) { allPreserved = false; break; }
                    if (allPreserved) continue;
                }

                damageList.Add(r);
            }

            _damageRenderers = damageList.ToArray();
            _mpb = new MaterialPropertyBlock();
        }

        private void Update()
        {
            if (!_initialized || _damageRenderers == null || _damageRenderers.Length == 0
                || _visualConfig == null) return;

            _currentDamage01 = Mathf.MoveTowards(
                _currentDamage01, _targetDamage01,
                _visualConfig.DamageLerpSpeed * Time.deltaTime);

            float shaderVal = _visualConfig.DamageResponse != null
                ? _visualConfig.DamageResponse.Evaluate(_currentDamage01)
                : _currentDamage01;

            foreach (var r in _damageRenderers)
            {
                r.GetPropertyBlock(_mpb);
                _mpb.SetFloat(DamageAmountId, shaderVal);
                r.SetPropertyBlock(_mpb);
            }
        }

        private void OnEnable()
        {
            // Guard: OnEnable срабатывает до Construct — ничего не делаем, пока не инициализированы.
            // При повторном включении после OnDisable — переподписываемся.
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

            // Лог для проверки шага 3 (HUD ещё нет); убрать на шаге 8, когда появится DerbyHudView.
            Debug.Log($"[Damage] {e.Target} HP {e.RemainingHp:F0}/{maxArmor:F0} (-{e.Damage:F0})");

            float ratio = e.RemainingHp / maxArmor;
            _targetDamage01 = Mathf.Clamp01(1f - ratio); // непрерывно гонит шейдер, независимо от стадии

            int newIndex = _stageResolver.ResolveStageIndex(_visualConfig.Stages, ratio);

            if (newIndex == _currentStageIndex) return;

            // goingDeeper: переходим в более повреждённую стадию (напр., Pristine→Dented).
            // Используется как guard для одноразовых эффектов (decals, VFX), чтобы они не повторялись при восстановлении HP.
            bool goingDeeper = newIndex > _currentStageIndex;
            _currentStageIndex = newIndex;

            // Лог для проверки шага 3; убрать на шаге 8 (HUD).
            Debug.Log($"[Damage] {e.Target} stage → {newIndex} (ratio {ratio:P0}, {(goingDeeper ? "worse" : "repaired")})");

            if (newIndex >= 0 && newIndex < _visualConfig.Stages.Count)
                ApplyStage(_visualConfig.Stages[newIndex], goingDeeper);
        }

        /// <param name="goingDeeper">True при входе в более повреждённую стадию; false при восстановлении HP.</param>
        private void ApplyStage(CarDamageVisualConfig.DamageStage stage, bool goingDeeper)
        {
            // --- Идемпотентные сеттеры состояния: применяются на ЛЮБОЙ смене стадии (включая восстановление HP) ---
            // Стратегия A (дискретная подмена материалов) удалена — визуализация повреждений теперь
            // непрерывно гонится через shader-свойство _DamageAmount в Update(). См. _damageRenderers.

            // Стратегия B: подмена меша
            if (stage.damagedMesh != null && _bodyMeshFilter != null)
                _bodyMeshFilter.sharedMesh = stage.damagedMesh;

            // Стратегия C: веса blend shape
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

            // Переключение отрываемых деталей (идемпотентно: только проставляет joint breakForce)
            if (_detachableParts != null)
            {
                foreach (var part in _detachableParts)
                {
                    if (part != null)
                        part.SetBreakable(stage.enableDetachableParts);
                }
            }

            // --- Одноразовые эффекты: только при входе в более глубокую (более повреждённую) стадию ---
            // Предотвращает дублирование decals / VFX при восстановлении HP (repair pickup из шага 5).
            if (!goingDeeper) return;

            // Decals: создаём как постоянных детей (не из Pool)
            if (stage.decalPrefabsToSpawn != null)
            {
                foreach (var decalPrefab in stage.decalPrefabsToSpawn)
                {
                    if (decalPrefab != null)
                        Instantiate(decalPrefab, transform);
                }
            }

            // VFX: проигрываем через Service (контроллеры никогда не вызывают IVfxFactory напрямую)
            if (stage.vfxToActivate != null && _vfx != null)
            {
                foreach (var vfxType in stage.vfxToActivate)
                    _vfx.PlayEffect(vfxType, transform.position, transform.rotation);
            }
        }
    }
}
