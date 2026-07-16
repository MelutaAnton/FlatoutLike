using FlatOutLike.Core.Events;
using FlatOutLike.Data.Configs.Camera;
using FlatOutLike.Services.Match;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using VContainer;

namespace FlatOutLike.Controllers.Camera
{
    /// <summary>
    /// Кинематографический отклик chase-камеры на нитро ИГРОКА: FOV-кик линзы +
    /// радиальный блюр по краям экрана (fullscreen render feature). Реагирует только на
    /// <see cref="NitroStateChangedEvent"/> машины игрока (ICarRegistry.Player) — у врагов
    /// нитро не даёт эффекта камере/экрану, только трейлы (см. NitroTrailsController).
    /// </summary>
    public sealed class NitroCameraEffectController : MonoBehaviour
    {
        [SerializeField] private CinemachineCamera _chaseCamera;
        [SerializeField] private NitroCameraEffectConfig _config;
        [SerializeField] private ScriptableRendererFeature[] _blurFeatures;

        // Имя uniform'а определено в NitroRadialBlur.shader; это единственное место в C#,
        // где оно резолвится (правило «строковый ключ один раз»).
        private static readonly int BlurIntensityId = Shader.PropertyToID("_NitroBlurIntensity");

        private IEventBus _eventBus;
        private ICarRegistry _carRegistry;
        private bool _initialized;
        private bool _disabled; // true, если _chaseCamera/_config не заданы — эффект тихо бездействует

        private float _intensity;
        private float _targetIntensity;
        private float _baseFov;
        private bool _baseFovCached;

        private void Awake()
        {
            // Фичи — ассеты проекта (не рантайм-объекты): их включённое состояние переживает
            // Stop/Play в редакторе. Гарантированно гасим на старте, до подписки на события.
            SetBlurFeaturesActive(false);
            Shader.SetGlobalFloat(BlurIntensityId, 0f);

            if (_chaseCamera == null || _config == null)
            {
                Debug.LogWarning("[NitroCameraEffectController] _chaseCamera/_config не заданы — эффект нитро отключён.");
                _disabled = true;
            }
        }

        [Inject]
        public void Construct(IEventBus eventBus, ICarRegistry carRegistry)
        {
            _eventBus = eventBus;
            _carRegistry = carRegistry;
            _initialized = true;
            // OnEnable срабатывает до Construct — подписываемся здесь явно (по образцу CarDamageVisualController).
            _eventBus.Subscribe<NitroStateChangedEvent>(OnNitroStateChanged);
        }

        private void OnEnable()
        {
            if (!_initialized) return;
            _eventBus.Subscribe<NitroStateChangedEvent>(OnNitroStateChanged);
        }

        private void OnDisable()
        {
            if (_eventBus == null) return;
            _eventBus.Unsubscribe<NitroStateChangedEvent>(OnNitroStateChanged);
        }

        private void OnDestroy()
        {
            SetBlurFeaturesActive(false);
            Shader.SetGlobalFloat(BlurIntensityId, 0f);

            if (_eventBus == null) return;
            _eventBus.Unsubscribe<NitroStateChangedEvent>(OnNitroStateChanged);
        }

        private void OnNitroStateChanged(NitroStateChangedEvent evt)
        {
            if (_disabled) return;
            if (_carRegistry == null || _carRegistry.Player == null) return;
            if (evt.CarId != _carRegistry.Player.CarId) return;

            _targetIntensity = evt.IsActive ? 1f : 0f;
        }

        private void Update()
        {
            if (_disabled) return;

            float prevIntensity = _intensity;
            float rate = _targetIntensity > _intensity
                ? 1f / Mathf.Max(_config.AttackTime, 0.0001f)
                : 1f / Mathf.Max(_config.ReleaseTime, 0.0001f);
            _intensity = Mathf.MoveTowards(_intensity, _targetIntensity, rate * Time.deltaTime);

            ApplyFov();
            Shader.SetGlobalFloat(BlurIntensityId, _intensity * _config.BlurStrength);

            // Renderer-фичи переключаем только при смене состояния «эффект есть / эффекта нет»,
            // не каждый кадр — SetActive на фиче форсит пересборку рендер-графа.
            bool wasOn = prevIntensity > 0f;
            bool isOn = _intensity > 0f;
            if (wasOn != isOn)
                SetBlurFeaturesActive(isOn);
        }

        private void ApplyFov()
        {
            if (_chaseCamera == null) return;

            if (_intensity > 0f && !_baseFovCached)
            {
                // Ленивое кэширование при первом включении эффекта: Awake остальных vcam-адаптеров
                // (CinemachineChaseCameraBehaviour.ApplyConfig) к этому моменту уже применил
                // «спокойный» FOV из ChaseCameraConfig — читаем именно его, а не хардкод.
                _baseFov = _chaseCamera.Lens.FieldOfView;
                _baseFovCached = true;
            }

            if (!_baseFovCached) return; // эффект ещё ни разу не включался — линзу не трогаем

            var lens = _chaseCamera.Lens;
            if (_intensity <= 0f)
            {
                lens.FieldOfView = _baseFov;
                _chaseCamera.Lens = lens;
                _baseFovCached = false; // сброс кэша на случай смены конфига камеры сцены
                return;
            }

            lens.FieldOfView = _baseFov + _config.FovBoost * _intensity;
            _chaseCamera.Lens = lens;
        }

        private void SetBlurFeaturesActive(bool active)
        {
            if (_blurFeatures == null) return;
            foreach (var feature in _blurFeatures)
            {
                if (feature == null) continue;
                feature.SetActive(active);
            }
        }
    }
}
