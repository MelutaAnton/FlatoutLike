using System.Collections.Generic;
using FlatOutLike.Controllers.Car;
using FlatOutLike.Controllers.Enemies;
using FlatOutLike.Data.Configs.Car;
using FlatOutLike.Data.Configs.Match;
using FlatOutLike.Services.Car;
using FlatOutLike.Services.Match;
using FlatOutLike.Services.Meta;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace FlatOutLike.Factories.Car
{
    /// <summary>
    /// Инстанцирует машины для Derby-матча. Машины не пулятся — живут весь матч.
    ///
    /// Порядок гарантирован: Instantiate → Configure (до инъекции) → InjectGameObject.
    /// Это позволяет Construct читать актуальные _config / _profile / _statsConfig,
    /// а не значения по умолчанию из prefab.
    ///
    /// После спавна регистрирует все машины в <see cref="ICarRegistry"/> —
    /// это позволяет <c>TargetSelectionService</c> видеть всех участников матча.
    ///
    /// Машина игрока (Шаг 8a): если <see cref="CarCatalogConfig"/> не пуст, prefab и базовые
    /// статы берутся из entry по <see cref="IEconomyService.SelectedCarId"/> (правило резолва
    /// «невалидный/пустой id → первая entry» живёт ТОЛЬКО в EconomyService — здесь не дублируется).
    /// Незаполненные поля entry и пустой каталог — фоллбэк на <see cref="DerbyMatchConfig.PlayerCarPrefab"/> /
    /// <see cref="DerbyMatchConfig.PlayerStats"/>, как раньше. В обоих случаях статы прогоняются
    /// через <see cref="ICarStatsService.GetEffectiveStats"/>, применяя бонусы апгрейдов.
    /// </summary>
    public sealed class CarFactory : ICarFactory
    {
        private readonly IObjectResolver _resolver;
        private readonly ICarRegistry    _registry;
        private readonly IEconomyService _economy;
        private readonly CarCatalogConfig _carCatalogConfig;
        private readonly ICarStatsService _carStatsService;

        public CarFactory(
            IObjectResolver resolver,
            ICarRegistry registry,
            IEconomyService economy,
            CarCatalogConfig carCatalogConfig,
            ICarStatsService carStatsService)
        {
            _resolver = resolver;
            _registry = registry;
            _economy = economy;
            _carCatalogConfig = carCatalogConfig;
            _carStatsService = carStatsService;
        }

        /// <inheritdoc/>
        public IReadOnlyList<CarController> SpawnMatch(DerbyMatchConfig config, IReadOnlyList<Transform> spawnPoints)
        {
            var allCars = new List<CarController>();

            // Очищаем реестр в самом начале — даже при невалидном конфиге не должно
            // остаться стейл-машин от прошлого матча (важно для повторного спавна на 5e).
            _registry.Clear();

            if (spawnPoints == null || spawnPoints.Count == 0)
            {
                Debug.LogWarning("[CarFactory] SpawnMatch: список точек спавна пуст. Машины не заспавнены.");
                return allCars;
            }

            if (config == null)
            {
                Debug.LogWarning("[CarFactory] SpawnMatch: DerbyMatchConfig равен null.");
                return allCars;
            }

            // ── Спавн игрока (spawnPoints[0]) ──────────────────────────────────
            // Базовые prefab/статы: каталог (если не пуст) → entry по SelectedCarId-логике,
            // иначе фоллбэк на DerbyMatchConfig.PlayerCarPrefab/PlayerStats (старый путь).
            GameObject      playerPrefab    = config.PlayerCarPrefab;
            CarStatsConfig  playerBaseStats = config.PlayerStats;

            // Kill-feed (Шаг 10b): имя игрока для ленты. Дефолт — "PLAYER", переопределяется
            // ниже из каталога, если у entry заполнен DisplayName.
            string playerDisplayName = "PLAYER";

            if (_carCatalogConfig != null && _carCatalogConfig.Entries.Count > 0 &&
                _carCatalogConfig.TryGetEntry(_economy.SelectedCarId, out var catalogEntry))
            {
                // Частично заполненная entry (художник не прошил prefab/stats) не должна
                // тихо ломать матч — по каждому пустому полю откатываемся на DerbyMatchConfig.
                if (catalogEntry.Prefab != null)
                    playerPrefab = catalogEntry.Prefab;
                else
                    Debug.LogWarning($"[CarFactory] У entry '{catalogEntry.Id}' каталога не задан Prefab — фоллбэк на DerbyMatchConfig.PlayerCarPrefab.");

                if (catalogEntry.Stats != null)
                    playerBaseStats = catalogEntry.Stats;
                else
                    Debug.LogWarning($"[CarFactory] У entry '{catalogEntry.Id}' каталога не задан Stats — фоллбэк на DerbyMatchConfig.PlayerStats.");

                if (!string.IsNullOrEmpty(catalogEntry.DisplayName))
                    playerDisplayName = catalogEntry.DisplayName;
            }

            CarController playerController = null;
            if (playerPrefab != null && spawnPoints[0] != null)
            {
                var sp       = spawnPoints[0];
                var playerGo = Object.Instantiate(playerPrefab, sp.position, sp.rotation);

                // Прогоняем базовые статы через апгрейды ВЫБРАННОЙ машины игрока (per-car
                // прогресс, §8/§14.3 game-design.md) — работает и для каталожной, и для
                // фоллбэк-машины: id один и тот же (_economy.SelectedCarId — каталожный путь
                // резолвит catalogEntry.Id из того же id). Пустой id (пустой каталог и профиль
                // без выбора) → GetEffectiveStats вернёт baseStats без клона. При активных
                // апгрейдах это runtime-клон SO: он живёт до domain reload (один на матч,
                // только у игрока) — осознанный trade-off, без апгрейдов возвращается исходный
                // ассет без аллокации.
                var effectiveStats = _carStatsService.GetEffectiveStats(_economy.SelectedCarId, playerBaseStats);

                playerController = playerGo.GetComponent<CarController>();
                if (playerController != null)
                    playerController.Configure(0, effectiveStats); // carId игрока = 0

                // InjectGameObject вызывает Construct на всех компонентах.
                // К этому моменту Configure уже задал _carId и _config.
                _resolver.InjectGameObject(playerGo);

                if (playerController != null)
                {
                    allCars.Add(playerController);
                    _registry.Register(playerController, isPlayer: true);
                    _registry.RegisterName(playerController.CarId, playerDisplayName);
                }
            }
            else
            {
                Debug.LogWarning("[CarFactory] Prefab машины игрока не задан (ни в каталоге, ни в DerbyMatchConfig.PlayerCarPrefab).");
            }

            // ── Спавн врагов (spawnPoints[1..]) ────────────────────────────────
            var enemies = config.Enemies;
            if (enemies == null)
            {
                Debug.LogWarning("[CarFactory] Список врагов (DerbyMatchConfig.Enemies) равен null.");
                return allCars;
            }

            int enemyCount     = enemies.Count;
            int availableSlots = spawnPoints.Count - 1; // минус слот для игрока

            if (availableSlots < enemyCount)
            {
                Debug.LogWarning(
                    $"[CarFactory] Недостаточно точек спавна: нужно {enemyCount + 1}, " +
                    $"есть {spawnPoints.Count}. Будет заспавнено {availableSlots} из {enemyCount} врагов.");
            }

            int spawnCount = Mathf.Min(enemyCount, availableSlots);

            // Счётчик употреблений имени профиля — уникализация для kill-feed: несколько
            // врагов на одном EnemyPersonalityConfig иначе неразличимы ("Aggressor ▸ Aggressor").
            var nameCounts = new System.Collections.Generic.Dictionary<string, int>();

            for (int i = 0; i < spawnCount; i++)
            {
                var entry = enemies[i];
                var sp    = spawnPoints[i + 1];
                int carId = i + 1; // уникальный id; 0 зарезервирован за игроком

                if (entry.carPrefab == null)
                {
                    Debug.LogWarning($"[CarFactory] carPrefab не задан у врага {i} в DerbyMatchConfig. Враг пропущен.");
                    continue;
                }

                if (sp == null)
                {
                    Debug.LogWarning($"[CarFactory] Точка спавна {i + 1} пуста (null). Враг {i} пропущен.");
                    continue;
                }

                var enemyGo = Object.Instantiate(entry.carPrefab, sp.position, sp.rotation);

                var carController   = enemyGo.GetComponent<CarController>();
                var enemyController = enemyGo.GetComponent<EnemyCarController>();

                // Configure до InjectGameObject — Construct прочитает правильные значения.
                if (carController != null)
                    carController.Configure(carId, entry.stats);

                // Configure задаёт только профиль и статы. Цель не передаётся:
                // ITargetSelectionService выберет её автоматически в первом FixedUpdate.
                if (enemyController != null)
                    enemyController.Configure(entry.profile, entry.stats);

                // InjectGameObject: порядок вызовов Construct на компонентах не гарантирован,
                // но CarController и EnemyCarController спроектированы так, что оба варианта
                // порядка корректны (см. комментарий в EnemyCarController).
                _resolver.InjectGameObject(enemyGo);

                if (carController != null)
                {
                    allCars.Add(carController);
                    _registry.Register(carController, isPlayer: false);

                    // Kill-feed (Шаг 10b): EnemyPersonalityConfig не имеет поля displayName —
                    // используем имя ассета профиля; повторные употребления получают суффикс-номер
                    // ("Aggressor", "Aggressor 2", ...). Профиль не обязателен: тогда
                    // ICarRegistry.TryGetName вернёт false, presenter подставит фоллбэк "CAR <id>".
                    if (entry.profile != null)
                    {
                        string baseName = entry.profile.name;
                        nameCounts.TryGetValue(baseName, out int used);
                        nameCounts[baseName] = used + 1;

                        _registry.RegisterName(carController.CarId,
                            used == 0 ? baseName : $"{baseName} {used + 1}");
                    }
                }
            }

            return allCars;
        }
    }
}
