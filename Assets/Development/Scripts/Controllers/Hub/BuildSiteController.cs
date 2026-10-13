using FlatOutLike.Core.Events;
using FlatOutLike.Models;
using FlatOutLike.Services.Progression;
using UnityEngine;
using VContainer;

namespace FlatOutLike.Controllers.Hub
{
    /// <summary>
    /// Площадка здания BaseHub (корень <c>Buildings/&lt;Id&gt;</c>, game-design §6): показывает
    /// визуальную стадию стройки — свап дочерних объектов. Индекс в _stageVisuals = число
    /// завершённых этапов (длина = StageCount + 1: [0] — пустырь, последний — готовое здание);
    /// у построенного со старта — один элемент. Источник состояния — IBuildProgressService,
    /// смена стадии — по BuildingStageInvestedEvent. Только рендер: SetActive дочерних визуалов.
    /// Сценный объект — InjectGameObject в build callback scope (заодно инжектит дочерние зоны).
    /// </summary>
    public sealed class BuildSiteController : MonoBehaviour
    {
        [SerializeField] private BuildingId _buildingId;
        [SerializeField] private GameObject[] _stageVisuals;

        private IBuildProgressService _build;
        private IEventBus _eventBus;
        private bool _initialized;
        private bool _subscribed;

        public BuildingId BuildingId => _buildingId;

        [Inject]
        public void Construct(IBuildProgressService build, IEventBus eventBus)
        {
            _build = build;
            _eventBus = eventBus;
            _initialized = true;

            // У построенного со старта (этапов нет) — всегда последний визуал.
            int stage = _build.GetTotalStages(_buildingId) == 0
                ? LastIndex()
                : _build.GetCompletedStages(_buildingId);
            ShowStage(stage);

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

        private void OnDisable() => Unsubscribe();

        private void OnDestroy() => Unsubscribe();

        private void OnStageInvested(BuildingStageInvestedEvent e)
        {
            if (e.Id != _buildingId) return;

            ShowStage(e.CompletedStages);
        }

        /// <summary>Включает только визуал стадии index (клампится); null-элементы пропускаются.</summary>
        private void ShowStage(int index)
        {
            if (_stageVisuals == null || _stageVisuals.Length == 0) return;

            int active = Mathf.Clamp(index, 0, _stageVisuals.Length - 1);
            for (int i = 0; i < _stageVisuals.Length; i++)
            {
                var visual = _stageVisuals[i];
                if (visual == null) continue;

                bool show = i == active;
                if (visual.activeSelf != show) visual.SetActive(show);
            }
        }

        private int LastIndex() =>
            _stageVisuals != null && _stageVisuals.Length > 0 ? _stageVisuals.Length - 1 : 0;

        private void Subscribe()
        {
            if (_subscribed || _eventBus == null) return;

            _eventBus.Subscribe<BuildingStageInvestedEvent>(OnStageInvested);
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed || _eventBus == null) return;

            _eventBus.Unsubscribe<BuildingStageInvestedEvent>(OnStageInvested);
            _subscribed = false;
        }
    }
}
