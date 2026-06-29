---
name: step5c-nitro-pickups
description: Шаг 5c — NitroService, pickup-система (RepairPickup/NitroPickup), PickupSpawnerService, DamageService.Repair
metadata:
  type: project
---

## Реализованные системы (шаг 5c)

**NitroService** (`Services/Combat/INitroService.cs` + `NitroService.cs`)
- `Dictionary<CarId, (float current, float capacity)>` — pure C#
- `RegisterCar` стартует с пустым баком (0f)
- `TryConsume` → false без списания при нехватке
- `Refill(fraction)` — доливает fraction*capacity с cap
- Зарегистрирован: `builder.Register<NitroService>(Lifetime.Singleton).As<INitroService>()` — БЕЗУСЛОВНО (нужен CarController всегда)

**CarStatsConfig** — добавлено поле `_nitroDrainRate = 25f` + геттер `NitroDrainRate`

**CarController** — добавлен `INitroService _nitro` в Construct(5 параметров), `nitro.RegisterCar(CarId, NitroCapacity)` при инициализации, `ApplyNitro()` в FixedUpdate

**DamageService.Repair** — добавлен `_maxArmor` Dictionary; Repair публикует `CarDamagedEvent(id, -healed, newHp)` (отрицательный damage) → CarDamageVisualController откатывает визуал

**PickupConfig** (`Data/Configs/Pickups/PickupConfig.cs`) — SO, namespace `FlatOutLike.Data.Configs.Pickups`
- Поля: `_repairFraction=0.4f`, `_nitroFillFraction=1.0f`, `_respawnInterval=8f`, `PickupEntry[] _entries`
- Вложенный `[Serializable] struct PickupEntry { PickupType type; GameObject prefab; int poolSize; }`

**IPickup** (`Controllers/Pickups/IPickup.cs`) — `PickupType Type; void Activate(Action<IPickup>)`
**RepairPickup / NitroPickup** — MonoBehaviour в namespace `FlatOutLike.Controllers.Pickups`
- Trigg-коллайдер + кинематический Rigidbody на prefab собирается editor-скриптом
- _initialized guard, _collected flag для переиспользования из пула

**PickupFactory** (`Factories/Pickup/`) — pure C#, Prewarm по config.Entries, паттерн VfxFactory
- Despawn: `(pickup as MonoBehaviour).gameObject`

**PickupZone** — `[Serializable] struct { Transform point; PickupType type; }` в `Services/Pickups/`

**PickupSpawnerService** (`Services/Pickups/`) — implements `IPickupSpawnerService + ITickable`
- Registered: `builder.RegisterEntryPoint<PickupSpawnerService>().As<IPickupSpawnerService>()`
- ZoneState — private sealed class (ref type), нет проблемы с value-type мутацией в List
- Tick() тикает таймеры зон без активного пикапа

**GameLifetimeScope** — pickup-регистрации УСЛОВНЫЕ (`if (_pickupConfig != null)`): регистрирует SO, PickupFactory, PickupSpawnerService EntryPoint. NitroService — безусловный. `StartSpawning` вызывается в build callback при `_pickupConfig != null && _pickupZones?.Length > 0`.

**Events** — добавлен `PickupCollectedEvent { CarId Collector; PickupType Type; }` в `Events.Derby.cs`

**Тесты** — `Tests/EditMode/NitroServiceTests.cs` — 9 тестов, namespace `FlatOutLike.Tests`

**Why:** Условная регистрация критична — иначе VContainer падает при сборке контейнера в тест-сценах без PickupConfig, т.к. PickupSpawnerService является EntryPoint и создаётся eagerly.

**How to apply:** При добавлении новых сервисов, зависящих от опциональных SO-конфигов — оборачивать все связанные регистрации в `if (_config != null)`.
