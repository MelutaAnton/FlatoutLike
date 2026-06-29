---
name: step5b-target-selection
description: Step 5b — target selection by personality: CarRegistry, TargetSelectionService, EnemyPersonalityConfig.TargetSelectionMode
metadata:
  type: project
---

## Step 5b — Derby: выбор цели по личности (реализовано)

**Новые файлы:**
- `Models/TargetSelectionMode.cs` — enum `{Nearest=0, Weakest=1, Player=2}`
- `Services/Match/ICarRegistry.cs` + `CarRegistry.cs` — реестр всех машин матча
- `Services/AI/ITargetSelectionService.cs` + `TargetSelectionService.cs` — выбор цели по режиму
- `Tests/EditMode/TargetSelectionServiceTests.cs` — 7 тестов

**Изменённые файлы:**
- `EnemyPersonalityConfig` — добавлено поле `_targetSelectionMode` (default Nearest) + геттер
- `EnemyCarController` — `Construct` принимает `ITargetSelectionService`; `Configure` без `target`; удалён `SetTarget`; в `FixedUpdate` таймер переключения цели
- `CarFactory` — ctor принимает `ICarRegistry`; `SpawnMatch` вызывает `_registry.Clear()` + Register для игрока и каждого врага
- `GameLifetimeScope` — зарегистрированы `CarRegistry`/`ICarRegistry` и `TargetSelectionService`/`ITargetSelectionService` (Singleton)

**Ключевые решения:**
- `SetTarget` удалён из `EnemyCarController` — вызовов извне не было
- `_target` поле сохранено как `[SerializeField]` для диагностики в Inspector (показывает текущую цель в рантайме)
- `_targetSwitchTimer` стартует в 0 → цель выбирается на первом FixedUpdate
- Player-fallback → Nearest при мёртвом игроке

**Why:** TargetSelectionService — pure C#, тестируемый, без MonoBehaviour. CarRegistry — pure C#.

**How to apply:** при любом новом режиме выбора добавить значение в `TargetSelectionMode` enum (не менять порядок существующих!) и ветку в `TargetSelectionService.SelectTarget`.

Связано с: [[step5a-pool-and-car-factory]], [[step4-ai-status]]
