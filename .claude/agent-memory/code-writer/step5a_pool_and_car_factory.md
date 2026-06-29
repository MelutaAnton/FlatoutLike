---
name: step5a-pool-and-car-factory
description: Pool system (IPoolManager/GameObjectPool/PooledInstance/PoolManager) and CarFactory added in step 5a; VfxFactory retrofitted; Configure pattern on controllers
metadata:
  type: project
---

## Step 5a — Generic Pool + CarFactory (шаг 5a)

**Добавленные системы:**
- `Core/Pool/IPoolManager` — prefab-keyed пул (Get/Return/Prewarm). Ключ = ссылка на prefab, не строка.
- `Core/Pool/GameObjectPool` — пул одного prefab: Queue<GameObject>, SetActive-семантика.
- `Core/Pool/PooledInstance` — маркер-компонент с `SourcePrefab`; позволяет Return найти правильный пул.
- `Core/Pool/PoolManager` — Singleton; `Dictionary<GameObject prefab, GameObjectPool>`.
- `Factories/Car/ICarFactory` + `Factories/Car/CarFactory` — спавн матча через `SpawnMatch(DerbyMatchConfig, IReadOnlyList<Transform>)`.
- `Data/Configs/Match/DerbyMatchConfig` — SO с `EnemySpawnEntry[] _enemies` (профиль + статы).

**VfxFactory retrofit:** конструктор теперь принимает `IPoolManager`; собственные `_pools` и `_controllerTypes` удалены. Прекарм делегирован в `_pool.Prewarm(entry.prefab, entry.poolSize)`. IVfxFactory контракт не изменился.

**Configure-паттерн на контроллерах:**
- `CarController.Configure(int carId, CarStatsConfig stats)` — задаёт `_carId` и `_config` до InjectGameObject.
- `EnemyCarController.Configure(EnemyPersonalityConfig, CarStatsConfig, CarController target)` — задаёт `_profile`, `_statsConfig`, вызывает `SetTarget`. Sensor config остаётся из prefab.

**Порядок спавна в CarFactory:** Instantiate → Configure (до инъекции) → InjectGameObject.

**GameLifetimeScope:** добавлены `_derbyMatchConfig` (DerbyMatchConfig SO) и `_spawnPoints[]`; PoolManager и CarFactory зарегистрированы; RegisterBuildCallback для derby-пути взаимоисключает _sceneCars-путь (_TestDriving).

**Why:** Шаг 5a — foundation для Derby-матча. На шаге 5e спавн переедет в DerbyMatchService.
**How to apply:** При создании любой новой фабрики — использовать IPoolManager, не прямой Queue. Configure вызывать ДО InjectGameObject.
