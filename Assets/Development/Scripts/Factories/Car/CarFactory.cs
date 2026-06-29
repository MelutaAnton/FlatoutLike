using System.Collections.Generic;
using FlatOutLike.Controllers.Car;
using FlatOutLike.Controllers.Enemies;
using FlatOutLike.Data.Configs.Match;
using FlatOutLike.Services.Match;
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
    /// </summary>
    public sealed class CarFactory : ICarFactory
    {
        private readonly IObjectResolver _resolver;
        private readonly ICarRegistry    _registry;

        public CarFactory(IObjectResolver resolver, ICarRegistry registry)
        {
            _resolver = resolver;
            _registry = registry;
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
            CarController playerController = null;
            if (config.PlayerCarPrefab != null && spawnPoints[0] != null)
            {
                var sp       = spawnPoints[0];
                var playerGo = Object.Instantiate(config.PlayerCarPrefab, sp.position, sp.rotation);

                playerController = playerGo.GetComponent<CarController>();
                if (playerController != null)
                    playerController.Configure(0, config.PlayerStats); // carId игрока = 0

                // InjectGameObject вызывает Construct на всех компонентах.
                // К этому моменту Configure уже задал _carId и _config.
                _resolver.InjectGameObject(playerGo);

                if (playerController != null)
                {
                    allCars.Add(playerController);
                    _registry.Register(playerController, isPlayer: true);
                }
            }
            else
            {
                Debug.LogWarning("[CarFactory] PlayerCarPrefab не задан в DerbyMatchConfig.");
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
                }
            }

            return allCars;
        }
    }
}
