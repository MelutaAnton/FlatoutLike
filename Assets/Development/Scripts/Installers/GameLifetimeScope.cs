using FlatOutLike.Controllers.Audio;
using FlatOutLike.Controllers.Car;
using FlatOutLike.Controllers.Camera;
using FlatOutLike.Controllers.Destructible;
using FlatOutLike.Controllers.Hub;
using FlatOutLike.Controllers.Mobile;
using FlatOutLike.Controllers.Stunts;
using FlatOutLike.Core.Events;
using FlatOutLike.Core.Input;
using FlatOutLike.Core.Pool;
using FlatOutLike.Data.Configs.Audio;
using FlatOutLike.Data.Configs.Camera;
using FlatOutLike.Data.Configs.Car;
using FlatOutLike.Data.Configs.Combat;
using FlatOutLike.Data.Configs.Driver;
using FlatOutLike.Data.Configs.Flow;
using FlatOutLike.Data.Configs.Hub;
using FlatOutLike.Data.Configs.Input;
using FlatOutLike.Data.Configs.Match;
using FlatOutLike.Data.Configs.Pickups;
using FlatOutLike.Data.Configs.Progression;
using FlatOutLike.Data.Configs.Rewards;
using FlatOutLike.Data.Configs.Stunts;
using FlatOutLike.Data.Configs.Upgrades;
using FlatOutLike.Data.Configs.Vfx;
using FlatOutLike.Factories.Car;
using FlatOutLike.Factories.Destructible;
using FlatOutLike.Factories.Pickup;
using FlatOutLike.Factories.Vfx;
using FlatOutLike.Models;
using FlatOutLike.Services.AI;
using FlatOutLike.Services.Audio;
using FlatOutLike.Services.Camera;
using FlatOutLike.Services.Car;
using FlatOutLike.Services.Combat;
using FlatOutLike.Services.Driver;
using FlatOutLike.Services.Flow;
using FlatOutLike.Services.Hub;
using FlatOutLike.Services.Match;
using FlatOutLike.Services.Meta;
using FlatOutLike.Services.Pickups;
using FlatOutLike.Services.Stunts;
using FlatOutLike.Services.Vfx;
using FlatOutLike.Views.UI.Common;
using FlatOutLike.Views.UI.Garage;
using FlatOutLike.Views.UI.Hub;
using FlatOutLike.Views.UI.Hud;
using FlatOutLike.Views.UI.Overlay;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;
using VContainer.Unity;

namespace FlatOutLike.Installers
{
    public sealed class GameLifetimeScope : LifetimeScope
    {
        [SerializeField] private DamageConfig    _damageConfig;
        [SerializeField] private VfxConfig       _vfxConfig;

        /// <summary>
        /// Корневые GameObject всех машин в сцене (игрок + враги).
        /// Используется только в _TestDriving-сцене (ручной спавн).
        /// Заполняется editor-скриптом "Fill GameLifetimeScope Refs".
        /// VContainer внедряет все компоненты (CarController, EnemyCarController,
        /// CarDamageVisualController) через InjectGameObject в build callback.
        /// </summary>
        [SerializeField] private GameObject[] _sceneCars;

        // ── Шаг 5a: Derby-матч ─────────────────────────────────────────────────
        /// <summary>
        /// Конфиг Derby-матча (игрок + враги + точки спавна).
        /// Заполняется на арене; в _TestDriving оставить null.
        /// </summary>
        [SerializeField] private DerbyMatchConfig _derbyMatchConfig;

        /// <summary>
        /// Конфиг наград за Derby-матч (монеты за килл/победу).
        /// Заполняется на арене вместе с _derbyMatchConfig; в _TestDriving оставить null.
        /// </summary>
        [SerializeField] private RewardConfig _rewardConfig;

        /// <summary>
        /// Точки спавна машин для Derby-матча. Index 0 = игрок, 1.. = враги.
        /// Создаются editor-скриптом на арене.
        /// </summary>
        [SerializeField] private Transform[] _spawnPoints;

        // ── Шаг 6: Eject + камера водителя ──────────────────────────────────────
        /// <summary>
        /// Camera rig арены — переключатель между chase-камерой (следование за машиной)
        /// и orbit-камерой (вокруг катапультированного водителя). Заполняется editor-скриптом;
        /// в тест-сценах без камеры оставить null.
        /// </summary>
        [SerializeField] private CameraRig _cameraRig;

        /// <summary>
        /// Параметры импульса катапультирования водителя. В тест-сценах без ragdoll — null:
        /// тогда в контейнер регистрируется fallback-инстанс (см. Configure) — VContainer
        /// не умеет опциональные параметры инъекции.
        /// </summary>
        [SerializeField] private DriverEjectConfig _driverEjectConfig;

        /// <summary>
        /// Параметры орбитальной камеры вокруг катапультированного водителя.
        /// В тест-сценах без камеры оставить null.
        /// </summary>
        [SerializeField] private OrbitCameraConfig _orbitCameraConfig;

