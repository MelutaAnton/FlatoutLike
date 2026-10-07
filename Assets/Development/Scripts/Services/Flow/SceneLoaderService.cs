using FlatOutLike.Core.Events;
using FlatOutLike.Data.Configs.Flow;
using FlatOutLike.Models;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer.Unity;

namespace FlatOutLike.Services.Flow
{
    /// <summary>
    /// Тонкая обёртка над SceneManager. Единственное место в проекте, где вызывается
    /// SceneManager.LoadScene* — межэкранные переходы идут только через ISceneLoaderService.
    /// Переход асинхронный и двухфазный (scope per-scene, без DontDestroyOnLoad): старая сцена
    /// закрывается экраном загрузки (ILoadingScreen.Show) → LoadSceneAsync(Single) с
    /// allowSceneActivation = false, прогресс ведёт LoadingProgressModel (Tick); на 100% сцена
    /// активируется. Новая сцена стартует уже закрытой (Cover в конструкторе — до первого кадра)
    /// и открывается в Start() своего SceneLoaderService.
    /// </summary>
    public sealed class SceneLoaderService : ISceneLoaderService, IStartable, ITickable
    {
        // Порог AsyncOperation.progress: при allowSceneActivation = false Unity останавливает
        // прогресс на 0.9 — сцена загружена и ждёт только разрешения на активацию.
        private const float SceneReadyProgress = 0.9f;

        private readonly ILoadingScreen _screen;
        private readonly LoadingScreenConfig _config;
        private readonly IEventBus _eventBus;

        // Загрузка начата (идёт проявление экрана или асинхронная загрузка) — повторные запросы
        // игнорируются, а Start() не открывает экран поверх начатого перехода.
        private bool _loading;

        private AsyncOperation _operation;
        private LoadingProgressModel _model;
        private bool _hintHidden;
        private bool _activationAllowed;

        public string ActiveSceneName => SceneManager.GetActiveScene().name;

        public bool IsLoading => _loading;

        public SceneLoaderService(ILoadingScreen screen, LoadingScreenConfig config, IEventBus eventBus)
        {
            _screen = screen;
            _config = config;
            _eventBus = eventBus;

            // Конструктор вызывается при сборке контейнера (Awake scope) — экран закрывает новую
            // сцену ДО первого кадра, стык с концом загрузки в старой сцене не виден.
            _screen.Cover(TitleFor(ActiveSceneName));
        }

        public void Start()
        {
            // КРИТИЧНО: в Boot GameFlowService уже при сборке контейнера вызвал Load
            // (SceneEntryState.Enter начального состояния) — переход начался ДО Start().
            // Hide здесь открыл бы Boot вместо экрана загрузки.
            if (!_loading)
                _screen.Hide();
        }

        public void Tick()
        {
            if (_operation == null)
                return;

            _model.Advance(Time.unscaledDeltaTime, _operation.progress >= SceneReadyProgress);
            _screen.SetProgress(_model.Displayed);

            if (_model.IsFinishing && !_hintHidden)
            {
                // Подсказку гасим на рывке к 100% — новая сцена стартует с Cover без подсказки.
                _hintHidden = true;
                _screen.HideHint();
            }

            if (_model.IsComplete && !_activationAllowed)
            {
                _activationAllowed = true;
                _operation.allowSceneActivation = true;
            }
        }

        public void Load(string sceneKey)
        {
            if (ActiveSceneName == sceneKey)
            {
                // Сцена уже активна (например, GameLifetimeScope стартовал прямо в ней) — повторная
                // загрузка пересоздала бы весь Composition Root и сбросила рантайм-состояние.
                Debug.Log($"[SceneLoader] Сцена '{sceneKey}' уже активна — загрузка пропущена.");
                return;
            }

            if (_loading)
            {
                // Двойной тап / повторный переход во время перехода — первая загрузка выигрывает.
                Debug.Log($"[SceneLoader] Загрузка уже идёт — запрос '{sceneKey}' пропущен.");
                return;
            }

            _loading = true;

            string title = string.Empty;
            float minSeconds = _config.DefaultMinSeconds;
            if (_config.TryGetDestination(GameStateSceneMap.InitialStateFor(sceneKey), out var destination))
            {
                title = destination.Title ?? string.Empty;
                minSeconds = destination.MinSeconds;
            }

            Debug.Log($"[SceneLoader] Загрузка сцены '{sceneKey}' (через экран загрузки).");
            _screen.Show(title, PickHint(), () => BeginAsyncLoad(sceneKey, minSeconds));
        }

        private void BeginAsyncLoad(string sceneKey, float minSeconds)
        {
            var operation = SceneManager.LoadSceneAsync(sceneKey, LoadSceneMode.Single);
            if (operation == null)
            {
                // Сцена не найдена в Build Settings — SceneManager уже залогировал ошибку.
                // Снимаем блокировку и открываем текущую сцену, чтобы игрок не застрял на экране
                // загрузки. SceneTransitionCoveredEvent не публикуем — старая сцена остаётся живой.
                Debug.LogError($"[SceneLoader] Не удалось начать загрузку сцены '{sceneKey}' — экран загрузки снят.");
                _loading = false;
                _screen.Hide();
                return;
            }

            // Активацию держим до 100% на полосе — иначе сцена переключится посреди анимации.
            operation.allowSceneActivation = false;
            _model = new LoadingProgressModel(
                _config.FakeTarget, _config.FakeSeconds,
                _config.CreepTarget, _config.CreepSeconds,
                _config.FinishSeconds, minSeconds);
            _hintHidden = false;
            _activationAllowed = false;
            _operation = operation;

            _eventBus.Publish(new SceneTransitionCoveredEvent());
        }

        private string TitleFor(string sceneName)
        {
            var state = GameStateSceneMap.InitialStateFor(sceneName);
            return _config.TryGetDestination(state, out var destination) && destination.Title != null
                ? destination.Title
                : string.Empty;
        }

        private string PickHint()
        {
            var hints = _config.Hints;
            if (hints.Count == 0)
                return string.Empty;

            return hints[UnityEngine.Random.Range(0, hints.Count)] ?? string.Empty;
        }
    }
}
