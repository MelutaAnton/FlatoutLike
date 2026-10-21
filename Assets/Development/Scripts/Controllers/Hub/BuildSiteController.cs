using FlatOutLike.Core.Events;
using FlatOutLike.Data.Configs.Progression;
using FlatOutLike.Models;
using FlatOutLike.Services.Progression;
using FlatOutLike.Services.Vfx;
using UnityEngine;
using VContainer;

namespace FlatOutLike.Controllers.Hub
{
    /// <summary>
    /// Площадка здания BaseHub (корень <c>Buildings/&lt;Id&gt;</c>, game-design §6): показывает
    /// визуальную стадию стройки — свап дочерних объектов. Индекс в _stageVisuals = число
    /// завершённых этапов (длина = StageCount + 1: [0] — пустырь, последний — готовое здание);
    /// у построенного со старта — один элемент. Источник состояния — IBuildProgressService,
    /// смена стадии — по BuildingStageInvestedEvent. Только рендер: SetActive дочерних визуалов,
    /// «поп» масштаба нового визуала и VFX смены стадии / завершения здания (шаг 12b).
    /// Сценный объект — InjectGameObject в build callback scope (заодно инжектит дочерние зоны).
    /// </summary>
    public sealed class BuildSiteController : MonoBehaviour
    {
        [SerializeField] private BuildingId _buildingId;
        [SerializeField] private GameObject[] _stageVisuals;

        private IBuildProgressService _build;
        private IEventBus _eventBus;
        private IVfxService _vfx;
        // Параметры попа и высота VFX завершения — из SO-каталога (одно место на все площадки).
        private BuildingCatalogConfig _catalog;
        private bool _initialized;
        private bool _subscribed;

        // Базовые localScale визуалов (кэш в Construct) и состояние активного попа.
        private Vector3[] _baseScales = System.Array.Empty<Vector3>();
        private int _popIndex = -1;
        private float _popElapsed;

        public BuildingId BuildingId => _buildingId;

        [Inject]
        public void Construct(IBuildProgressService build, IEventBus eventBus, IVfxService vfx,
            BuildingCatalogConfig catalog)
        {
            _build = build;
            _eventBus = eventBus;
            _vfx = vfx;
            _catalog = catalog;
            _initialized = true;

            CacheBaseScales();

            // У построенного со старта (этапов нет) — всегда последний визуал.
            int stage = _build.GetTotalStages(_buildingId) == 0
                ? LastIndex()
                : _build.GetCompletedStages(_buildingId);
            ShowStage(stage); // загрузка состояния — без попа и VFX

            // OnEnable мог сработать до Construct — подписываемся здесь явно (повтор в OnEnable
            // отсекает _subscribed). isActiveAndEnabled не годится: при сборке контейнера (Awake
            // scope) он ещё false до OnEnable объекта.
            if (enabled && gameObject.activeInHierarchy) Subscribe();
        }

        private void OnEnable()
        {
            // Guard: OnEnable срабатывает до Construct — ничего не делаем, пока не инициализированы.
            if (!_initialized) return;
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
            // Поп не должен оставить визуал в промежуточном масштабе.
            FinishPop();
        }

        private void OnDestroy() => Unsubscribe();

        private void Update()
        {
            if (_popIndex < 0) return;

            var visual = _stageVisuals[_popIndex];
            float duration = _catalog.StagePopDuration;
            if (visual == null || duration <= 0f)
            {
                FinishPop();
                return;
            }

            // Игровое время: пауза хаба (timeScale 0) замораживает поп — это допустимо.
            _popElapsed += Time.deltaTime;
            float t = _popElapsed / duration;
            if (t >= 1f)
            {
                FinishPop();
                return;
            }

            visual.transform.localScale =
                _baseScales[_popIndex] * StagePopCurve.Evaluate(
                    t, _catalog.StagePopStartScale, _catalog.StagePopOvershoot);
        }

        private void OnStageInvested(BuildingStageInvestedEvent e)
        {
            if (e.Id != _buildingId) return;

            int active = ShowStage(e.CompletedStages);
            StartPop(active);

            // На финальном этапе играет только VFX завершения здания (OnBuildingCompleted).
            if (e.CompletedStages < e.TotalStages && _vfx != null)
                _vfx.PlayEffect(VfxEffectType.BuildStageComplete, transform.position, Quaternion.identity);
        }

        private void OnBuildingCompleted(BuildingCompletedEvent e)
        {
            if (e.Id != _buildingId || _vfx == null) return;

            _vfx.PlayEffect(VfxEffectType.BuildingCompleted,
                transform.position + Vector3.up * _catalog.CompletedVfxHeight,Quaternion.identity);
        }

        private void CacheBaseScales()
        {
            int count = _stageVisuals != null ? _stageVisuals.Length : 0;
            _baseScales = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                var visual = _stageVisuals[i];
                _baseScales[i] = visual != null ? visual.transform.localScale : Vector3.one;
            }
        }

        /// <summary>Запускает поп визуала index; предыдущий незавершённый поп сбрасывается к базовому масштабу.</summary>
        private void StartPop(int index)
        {
            FinishPop();

            if (index < 0 || index >= _stageVisuals.Length || _stageVisuals[index] == null) return;
            // Длительность ≤ 0 — поп выключен: визуал остаётся в базовом масштабе.
            if (_catalog.StagePopDuration <= 0f) return;

            _popIndex = index;
            _popElapsed = 0f;
            _stageVisuals[index].transform.localScale =
                _baseScales[index] * StagePopCurve.Evaluate(
                    0f, _catalog.StagePopStartScale, _catalog.StagePopOvershoot);
        }

        /// <summary>Завершает активный поп: визуалу возвращается ровно базовый масштаб.</summary>
        private void FinishPop()
        {
            if (_popIndex < 0) return;

            var visual = _stageVisuals[_popIndex];
            if (visual != null) visual.transform.localScale = _baseScales[_popIndex];
            _popIndex = -1;
        }

        /// <summary>
        /// Включает только визуал стадии index (клампится); null-элементы пропускаются.
        /// Возвращает индекс активного визуала или -1, если визуалов нет.
        /// </summary>
        private int ShowStage(int index)
        {
            if (_stageVisuals == null || _stageVisuals.Length == 0) return -1;

            int active = Mathf.Clamp(index, 0, _stageVisuals.Length - 1);
            for (int i = 0; i < _stageVisuals.Length; i++)
            {
                var visual = _stageVisuals[i];
                if (visual == null) continue;

                bool show = i == active;
                if (visual.activeSelf != show) visual.SetActive(show);
            }

            return active;
        }

        private int LastIndex() =>
            _stageVisuals != null && _stageVisuals.Length > 0 ? _stageVisuals.Length - 1 : 0;

        private void Subscribe()
        {
            if (_subscribed || _eventBus == null) return;

            _eventBus.Subscribe<BuildingStageInvestedEvent>(OnStageInvested);
            _eventBus.Subscribe<BuildingCompletedEvent>(OnBuildingCompleted);
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed || _eventBus == null) return;

            _eventBus.Unsubscribe<BuildingStageInvestedEvent>(OnStageInvested);
            _eventBus.Unsubscribe<BuildingCompletedEvent>(OnBuildingCompleted);
            _subscribed = false;
        }
    }
}