        /// <summary>
        /// Тряска камеры (Cinemachine Impulse) от столкновений машин в дерби.
        /// Оба поля условные: заполняются вместе на риге арены; в тест-сценах без рига
        /// оставить null — CameraShakeService не регистрируется.
        /// </summary>
        [SerializeField] private CameraShakeConfig _cameraShakeConfig;
        [SerializeField] private CinemachineCameraShaker _cameraShaker;

        /// <summary>
        /// FOV-кик + радиальный блюр chase-камеры на нитро игрока. Условная регистрация
        /// (паттерн _cameraShaker) — в тест-сценах без камеры оставить null.
        /// </summary>
        [SerializeField] private NitroCameraEffectController _nitroCameraEffectController;

        // ── Шаг 5c: Pickup-система ─────────────────────────────────────────────
        /// <summary>
        /// Конфиг пикапов: типы, prefab'ы пула, размеры пула, тайминги.
        /// Заполняется editor-скриптом; на сценах без пикапов оставить null.
        /// </summary>
        [SerializeField] private PickupConfig _pickupConfig;

        /// <summary>
        /// Зоны спавна пикапов на арене (Transform-точка + тип пикапа).
        /// Создаются editor-скриптом; порядок не важен.
        /// </summary>
        [SerializeField] private PickupZone[] _pickupZones;

        // ── Шаг 7: Stunts High Jump ──────────────────────────────────────────────
        /// <summary>
        /// Параметры управления водителем в полёте (steer-коррекция, буст). Регистрируется
        /// БЕЗУСЛОВНО (fallback-инстанс, паттерн DriverEjectConfig) — DriverFlightController
        /// живёт на префабе водителя и инжектится даже в Derby-сценах без трамплина.
        /// </summary>
        [SerializeField] private DriverFlightConfig _driverFlightConfig;

        /// <summary>
        /// Правила подсчёта очков «High Jump». Заполняется на сцене трамплина;
        /// на сценах без него оставить null — сессия и скоринг не регистрируются.
        /// </summary>
        [SerializeField] private HighJumpRulesConfig _highJumpRulesConfig;

        // ── Шаг 8a: Meta (экономика и профиль) ───────────────────────────────────
        /// <summary>
        /// Каталог машин Garage. Регистрируется БЕЗУСЛОВНО (fallback-инстанс, паттерн
        /// DriverEjectConfig) — пустой каталог переводит CarFactory в фоллбэк на
        /// DerbyMatchConfig.PlayerCarPrefab/PlayerStats, HighJump-сцена работает без каталога.
        /// </summary>
        [SerializeField] private CarCatalogConfig _carCatalogConfig;

        /// <summary>
        /// Каталог апгрейдов Garage. Регистрируется БЕЗУСЛОВНО — пустой каталог даёт нулевые
        /// бонусы (GetMaxLevel всегда 0), CarStatsService возвращает базовые статы без клона.
        /// </summary>
        [SerializeField] private UpgradeCatalogConfig _upgradeCatalogConfig;

        // ── Шаг 8c: UI меты (MVP) ─────────────────────────────────────────────────
        /// <summary>
        /// Каталог трюков. Регистрируется БЕЗУСЛОВНО (fallback-инстанс, паттерн
        /// CarCatalogConfig/UpgradeCatalogConfig) — пустой каталог просто не даёт трюков,
        /// сцены без него не падают.
        /// </summary>
        [SerializeField] private StuntsCatalogConfig _stuntsCatalogConfig;

        /// <summary>
        /// View экрана Garage. Заполняется editor-скриптом сборки Canvas на своей сцене;
        /// на остальных сценах поле остаётся null — Presenter тогда не регистрируется
        /// (условно, без warning-спама — это норма для чужих сцен, не ошибка).
        /// </summary>
        [SerializeField] private GarageView _garageView;

        /// <summary>
        /// Оверлей результата Derby-матча. Живёт в Derby-сцене (не в мета-сценах!) — его
        /// регистрация не должна зависеть от каталогов Garage/Stunts.
        /// </summary>
        [SerializeField] private MatchEndView _matchEndView;

        // ── Шаг 8d: игровые HUD (Derby / Stunts HighJump) ────────────────────────
        /// <summary>
        /// HUD Derby-матча. Живёт в Derby-сцене, зависит только от IEventBus/IDamageService/
        /// INitroService/ICarRegistry (все регистрируются безусловно) — паттерн MatchEndView.
        /// </summary>
        [SerializeField] private DerbyHudView _derbyHudView;

        /// <summary>
        /// HUD трюка «Stunts: High Jump». Живёт на сцене трамплина, зависит от IDriverFlightService/
        /// IMetaProgressService/ICarRegistry/IGameFlowService (все регистрируются безусловно).
        /// </summary>
        [SerializeField] private HighJumpHudView _highJumpHudView;

        // ── Шаг 9: мобильный ввод ─────────────────────────────────────────────────
        /// <summary>
        /// Общий ассет схемы управления (один на весь проект). Прошивается editor-меню
        /// "Wire Input Config (All Scenes)". См. <see cref="InputConfig"/>.
        /// </summary>
        [SerializeField] private InputConfig _inputConfig;

