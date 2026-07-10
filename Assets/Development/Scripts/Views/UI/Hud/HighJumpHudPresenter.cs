using System;
using FlatOutLike.Core.Events;
using FlatOutLike.Models;
using FlatOutLike.Services.Driver;
using FlatOutLike.Services.Flow;
using FlatOutLike.Services.Match;
using FlatOutLike.Services.Meta;

namespace FlatOutLike.Views.UI.Hud
{
    /// <summary>
    /// Presenter HUD «Stunts: High Jump» (pure C#, Шаг 8d). Переключает фазы Driving/Flight/Scoring
    /// по GameStateChangedEvent; живёт в сцене трамплина.
    ///
    /// Порядок событий важен: HighJumpSessionService.OnDriverLanded сперва вызывает
    /// HighJumpScoreService.ScoreAttempt (публикует StuntsScoredEvent синхронно), и только
    /// ПОТОМ переключает GameFlow на StuntsHighJumpScoring (публикует GameStateChangedEvent) —
    /// поэтому счёт кэшируется по StuntsScoredEvent и применяется на GameStateChangedEvent
    /// (паттерн MatchEndPresenter: кэш по одному событию, применение по следующему).
    ///
    /// Свежий scope не публикует стартовое GameStateChangedEvent (см. GameFlowService/Шаг 8b) —
    /// начальная фаза применяется в конструкторе из IGameFlowService.Current.
    /// </summary>
    public sealed class HighJumpHudPresenter : IDisposable
    {
        private readonly HighJumpHudView _view;
        private readonly IEventBus _eventBus;
        private readonly IGameFlowService _gameFlow;
        private readonly ICarRegistry _carRegistry;
        private readonly IDriverFlightService _flight;
        private readonly IMetaProgressService _meta;

        private GameState _currentState;
        private bool _scorePending;
        // Результат попытки = пиковая высота (композитный счёт удалён решением геймдизайна).
        private float _pendingHeight;
        private bool _pendingIsNewRecord;

        public HighJumpHudPresenter(HighJumpHudView view, IEventBus eventBus, IGameFlowService gameFlow,
            ICarRegistry carRegistry, IDriverFlightService flight, IMetaProgressService meta)
        {
            _view = view;
            _eventBus = eventBus;
            _gameFlow = gameFlow;
            _carRegistry = carRegistry;
            _flight = flight;
            _meta = meta;

            _view.OnTick += HandleTick;
            _view.OnContinueClicked += HandleContinueClicked;
            _eventBus.Subscribe<GameStateChangedEvent>(OnGameStateChanged);
            _eventBus.Subscribe<StuntsScoredEvent>(OnStuntsScored);

            ApplyPhase(_gameFlow.Current);
        }

        public void Dispose()
        {
            _view.OnTick -= HandleTick;
            _view.OnContinueClicked -= HandleContinueClicked;
            _eventBus.Unsubscribe<GameStateChangedEvent>(OnGameStateChanged);
            _eventBus.Unsubscribe<StuntsScoredEvent>(OnStuntsScored);
        }

        private void OnGameStateChanged(GameStateChangedEvent e) => ApplyPhase(e.To);

        private void ApplyPhase(GameState state)
        {
            _currentState = state;

            if (IsDrivingPhase(state))
            {
                _view.ShowDrivingPhase();
            }
            else if (IsFlightPhase(state))
            {
                _view.ShowFlightPhase();
            }
            else if (IsScoringPhase(state))
            {
                // StuntsScoredEvent приходит раньше — см. класс-комментарий про порядок событий.
                // Защитный случай (нарушение штатного порядка): без кэша показываем нулевой счёт,
                // а не мусор из предыдущей попытки.
                // Рекорд читаем из профиля ПОСЛЕ обновления сервисом: при новом рекорде
                // best == высота попытки — панель показывает обе одинаковыми, это корректно.
                float best = _meta.Profile.BestHighJumpHeight;
                if (_scorePending)
                {
                    _view.ShowScoringPhase(_pendingHeight, best, _pendingIsNewRecord);
                    _scorePending = false;
                }
                else
                {
                    _view.ShowScoringPhase(0f, best, false);
                }
            }
            // Любое другое состояние (MainMenu/GarageMeta/Paused/...) — HUD трамплина не при делах,
            // presenter просто ничего не показывает поверх текущей фазы.
        }

        private void OnStuntsScored(StuntsScoredEvent e)
        {
            if (e.Type != StuntType.HighJump) return;

            _pendingHeight = e.Height;
            _pendingIsNewRecord = e.IsNewRecord;
            _scorePending = true;
        }

        private void HandleTick()
        {
            if (IsDrivingPhase(_currentState))
            {
                _view.SetSpeed(MsToKmh(PlayerSpeed()));
            }
            else if (IsFlightPhase(_currentState))
            {
                _view.SetHeight(_flight.PeakHeight, _meta.Profile.BestHighJumpHeight);
                _view.SetBoostAvailable(_flight.HasBoostAvailable);
            }
        }

        /// <summary>
        /// Скорость машины игрока (м/с). Presenter остаётся pure C#, но допускает чтение
        /// готового публичного свойства CarController — так же, как ICarRegistry.Player уже
        /// отдаёт MonoBehaviour-ссылку наружу по контракту интерфейса.
        /// </summary>
        private float PlayerSpeed()
        {
            var player = _carRegistry.Player;
            return player != null ? player.Speed : 0f;
        }

        private void HandleContinueClicked() => _gameFlow.ChangeState(GameState.GarageMeta);

        // ── Вычислительные хелперы: публичные static ради юнит-тестов без живого View
        // (HighJumpHudView — MonoBehaviour, требует сцену/префаб — см. GaragePresenterTests). ──

        public static bool IsDrivingPhase(GameState state) => state == GameState.StuntsHighJumpDriving;
        public static bool IsFlightPhase(GameState state) => state == GameState.StuntsHighJumpFlight;
        public static bool IsScoringPhase(GameState state) => state == GameState.StuntsHighJumpScoring;

        public static float MsToKmh(float metersPerSecond) => metersPerSecond * 3.6f;
    }
}