        /// <summary>
        /// Canvas мобильного управления (стики + кнопки). Заполняется editor-скриптом на
        /// мобильных сценах; регистрируется только в mobile-ветке выбора IInputProvider.
        /// </summary>
        [SerializeField] private MobileInputCanvas _mobileInputCanvas;

        // ── Шаг 10a: Pause ────────────────────────────────────────────────────────
        /// <summary>
        /// Оверлей паузы. Живёт в геймплейных сценах (Derby/HighJump) и в BaseHub, зависит только от
        /// IEventBus/IGameFlowService (безусловны) — паттерн MatchEndView. На сценах без него
        /// (Garage) поле остаётся null, Presenter не регистрируется.
        /// </summary>
        [SerializeField] private PauseOverlayView _pauseOverlayView;

        // ── Звук (Derby + High Jump + мета-UI) ────────────────────────────────────
        /// <summary>
        /// Конфиг звука: SFX/музыка/маппинг GameState→трек. Регистрируется БЕЗУСЛОВНО
        /// (fallback-инстанс, паттерн _damageConfig) — до импорта звукового ассета все
        /// массивы клипов пустые, AudioService обязан молча переживать это на любой сцене.
        /// </summary>
        [SerializeField] private AudioConfig _audioConfig;

        /// <summary>
        /// Реальный исполнитель звука (дочерние AudioSource). Условная регистрация
        /// (паттерн _cameraShaker): если не прошит — регистрируется NullAudioPlayer,
        /// AudioService на сценах без него просто не издаёт звук.
        /// </summary>
        [SerializeField] private AudioPlayerController _audioPlayerController;

        /// <summary>
        /// Вешает клик-звук на все кнопки сцены. Условная регистрация — на сценах без UI
        /// (пустые тест-сцены вождения) не прошивается.
        /// </summary>
        [SerializeField] private UiClickSoundRelay _uiClickSoundRelay;

        // ── Шаг 11a: BaseHub (персонаж на базе) ──────────────────────────────────
        /// <summary>
        /// Параметры бега персонажа BaseHub. Регистрируется БЕЗУСЛОВНО (fallback-инстанс,
        /// паттерн DriverEjectConfig) — на сценах без хаба поле не прошито, а RegisterInstance(null)
        /// бросает NRE при построении контейнера.
        /// </summary>
        [SerializeField] private HubCharacterConfig _hubCharacterConfig;

        /// <summary>
        /// Персонаж BaseHub (MonoBehaviour, нужна инъекция IInputProvider/HubCharacterConfig —
        /// поэтому RegisterComponent). Живёт только в сцене BaseHub; на остальных null —
        /// не регистрируется.
        /// </summary>
        [SerializeField] private HubCharacterController _hubCharacter;

        // ── Шаг 11b: зоны запуска, HUD хаба, PLAY ────────────────────────────────
        /// <summary>
        /// Каталог построек BaseHub (зоны запуска режимов, таймер подтверждения). Регистрация
        /// УСЛОВНАЯ: без каталога HubLaunchService и LaunchZoneController-инъекция не нужны —
        /// на сценах без хаба поле не прошито.
        /// </summary>
        [SerializeField] private BuildingCatalogConfig _buildingCatalogConfig;

        /// <summary>
        /// HUD хаба (монеты + временная кнопка PLAY). Живёт только в BaseHub; на остальных
        /// сценах null — Presenter не регистрируется (паттерн _garageView).
        /// </summary>
        [SerializeField] private HubHudView _hubHudView;

        /// <summary>
        /// World-space круг подтверждения запуска над активной зоной. Регистрируется только
        /// вместе с _buildingCatalogConfig — presenter читает каталог и IHubLaunchService.
        /// </summary>
        [SerializeField] private LaunchConfirmView _launchConfirmView;

        // ── Шаг 11d: экран загрузки межсценового перехода ────────────────────────
        /// <summary>
        /// Экран загрузки (инстанс префаба Prefabs/UI/LoadingScreenCanvas). Прошивается меню
        /// "FlatOutLike/UI/Build Loading Screen (All Scenes)". Условная регистрация (паттерн
        /// _audioPlayerController): на сценах без экрана регистрируется NullLoadingScreen —
        /// переход мгновенный.
        /// </summary>
        [SerializeField] private LoadingScreenView _loadingScreenView;

        /// <summary>
        /// Настройки экрана загрузки (длительности, фейк-прогресс, заголовки, подсказки).
        /// Прошивается тем же меню "FlatOutLike/UI/Build Loading Screen (All Scenes)".
        /// Регистрируется БЕЗУСЛОВНО с фоллбэк-инстансом — SceneLoaderService нужен на любой сцене.
        /// </summary>
        [SerializeField] private LoadingScreenConfig _loadingScreenConfig;

        protected override void Configure(IContainerBuilder builder)
        {
            // Core
            builder.Register<EventBus>(Lifetime.Singleton).As<IEventBus>();

            // Выбор input-провайдера (Шаг 9): мобильный HUD пушит ввод вручную, поэтому
            // MobileInputProvider регистрируется дополнительно AsSelf() — MobileInputCanvas
            // инжектит его напрямую (не через IInputProvider), чтобы вызывать Set*/Queue*.
            bool useMobileInput = (_inputConfig != null && _inputConfig.ForceMobileInput) || Application.isMobilePlatform;
            if (useMobileInput)
            {
                builder.Register<MobileInputProvider>(Lifetime.Singleton).AsSelf().As<IInputProvider>();

                if (_mobileInputCanvas != null)
                    builder.RegisterComponent(_mobileInputCanvas);
            }
            else
            {
                builder.Register<KeyboardInputProvider>(Lifetime.Singleton).As<IInputProvider>();

                // На десктопе прошитый мобильный канвас не регистрируется (Construct не вызовется),
                // но GameObject остался бы видимым поверх игры — гасим явно.
                if (_mobileInputCanvas != null)
                    _mobileInputCanvas.gameObject.SetActive(false);
            }

            builder.Register<InputRouter>(Lifetime.Singleton);

            // Meta
            builder.Register<PlayerPrefsProfileStorage>(Lifetime.Singleton).As<IProfileStorage>();
            builder.Register<MetaProgressService>(Lifetime.Singleton).As<IMetaProgressService>();
            builder.Register<IPlayerProfile>(c => c.Resolve<IMetaProgressService>().Profile, Lifetime.Singleton);

            // Экономика и апгрейды (Шаг 8a). Каталоги — БЕЗУСЛОВНО (fallback empty instance,
            // паттерн DriverEjectConfig): пустой CarCatalogConfig переводит CarFactory в фоллбэк
            // на DerbyMatchConfig.PlayerCarPrefab/PlayerStats, пустой UpgradeCatalogConfig даёт
            // нулевые бонусы (GetMaxLevel всегда 0) — тест-сцены без Garage не падают.
            builder.RegisterInstance(_carCatalogConfig != null
                ? _carCatalogConfig
                : ScriptableObject.CreateInstance<CarCatalogConfig>());
            builder.RegisterInstance(_upgradeCatalogConfig != null
                ? _upgradeCatalogConfig
                : ScriptableObject.CreateInstance<UpgradeCatalogConfig>());
            builder.Register<EconomyService>(Lifetime.Singleton).As<IEconomyService>();
            builder.Register<CarStatsService>(Lifetime.Singleton).As<ICarStatsService>();

            // Flow (Шаг 8b) — scope живёт per-scene; начальное состояние определяется активной
            // сценой через GameStateSceneMap (единственный источник соответствия GameState↔сцена).
            // GameState — не Unity/VContainer-тип, поэтому передаётся явным .WithParameter —
            // VContainer не умеет опциональные параметры инъекции конструктора.
            var initialState = GameStateSceneMap.InitialStateFor(SceneManager.GetActiveScene().name);
            // Мета-сцены (хаб/гараж) ожидаемо не имеют машинного рига/тряски/нитро-камеры —
            // там отсутствие этих полей не ошибка: Log вместо LogWarning (см. LogMissingOptional).
            bool isMetaScene = initialState == GameState.BaseHub || initialState == GameState.GarageMeta;
            void LogMissingOptional(string message)
            {
                if (isMetaScene)
                    Debug.Log(message);
                else
                    Debug.LogWarning(message);
            }

            // Экран загрузки межсценового перехода (Шаг 11d). Конфиг — БЕЗУСЛОВНО (фоллбэк-инстанс
            // с дефолтами: RegisterInstance(null) = NRE при резолве SceneLoaderService).
            builder.RegisterInstance(_loadingScreenConfig != null
                ? _loadingScreenConfig
                : ScriptableObject.CreateInstance<LoadingScreenConfig>());

            // View — условно по наличию экрана на сцене: без него SceneLoaderService получает
            // мгновенный NullLoadingScreen (тест-сцены).
            if (_loadingScreenView != null)
                builder.RegisterComponent<ILoadingScreen>(_loadingScreenView);
            else
                builder.Register<NullLoadingScreen>(Lifetime.Singleton).As<ILoadingScreen>();

            // SceneLoaderService — EntryPoint (Singleton, AsImplementedInterfaces: IStartable открывает
            // сцену, если загрузка не началась ещё при сборке контейнера (Boot); ITickable ведёт прогресс
            // загрузки). GameFlowService/PauseService/AudioService получают этот же синглтон через
            // ISceneLoaderService.
            builder.RegisterEntryPoint<SceneLoaderService>().As<ISceneLoaderService>();
            // Eager: конструктор вызывает ILoadingScreen.Cover — экран обязан закрыть новую сцену
            // при сборке контейнера (Awake scope), ДО первого отрисованного кадра.
            builder.RegisterBuildCallback(c => c.Resolve<ISceneLoaderService>());
            builder.Register<GameFlowService>(Lifetime.Singleton)
                .As<IGameFlowService>()
                .WithParameter(initialState);
            builder.RegisterComponentInHierarchy<GameBootstrap>();

            // Пауза (Шаг 10a). Регистрируется БЕЗУСЛОВНО — GameBootstrap инжектит IPauseService
            // на каждой сцене (паттерн InputRouter). Eager — иначе конструктор (страховка
            // Time.timeScale=1f + подписки) не выполнится до первого Tick.
            builder.Register<PauseService>(Lifetime.Singleton).As<IPauseService>();
            builder.RegisterBuildCallback(c => c.Resolve<IPauseService>());

            if (_pauseOverlayView != null)
            {
                builder.RegisterComponent(_pauseOverlayView);
                builder.Register<PauseOverlayPresenter>(Lifetime.Singleton);
                builder.RegisterBuildCallback(c => c.Resolve<PauseOverlayPresenter>());
            }

            // ── Звук (Derby + High Jump + мета-UI) ────────────────────────────────
            // AudioConfig — БЕЗУСЛОВНО, паттерн _damageConfig: пустой конфиг (без клипов,
            // ассет ещё не импортирован) просто не даёт AudioService что проигрывать.
            builder.RegisterInstance(_audioConfig != null
                ? _audioConfig
                : ScriptableObject.CreateInstance<AudioConfig>());

            // IAudioPlayer — условно по наличию контроллера (паттерн _cameraShaker):
            // без него AudioService получает no-op реализацию и просто не издаёт звук.
            if (_audioPlayerController != null)
                builder.RegisterComponent<IAudioPlayer>(_audioPlayerController);
            else
                builder.Register<NullAudioPlayer>(Lifetime.Singleton).As<IAudioPlayer>();

            // AudioService — EntryPoint (IStartable инициализирует плеер и запускает музыку
            // текущего состояния, IDisposable отписывается). Регистрируется БЕЗУСЛОВНО —
            // зависит только от сервисов, зарегистрированных выше безусловно.
            builder.RegisterEntryPoint<AudioService>().As<IAudioService>();

            if (_uiClickSoundRelay != null)
                builder.RegisterComponent(_uiClickSoundRelay);

            // Награды (Шаг 5e) — зачисляет RewardEarnedEvent в профиль игрока.
            // Регистрируется безусловно (не зависит от _derbyMatchConfig/_rewardConfig),
            // т.к. награды может публиковать любой источник (Derby, HighJump, миссии).
            builder.Register<RewardCrediterService>(Lifetime.Singleton);

            // ── UI меты, MVP (Шаг 8c) ────────────────────────────────────────────
            // StuntsCatalogConfig — БЕЗУСЛОВНО, паттерн CarCatalogConfig/UpgradeCatalogConfig:
            // пустой каталог просто не даёт трюков.
            builder.RegisterInstance(_stuntsCatalogConfig != null
                ? _stuntsCatalogConfig
                : ScriptableObject.CreateInstance<StuntsCatalogConfig>());

            // Presenter условен по наличию View — его никто не резолвит напрямую (никакой
            // Controller/сервис на него не завязан), поэтому build callback обязателен: без него
            // конструктор Presenter'а не вызовется и подписки на клики/события не произойдут.
            if (_garageView != null)
            {
                builder.RegisterComponent(_garageView);
                builder.Register<GaragePresenter>(Lifetime.Singleton);
                builder.RegisterBuildCallback(c => c.Resolve<GaragePresenter>());
            }

            // ── BaseHub (Шаг 11a) ─────────────────────────────────────────────────
            // HubCharacterConfig — БЕЗУСЛОВНО (fallback-инстанс, чек-лист п.5): RegisterInstance(null)
            // роняет построение контейнера на сценах без хаба.
            builder.RegisterInstance(_hubCharacterConfig != null
                ? _hubCharacterConfig
                : ScriptableObject.CreateInstance<HubCharacterConfig>());

            // Персонаж — сцено-объект с [Inject] Construct: RegisterComponent, НЕ RegisterInstance
            // (иначе Construct не вызовется). Условно — существует только в сцене BaseHub.
            if (_hubCharacter != null)
                builder.RegisterComponent(_hubCharacter);

            // ── BaseHub: зоны запуска, HUD, PLAY (Шаг 11b) ─────────────────────────
            // Каталог и сервис запуска — условно: без каталога зонам нечего проверять, а
            // LaunchZoneController-инъекция (build callback ниже) тоже идёт только при нём.
            if (_buildingCatalogConfig != null)
            {
                builder.RegisterInstance(_buildingCatalogConfig);
                // EntryPoint → VContainer вызывает ITickable.Tick через PlayerLoop-диспетчер автоматически.
                builder.RegisterEntryPoint<HubLaunchService>().As<IHubLaunchService>();
            }

            // HUD хаба зависит только от безусловных IEconomyService/IEventBus/IGameFlowService.
            if (_hubHudView != null)
            {
                builder.RegisterComponent(_hubHudView);
                builder.Register<HubPresenter>(Lifetime.Singleton);
                builder.RegisterBuildCallback(c => c.Resolve<HubPresenter>());
            }

            // Круг подтверждения читает каталог и IHubLaunchService — оба условны по каталогу.
            if (_launchConfirmView != null && _buildingCatalogConfig != null)
            {
                builder.RegisterComponent(_launchConfirmView);
                builder.Register<LaunchConfirmPresenter>(Lifetime.Singleton);
                builder.RegisterBuildCallback(c => c.Resolve<LaunchConfirmPresenter>());
            }

            // MatchEndView живёт в Derby-сцене — зависит только от IEventBus/ICarRegistry/
            // IGameFlowService (все регистрируются безусловно ниже по файлу), никаких
            // мета-каталогов Garage/Stunts ему не нужно.
            if (_matchEndView != null)
            {
                builder.RegisterComponent(_matchEndView);
                builder.Register<MatchEndPresenter>(Lifetime.Singleton);
                builder.RegisterBuildCallback(c => c.Resolve<MatchEndPresenter>());
            }

            // ── Игровые HUD (Шаг 8d) ──────────────────────────────────────────────
            // DerbyHudView живёт в Derby-сцене — тот же паттерн, что и MatchEndView: зависит
            // только от безусловных сервисов (IEventBus/IDamageService/INitroService/ICarRegistry).
            if (_derbyHudView != null)
            {
                builder.RegisterComponent(_derbyHudView);
                builder.Register<DerbyHudPresenter>(Lifetime.Singleton);
                builder.RegisterBuildCallback(c => c.Resolve<DerbyHudPresenter>());
            }

            // HighJumpHudView живёт на сцене трамплина — зависит от IDriverFlightService/
            // IMetaProgressService (безусловны) и ICarRegistry/IGameFlowService (тоже безусловны).
            if (_highJumpHudView != null)
            {
                builder.RegisterComponent(_highJumpHudView);
                builder.Register<HighJumpHudPresenter>(Lifetime.Singleton);
                builder.RegisterBuildCallback(c => c.Resolve<HighJumpHudPresenter>());
            }

            // Car
            builder.Register<CarMovementService>(Lifetime.Singleton).As<ICarMovementService>();

            // Combat (Шаг 3). Фоллбэк-инстанс (паттерн DriverEjectConfig): в мета-сценах/хабе
            // (BaseHub/Garage) поле не прошито, а RegisterInstance(null)
            // бросает NRE при построении контейнера — scope обязан переживать мета-сцены.
            builder.RegisterInstance(_damageConfig != null
                ? _damageConfig
                : ScriptableObject.CreateInstance<DamageConfig>());
            builder.Register<DamageService>(Lifetime.Singleton).As<IDamageService>();
            builder.Register<DamageStageResolver>(Lifetime.Singleton).As<IDamageStageResolver>();

            // VFX (Шаг 3). Фоллбэк по той же причине — VfxFactory в мета-сценах лениво не резолвится.
            builder.RegisterInstance(_vfxConfig != null
                ? _vfxConfig
                : ScriptableObject.CreateInstance<VfxConfig>());
            builder.Register<VfxFactory>(Lifetime.Singleton).As<IVfxFactory>();
            builder.Register<VfxService>(Lifetime.Singleton).As<IVfxService>();

            // AI (Шаг 4)
            builder.Register<EnemyAIService>(Lifetime.Singleton).As<IEnemyAIService>();

            // Match (Шаг 5b) — реестр машин и выбор цели
            builder.Register<CarRegistry>(Lifetime.Singleton).As<ICarRegistry>();
            builder.Register<TargetSelectionService>(Lifetime.Singleton).As<ITargetSelectionService>();

            // Nitro (Шаг 5c)
            builder.Register<NitroService>(Lifetime.Singleton).As<INitroService>();

            // Pickup-система (Шаг 5c).
            // Все три регистрации — условные: если _pickupConfig не задан (тест-сцены без пикапов),
            // VContainer не попытается создать PickupFactory/PickupSpawnerService и не упадёт.
            if (_pickupConfig != null)
            {
                builder.RegisterInstance(_pickupConfig);
                builder.Register<PickupFactory>(Lifetime.Singleton).As<IPickupFactory>();
                // EntryPoint → VContainer вызывает ITickable.Tick через PlayerLoop-диспетчер автоматически.
                builder.RegisterEntryPoint<PickupSpawnerService>().As<IPickupSpawnerService>();
            }

            // Eject + камера водителя (Шаг 6).
            // DriverEjectConfig регистрируется БЕЗУСЛОВНО: VContainer игнорирует C#-значения
            // по умолчанию в [Inject] Construct и бросает VContainerException на незарегистрированный
            // тип — условная регистрация роняла бы InjectGameObject любой машины в тест-сценах.
            // С fallback-инстансом eject там всё равно не срабатывает (_driver == null у машин).
            builder.RegisterInstance(_driverEjectConfig != null
                ? _driverEjectConfig
                : ScriptableObject.CreateInstance<DriverEjectConfig>());

            // Камерные регистрации — условные (паттерн pickup-конфига): тест-сцены без камеры
            // не должны падать при резолве.

            if (_orbitCameraConfig != null)
                builder.RegisterInstance(_orbitCameraConfig);

            if (_cameraRig != null)
            {
                builder.RegisterComponent<ICameraRig>(_cameraRig);
                builder.Register<CameraService>(Lifetime.Singleton).As<ICameraService>();
            }
            else
            {
                // Ожидаемо для мета-сцен/хаба (BaseHub/Garage) — там нет машинного рига: камера хаба
                // (CinemachineHubCameraBehaviour) живёт отдельно и в ICameraRig не входит.
                LogMissingOptional("[GameLifetimeScope] _cameraRig не задан — ICameraService не регистрируется (мета-сцена/хаб или тест-сцена без камеры).");
            }

            // Тряска камеры от столкновений (Impulse). Условно — оба поля заполняются
            // вместе на риге арены; тест-сцены без них просто не получают сервис.
            if (_cameraShakeConfig != null && _cameraShaker != null)
            {
                builder.RegisterInstance(_cameraShakeConfig);
                builder.RegisterComponent<ICameraShaker>(_cameraShaker);
                builder.Register<CameraShakeService>(Lifetime.Singleton).As<ICameraShakeService>();
            }
            else
            {
                LogMissingOptional("[GameLifetimeScope] _cameraShakeConfig/_cameraShaker не заданы — ICameraShakeService не регистрируется (мета-сцена/хаб или тест-сцена без рига).");
            }

            // NitroCameraEffectController — MonoBehaviour, ему нужна инъекция (IEventBus, ICarRegistry),
            // поэтому RegisterComponent, а не RegisterInstance. Условно — в тест-сценах без камеры null.
            if (_nitroCameraEffectController != null)
            {
                builder.RegisterComponent(_nitroCameraEffectController);
            }
            else
            {
                LogMissingOptional("[GameLifetimeScope] _nitroCameraEffectController не задан — эффект нитро-камеры не регистрируется (мета-сцена/хаб или тест-сцена без камеры).");
            }

            // Полёт водителя (Шаг 7). DriverFlightConfig — БЕЗУСЛОВНО, тот же резон, что и
            // DriverEjectConfig: DriverFlightController сидит на префабе водителя и инжектится
            // во всех сценах, включая Derby, где трамплина и сессии нет.
            builder.RegisterInstance(_driverFlightConfig != null
                ? _driverFlightConfig
                : ScriptableObject.CreateInstance<DriverFlightConfig>());

            builder.Register<FlipDetectorService>(Lifetime.Singleton).As<IFlipDetectorService>();
            builder.Register<DriverFlightService>(Lifetime.Singleton).As<IDriverFlightService>();

            // Скоринг и сессия «High Jump» — условно: только на сценах с трамплином.
            if (_highJumpRulesConfig != null)
            {
                builder.RegisterInstance(_highJumpRulesConfig);
                builder.Register<HighJumpScoreService>(Lifetime.Singleton).As<IHighJumpScoreService>();
                // EntryPoint → VContainer вызывает ITickable.Tick через PlayerLoop-диспетчер автоматически.
                builder.RegisterEntryPoint<HighJumpSessionService>().As<IHighJumpSessionService>();
            }

            // Pool (Шаг 5a) — единственный источник пулов для всех фабрик
            builder.Register<PoolManager>(Lifetime.Singleton).As<IPoolManager>();

            // Car factory (Шаг 5a)
            builder.Register<CarFactory>(Lifetime.Singleton).As<ICarFactory>();

            // Разрушаемые объекты (Шаг 5d)
            builder.Register<DestructibleFactory>(Lifetime.Singleton).As<IDestructibleFactory>();

            // Derby match config (Шаг 5a); SO — RegisterInstance корректно, инъекция не нужна
            if (_derbyMatchConfig != null)
            {
                builder.RegisterInstance(_derbyMatchConfig);

                // DerbyMatchService (Шаг 5e) — требует ещё и RewardConfig, поэтому условие двойное.
                if (_rewardConfig != null)
                {
                    builder.RegisterInstance(_rewardConfig);
                    builder.Register<DerbyMatchService>(Lifetime.Singleton).As<IDerbyMatchService>();
                }
            }

            // Eager-инициализация DamageService, чтобы его ctor подписался на CarCollisionEvent
            // даже когда явный consumer не запрашивает его из container.
            builder.RegisterBuildCallback(c => c.Resolve<IDamageService>());

            // Eager-инициализация RewardCrediterService, чтобы он подписался на RewardEarnedEvent
            // до того как матч успеет завершиться и опубликовать награду.
            builder.RegisterBuildCallback(c => c.Resolve<RewardCrediterService>());

            // Eager-инициализация CameraShakeService — иначе никто не резолвит его из контейнера,
            // и подписка на CarCollisionEvent в конструкторе не произойдёт. Регистрируем условно,
            // только если сервис вообще был зарегистрирован (см. блок выше).
            if (_cameraShakeConfig != null && _cameraShaker != null)
                builder.RegisterBuildCallback(c => c.Resolve<ICameraShakeService>());

            // Путь _TestDriving: внедряем машины, расставленные вручную в сцене.
            // Заменяет RegisterComponentInHierarchy<CarController> и
            // RegisterComponentInHierarchy<CarDamageVisualController> — эти API
            // находят только один компонент каждого типа в иерархии. Здесь мы внедряем
            // каждый GameObject из _sceneCars, покрывая CarController, EnemyCarController
            // и CarDamageVisualController на каждом корне Prefab за один проход.
            builder.RegisterBuildCallback(container =>
            {
                // _TestDriving-путь: внедряем машины, расставленные вручную в сцене,
                // и регистрируем их в ICarRegistry (первая = игрок). Без регистрации
                // CameraService не узнаёт игрока и не переключает камеру на ragdoll при eject.
                if (_sceneCars != null && _sceneCars.Length > 0)
                {
                    var registry = container.Resolve<ICarRegistry>();
                    for (int i = 0; i < _sceneCars.Length; i++)
                    {
                        var go = _sceneCars[i];
                        if (go == null) continue;

                        container.InjectGameObject(go);

                        var sceneCar = go.GetComponent<CarController>();
                        if (sceneCar != null)
                            registry.Register(sceneCar, isPlayer: i == 0);
                    }
                }

                // Шаг 5d: инъекция разрушаемых объектов (заборов и т.п.), расставленных на сцене.
                // Безусловно — не зависит от _sceneCars. Если заборов нет — цикл ничего не делает.
                foreach (var d in Object.FindObjectsByType<DestructibleObjectController>(FindObjectsSortMode.None))
                    container.InjectGameObject(d.gameObject);

                // Шаг 7: инъекция триггеров трамплина, расставленных на сцене.
                // Безусловно (паттерн DestructibleObjectController выше) — если объектов нет, цикл пуст.
                foreach (var eject in Object.FindObjectsByType<EjectTriggerZone>(FindObjectsSortMode.None))
                    container.InjectGameObject(eject.gameObject);

                // Шаг 11b: инъекция зон запуска хаба. Только при каталоге — без него IHubLaunchService
                // не зарегистрирован, и Construct зоны уронил бы построение контейнера.
                if (_buildingCatalogConfig != null)
                {
                    foreach (var zone in Object.FindObjectsByType<LaunchZoneController>(FindObjectsSortMode.None))
                        container.InjectGameObject(zone.gameObject);
                }

                // _TestDriving-путь: chase-камера на первую сцено-машину (арена привязывает камеру
                // отдельно, в build-callback ниже, после спавна через DerbyMatchService).
                if (_cameraRig != null && _sceneCars != null && _sceneCars.Length > 0 && _sceneCars[0] != null)
                {
                    var firstCar = _sceneCars[0].GetComponent<CarController>();
                    if (firstCar != null)
                        container.Resolve<ICameraService>().FollowPlayerCar(firstCar.transform);
                }

                // Шаг 7: запускаем сессию «High Jump», если сцена трамплина сконфигурирована.
                if (_highJumpRulesConfig != null)
                    container.Resolve<IHighJumpSessionService>().StartSession();
            });

            // Триггер старта Derby-матча (Шаг 5e). Спавн и логика матча теперь в DerbyMatchService;
            // здесь только запуск и привязка camera-таргета к заспавненной машине игрока.
            // Арена использует фабрику через DerbyMatchService (_sceneCars пуст),
            // _TestDriving — старый _sceneCars путь; пути взаимоисключающие по сцене.
            builder.RegisterBuildCallback(container =>
            {
                if (_derbyMatchConfig != null
                    && _rewardConfig != null
                    && _spawnPoints != null
                    && _spawnPoints.Length > 0)
                {
                    var cars = container.Resolve<IDerbyMatchService>().StartMatch(_spawnPoints);

                    // Привязываем chase-камеру к машине игрока (index 0), заспавненной в рантайме.
                    // ICameraService зарегистрирован только если _cameraRig задан (условная регистрация выше).
                    if (_cameraRig != null && cars != null && cars.Count > 0 && cars[0] != null)
                        container.Resolve<ICameraService>().FollowPlayerCar(cars[0].transform);
                }
                else if (_derbyMatchConfig != null && _rewardConfig == null)
                {
                    // Диагностика: арена настроена, но reward-конфиг не проставлен — иначе спавн молча не произойдёт.
                    Debug.LogWarning("[GameLifetimeScope] _derbyMatchConfig задан, но _rewardConfig == null — " +
                                     "Derby-матч не запустится. Прогони FlatOutLike/Configs/Create Default RewardConfig.");
                }

                // Шаг 5c: запускаем спавнер пикапов только если конфиг и зоны настроены.
                if (_pickupConfig != null && _pickupZones != null && _pickupZones.Length > 0)
                    container.Resolve<IPickupSpawnerService>().StartSpawning(_pickupZones);
            });
        }
    }
}
