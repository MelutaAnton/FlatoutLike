# FlatOutLike — Game Design (вертикальный срез)

> Архитектурные правила, паттерны и запреты — в `CLAUDE.md`. Этот документ их не дублирует, а ссылается. Все параметры геймплея — только ScriptableObject; никаких magic numbers в Controllers.

---

## 1. Vision & Scope

**Референс:** FlatOut 2 (Empire Interactive / Bugbear, 2006).

**Что берём в срез:**
- **Derby** — играбельный матч на арене: игрок против 3–6 AI-машин, win = last alive, физические повреждения с гибридной моделью (mesh-stages + отваливающиеся части), авто-eject водителя при гибели машины.
- **Stunts: High Jump** — играбельная мини-игра «Выброс водителя в высоту»: разгон на машине → трамплин → автоматический eject через лобовое стекло → ragdoll-полёт с управлением стиками и одноразовым midair-boost → подсчёт высоты.
- **Stunts Hub** — экран выбора мини-игр: High Jump активна, остальные 8 (Long Jump, Fire Rings, Bowling, Field Goal, High Card, ?, ?, ?) — карточки-заглушки с пометкой "Coming soon" (Scalability Hook).
- **Garage / Meta** — экран между матчами: валюта, выбор машины (3 шт.), апгрейды (Armor / Power / Nitro).

**Что НЕ входит в срез (Scalability Hooks):**
- Гонки на трассе с чекпоинтами и таймером.
- Остальные 8 Stunts-мини-игр (Long Jump, Fire Rings, Bowling, Field Goal, High Card, ?, ?, ?).
- Online / split-screen / leaderboards на сервере.
- Кастомизация скинов машин и водителя.
- Тюнинг подвески, звуковые пресеты, погода/время суток.

**Целевая длительность игровой сессии:** 2–8 минут на Derby-матч, 30–60 секунд на High Jump-попытку.

**Платформы:** PC (Windows) + Mobile (Android/iOS) одновременно через единый `IInputProvider`.

---

## 2. Core Gameplay Loop

```
[Main Menu]
   ├─→ Derby ──→ [Match: Arena] ──(win/lose)──→ Reward (coins + score) ──→ [Garage]
   ├─→ Stunts ─→ [Stunts Hub] ──→ High Jump ──→ [Stunts: HighJump] ──→ Score ──→ Reward ──→ [Garage]
   │                          └─→ (5 заглушек)
   └─→ Garage ──→ Buy car / Upgrade ──→ back
```

Игрок чередует Derby и Stunts, копит валюту, прокачивает машину/апгрейды, возвращается в матч.

---

## 3. Input

### Контракт `IInputProvider` (см. `Core/Input/`)
Единый интерфейс для PC и Mobile. Конкретный провайдер выбирается в `GameLifetimeScope` по платформе.

| Action            | Type        | PC                       | Mobile                       |
| ----------------- | ----------- | ------------------------ | ---------------------------- |
| `Move`            | `Vector2`   | WASD / arrows            | Left virtual stick           |
| `Look`            | `Vector2`   | Mouse delta              | Right virtual stick (Stunts) |
| `Handbrake`       | `bool`      | Space                    | On-screen button             |
| `Nitro`           | `bool`      | LeftShift                | On-screen button             |
| `Eject` (manual)  | `Trigger`   | E                        | On-screen button             |
| `MidairBoost`     | `Trigger`   | LMB                      | On-screen button (Stunts)    |
| `Pause`           | `Trigger`   | Esc                      | Pause icon                   |

### Контексты ввода (Input Contexts)
Активный контекст определяет, какие Action'ы транслируются в события. Переключается `InputRouter` по `GameState`:

- `Driving` — Move, Handbrake, Nitro, Eject (manual), Pause.
- `Flight`  — Look, MidairBoost, Pause.
- `UI`      — навигация по меню, Pause toggle.

Нюансы Active Input Handling = **Both** — см. `CLAUDE.md`.

---

## 4. Player

### Машина (car)
**MonoBehaviour:** `CarController` — Unity API only (Rigidbody, WheelColliders, audio).
**Service:** `CarMovementService` — pure C#, считает целевые крутящие моменты колёс и торможение по input.
**Config:** `CarStatsConfig` (SO) — `Acceleration`, `TopSpeed`, `Mass`, `SteerAngle`, `BrakeForce`, `MaxArmor`, `NitroCapacity`, `NitroForce`, `WheelFriction`.

### Водитель (driver)
**MonoBehaviour:** `DriverController` — Animator + Ragdoll switch. В машине = parented, animated. После eject = detached, ragdoll active, Rigidbody.
**Service:** `DriverFlightService` — pure C#, во время Stunts управляет midair direction по `Look` input и применяет midair-boost.
**Config:** `DriverFlightConfig` (SO) — `BaseFlightSpeed`, `SteerInfluence`, `SteerSpeedPenalty` (потеря скорости при коррекции), `BoostImpulse`, `BoostCount` (== 1), `RotationBonusThreshold` (град/сек для засчёта вращения), `FlipScoreMultiplier`.

### Стек повреждений (Derby)
- **HP**: `MaxArmor` из `CarStatsConfig` + модификаторы апгрейдов.
- **Damage source**: `DamageService` обрабатывает `CollisionEvent`, считает урон как `relativeVelocity.magnitude * massRatio * config.DamageScale`.
- **Damage visualization**: `CarDamageVisualConfig` (SO) с массивом `DamageStage[]`. Стейджей 3 (Pristine → Dented → Wrecked), свап на пересечении HP-порога. Каждый стейдж описывает **комбинацию** визуальных эффектов из трёх взаимозаменяемых стратегий (см. ниже). Можно использовать одну стратегию или комбинировать несколько — определяется в SO без правки кода.

  **Структура стейджа:**
  ```csharp
  [Serializable] public struct DamageStage {
      public float          hpThreshold;        // ниже какого HP активируется
      // Стратегия A: Material/Texture swap (дёшево, всегда работает)
      public Material[]     materials;          // null = не менять материалы
      public GameObject[]   decalPrefabsToSpawn;// царапины/копоть, спавнятся через VfxFactory
      // Стратегия B: Mesh swap (если есть готовые деформированные меши)
      public Mesh           damagedMesh;        // null = не свапать меш
      // Стратегия C: Blend shape weight (если меш SkinnedMeshRenderer с blend shapes)
      public BlendShapeWeight[] blendShapeWeights; // массив {string name; float weight 0..100}
      // Общее для всех
      public VfxEffectType[] vfxToActivate;     // дым, огонь, искры — через VfxFactory
      public bool           enableDetachableParts; // разрешить отлёт частей при достаточном импульсе
  }
  ```

  **Стратегии (один контроллер, разные данные в SO):**
  - **A. Material/Texture swap** — самая дешёвая по perf, не требует Blender. Меш не меняется; материалы переключаются на «закопчёные/потёртые» варианты, спавнятся decal'ы царапин (через `URP Decal Projector`). Хорошо комбинируется с detachable parts.
  - **B. Mesh swap** — требует готовых деформированных мешей (вручную в Blender или через Claude + Blender MCP). Резкий, но визуально читаемый переход между стейджами.
  - **C. Blend shapes (Morph Targets)** — рекомендуется для mobile, если делаешь деформацию в Blender. Один FBX с несколькими blend shapes даёт **плавную** деформацию (lerp по weight), не дискретный swap. Mesh должен быть `SkinnedMeshRenderer` с одной фейковой root-bone. Mobile-friendly (используется тем же пайплайном, что blend shapes для лиц на iPhone).

  **Контроллер:** `CarDamageVisualController` (MonoBehaviour) читает текущий стейдж из SO и применяет все три стратегии параллельно — пустые поля игнорируются. Это даёт возможность стартовать срез на стратегии A (без Blender), а позже добавить B или C без правки кода.
- **Detachable parts**: на Wrecked-стейдже включаются `DetachablePart` (отдельные Rigidbody с фиксированным джойнтом), которые отваливаются от достаточно сильных ударов (`break force` на джойнте).
- **Death**: HP ≤ 0 → событие `CarDestroyedEvent` → авто-eject водителя (см. ниже) → машина переходит в "wrecked" коллайдер-only режим до конца матча.

### Eject (выброс водителя)
- В Derby: только автоматически при `CarDestroyedEvent`.
- В Stunts: автоматически при срабатывании trigger-зоны на трамплине (см. Level System).
- Реализация: `EjectCommand` (`ICommand`) — детачит `DriverController` от иерархии машины, применяет `Rigidbody.AddForce(forward + up, ForceMode.VelocityChange)` по конфигу, публикует `DriverEjectedEvent`, переключает Input Context на `Flight`.

---

## 5. Enemies (Derby AI)

### Контроллер
**MonoBehaviour:** `EnemyCarController` — wraps `CarController` + AI input.
**Service:** `EnemyAIService` — pure C#, реализует стейт-машину `Idle → Pursue → Ram → Recover`.

### Personalities (SO-профили)
**Config:** `EnemyPersonalityConfig` (SO) — выбирается из списка профилей в `DerbyMatchConfig`.

| Profile        | Target selection                                            | Поведение                                                 |
| -------------- | ----------------------------------------------------------- | --------------------------------------------------------- |
| `Aggressor`    | Ближайший живой                                             | Всегда таранит, не уклоняется, нитро на финальном рывке   |
| `Coward`       | Самый слабый по HP                                          | Тарань если HP жертвы < 30%, иначе уходит от боя          |
| `PlayerHunter` | Всегда игрок (если жив), fallback — ближайший               | Преследует игрока, использует pickup'ы агрессивно         |

Каждый профиль определяет: `RamAggression` (0–1), `RetreatHpThreshold`, `PickupPriority` (Nitro/Repair weight), `TargetSwitchCooldown`.

### Match composition
**Config:** `DerbyMatchConfig` (SO) — массив `EnemySpawnEntry { EnemyPersonalityConfig profile; CarStatsConfig car; }`, общее число от 3 до 6 (настраивается в одном SO).

---

## 6. Level System

### Derby Arena
Одна сцена `Derby_Arena_01`. Внутри:
- Круглая площадка с бортиками (Wall-коллайдеры).
- 4–6 hazards: бетонные блоки + бочки (`Rigidbody`, легко смещаемые) + деревянные заборы (разбиваются на отвалившиеся куски через `DestructibleObjectController` + `DestructibleConfig` SO).
- 2 спавн-зоны pickup'ов: `RepairPickup` (восстанавливает 40% HP) и `NitroPickup` (заполняет nitro-бар). Респаун через `PickupSpawnerService` с интервалом из SO.
- 7 SpawnPoint'ов для машин (1 игрок + до 6 AI), расставлены по периметру.

### Stunts: High Jump
Одна сцена `Stunts_HighJump`. Внутри:
- Прямая разгонная полоса (~80 метров).
- Трамплин-рамп с фиксированным углом.
- TriggerZone на верхушке рампа `EjectTriggerZone` — при пересечении машиной игрока вызывает `EjectCommand` (с учётом нюанса VContainer о kinematic Rigidbody — см. `CLAUDE.md`).
- Невидимый "boundary box" вверху и по сторонам — при выходе за него начинается фаза landing/score.
- `LandingZone` внизу — финал попытки.
- Декоративные measurement-маркеры по высоте (визуальные риски на столбе).

### Stunts Hub
UI-сцена `Stunts_Hub`. 6 карточек:
- 1 активная (`StuntsCardView` → загружает `Stunts_HighJump`).
- 5 заглушек (`StuntsCardView` в disabled state с лейблом "Coming soon").
Список карточек — `StuntsCatalogConfig` (SO), элемент: `StuntsCardEntry { StuntType type; string title; string sceneKey; bool enabled; Sprite preview; }`.

### Garage
UI-сцена `Garage`. Tabs: Cars / Upgrades.
- Cars tab: 3 карточки из `CarCatalogConfig` (SO). Купленные / выбранные сохраняются в `MetaProgressService` (PlayerPrefs).
- Upgrades tab: 3 категории × N уровней из `UpgradeCatalogConfig` (SO).

---

## 7. Weapons & Combat

«Оружие» в этом срезе — сама машина (таран) и тело водителя (в полёте).

### Derby combat
- Урон считается из коллизий: `ICollisionListener` на `CarController` ловит `OnCollisionEnter` → публикует `CarCollisionEvent { CarId attacker; CarId victim; float impactSpeed; Vector3 point; }` → `DamageService` подписан, конвертирует в HP-loss, публикует `CarDamagedEvent` и при достижении порога — `CarDestroyedEvent`.
- Pickup'ы: `IPickup` интерфейс, реализации `RepairPickup`, `NitroPickup`. Триггер-зоны с kinematic Rigidbody. Берёт первый коснувшийся `CarController`.
- Nitro: расход из `NitroService` (pure C#). При активном `Nitro` input — постоянная сила в forward, расход капасити.

### Stunts combat
Нет combat в классическом смысле — только физика полёта. Очки за высоту + множители за вращения водителя в воздухе (флипы по pitch/roll/yaw).

---

## 8. Economy

### Валюта
- `DerbyCoins` — одна валюта, хранится в `MetaProgressService`.
- Источники: убийство в Derby (per kill amount из `RewardConfig` SO), победа в Derby (bonus), очки за High Jump (по формуле `height * heightToCoinRatio + flipBonus`).

### Машины
**Config:** `CarCatalogConfig` (SO) — 3 entries.
Каждая `CarCatalogEntry { string id; string displayName; CarStatsConfig stats; int price; Sprite icon; GameObject prefab; }`.
Стартовая машина бесплатная и unlock'нута. Остальные две покупаются.

### Апгрейды
**Config:** `UpgradeCatalogConfig` (SO) — 3 категории, каждая с массивом уровней.
| Category  | Effect                                       |
| --------- | -------------------------------------------- |
| `Armor`   | `+X` к `MaxArmor`                            |
| `Power`   | `+X%` к `Acceleration` и `TopSpeed`          |
| `Nitro`   | `+X` к `NitroCapacity` и `+Y%` к `NitroForce`|

Каждый уровень: `UpgradeLevelEntry { int level; float effectValue; int price; }`. Прогрессия апгрейдов — per-car (или global, опционально через флаг в SO — задел на расширение).

Применение апгрейдов: при старте матча `CarStatsService` собирает effective stats из `CarStatsConfig` + текущих уровней апгрейдов из `MetaProgressService`.

### Сохранение
- PlayerPrefs только для meta-состояния (валюта, купленные машины, уровни апгрейдов, текущая выбранная машина, рекорды High Jump).
- Никаких сохранений матча — каждый Derby/Stunts стартует с чистого листа.

---

## 9. Game Flow & States

### Generic FSM (см. `CLAUDE.md`)
Enum `GameState`:
```
MainMenu,
GarageMeta,
StuntsHub,
DerbyMatch,
StuntsHighJumpDriving,
StuntsHighJumpFlight,
StuntsHighJumpScoring,
MatchEnd,
Paused
```

Каждое состояние — реализация `IState`, регистрируется в `GameFlowService`. Переходы публикуют `GameStateChangedEvent { GameState from; GameState to; }`.

### Ключевые переходы
| From                       | Event                       | To                         |
| -------------------------- | --------------------------- | -------------------------- |
| `MainMenu`                 | UI: Derby pressed           | `DerbyMatch`               |
| `MainMenu`                 | UI: Stunts pressed          | `StuntsHub`                |
| `MainMenu`                 | UI: Garage pressed          | `GarageMeta`               |
| `DerbyMatch`               | `MatchEndedEvent`           | `MatchEnd`                 |
| `MatchEnd`                 | UI: Continue                | `GarageMeta`               |
| `StuntsHub`                | UI: HighJump card           | `StuntsHighJumpDriving`    |
| `StuntsHighJumpDriving`    | `DriverEjectedEvent`        | `StuntsHighJumpFlight`     |
| `StuntsHighJumpFlight`     | `DriverLandedEvent`         | `StuntsHighJumpScoring`    |
| `StuntsHighJumpScoring`    | UI: Continue                | `GarageMeta`               |
| any gameplay state         | UI: Pause                   | `Paused` (push)            |
| `Paused`                   | UI: Resume                  | previous (pop)             |

`Paused` реализован как push-pop поверх предыдущего состояния — все остальные сервисы получают `GamePausedEvent` и приостанавливают свою работу (`Time.timeScale = 0` + явная проверка флага у тех, кому нельзя полагаться на timeScale).

---

## 10. UI / HUD

MVP-разделение строгое: View — только `MonoBehaviour` с UI-элементами и сигналами в Presenter; Presenter — pure C#; Model — `MetaProgressService` / runtime-сервисы.

### HUD: Derby
**View:** `DerbyHudView`.
- HP-бар игрока (заполнение слева направо, цвет по порогам).
- Nitro-бар.
- Количество живых соперников (счётчик).
- Иконки последних событий (kill notifications) — pooled `KillFeedItemView`.
- Pause-кнопка (mobile только).

### HUD: Stunts HighJump
**View:** `HighJumpHudView`.
- Фаза `Driving`: текущая скорость + подсказка "Hold W / accel".
- Фаза `Flight`: индикатор высоты (текущая / лучшая), статус midair-boost (charged / used), счётчик вращений.
- Фаза `Scoring`: финальный score + кнопка Continue.

### Stunts Hub
**View:** `StuntsHubView` + динамически создаваемые `StuntsCardView` по списку из `StuntsCatalogConfig`.

### Garage
**View:** `GarageView` + `CarCardView` (×3) + `UpgradeRowView` (×3 категории).

### Main Menu / Match End / Pause overlay
Стандартные `MainMenuView`, `MatchEndView`, `PauseOverlayView`.

---

## 11. Camera

Один корневой `MonoBehaviour` `CameraRig` + интерфейс `ICameraBehaviour`, конкретные реализации:
- `ChaseCarCameraBehaviour` — за машиной игрока, smooth follow, look-ahead по скорости. Активно в `DerbyMatch` и `StuntsHighJumpDriving`.
- `OrbitDriverCameraBehaviour` — свободная орбита вокруг ragdoll-водителя. Активно в `StuntsHighJumpFlight` и `StuntsHighJumpScoring`.

Переключение — `CameraService` по `GameStateChangedEvent`. Конфиги поведений — отдельные SO (`ChaseCameraConfig`, `OrbitCameraConfig`).

---

## 12. Architecture Mapping

> Все пути — относительно `Assets/Development/Scripts/` (см. `CLAUDE.md`).

### Core (фреймворк)
- `Core/Commands/EjectCommand.cs`
- `Core/Events/Events.Derby.cs` — `CarCollisionEvent`, `CarDamagedEvent`, `CarDestroyedEvent`, `DerbyMatchEndedEvent`
- `Core/Events/Events.Stunts.cs` — `DriverEjectedEvent`, `DriverLandedEvent`, `StuntsScoredEvent`, `FlipDetectedEvent`
- `Core/Events/Events.Meta.cs` — `CurrencyChangedEvent`, `CarPurchasedEvent`, `UpgradePurchasedEvent`
- `Core/Events/Events.Flow.cs` — `GameStateChangedEvent`, `GamePausedEvent`, `GameResumedEvent`
- `Core/Input/IInputProvider.cs`, `Core/Input/KeyboardInputProvider.cs`, `Core/Input/MobileInputProvider.cs`, `Core/Input/InputRouter.cs`, `Core/Input/InputContext.cs`
- `Core/StateMachine/StateMachine.cs` + `IState.cs`

### Models
- `Models/GameState.cs`
- `Models/CarId.cs`
- `Models/StuntType.cs`
- `Models/UpgradeCategory.cs`
- `Models/HazardType.cs`
- `Models/Tags.cs` (`static class` с константами тегов)
- `Models/AnimatorHashes.cs` (`static readonly int` через `Animator.StringToHash`)

### Data / Configs (SO-определения)
- `Data/Configs/Car/CarStatsConfig.cs`
- `Data/Configs/Car/CarDamageVisualConfig.cs`
- `Data/Configs/Car/CarCatalogConfig.cs`
- `Data/Configs/Driver/DriverFlightConfig.cs`
- `Data/Configs/Enemies/EnemyPersonalityConfig.cs`
- `Data/Configs/Match/DerbyMatchConfig.cs`
- `Data/Configs/Stunts/StuntsCatalogConfig.cs`
- `Data/Configs/Stunts/HighJumpRulesConfig.cs`
- `Data/Configs/Upgrades/UpgradeCatalogConfig.cs`
- `Data/Configs/Rewards/RewardConfig.cs`
- `Data/Configs/Pickups/PickupConfig.cs`
- `Data/Configs/Destructible/DestructibleConfig.cs`
- `Data/Configs/Camera/ChaseCameraConfig.cs`
- `Data/Configs/Camera/OrbitCameraConfig.cs`

Инстансы (`.asset`) — в `Assets/Development/Data/Configs/`.

### Services (pure C#)
- `Services/Car/CarMovementService.cs` + `ICarMovementService`
- `Services/Car/CarStatsService.cs` + `ICarStatsService` (собирает effective stats из конфига + апгрейдов)
- `Services/Combat/DamageService.cs` + `IDamageService`
- `Services/Combat/NitroService.cs` + `INitroService`
- `Services/Driver/DriverFlightService.cs` + `IDriverFlightService`
- `Services/Driver/FlipDetectorService.cs` + `IFlipDetectorService`
- `Services/AI/EnemyAIService.cs` + `IEnemyAIService`
- `Services/AI/TargetSelectionService.cs` + `ITargetSelectionService`
- `Services/Match/DerbyMatchService.cs` + `IDerbyMatchService` (управляет жизненным циклом матча: спавн, win-condition, выдача наград)
- `Services/Stunts/HighJumpScoreService.cs` + `IHighJumpScoreService`
- `Services/Pickups/PickupSpawnerService.cs` + `IPickupSpawnerService`
- `Services/Meta/MetaProgressService.cs` + `IMetaProgressService` (PlayerPrefs save/load)
- `Services/Meta/EconomyService.cs` + `IEconomyService`
- `Services/Flow/GameFlowService.cs` + `IGameFlowService`
- `Services/Camera/CameraService.cs` + `ICameraService`

### Controllers (MonoBehaviour, Unity API only)
- `Controllers/Car/CarController.cs`
- `Controllers/Car/CarDamageVisualController.cs` (mesh swap + detachable parts trigger)
- `Controllers/Car/DetachablePart.cs`
- `Controllers/Driver/DriverController.cs` (Animator + Ragdoll switch)
- `Controllers/Enemies/EnemyCarController.cs`
- `Controllers/Stunts/EjectTriggerZone.cs` (kinematic Rigidbody — см. `CLAUDE.md` про триггеры)
- `Controllers/Stunts/LandingZone.cs`
- `Controllers/Pickups/RepairPickup.cs`
- `Controllers/Pickups/NitroPickup.cs`
- `Controllers/Destructible/DestructibleObjectController.cs`
- `Controllers/Camera/CameraRig.cs`
- `Controllers/Camera/ChaseCarCameraBehaviour.cs`
- `Controllers/Camera/OrbitDriverCameraBehaviour.cs`
- `Controllers/Mobile/MobileInputCanvas.cs` (виртуальные стики и кнопки)

### Views (MVP, только UI)
- `Views/UI/MainMenu/MainMenuView.cs`
- `Views/UI/Hud/DerbyHudView.cs` + `KillFeedItemView.cs`
- `Views/UI/Hud/HighJumpHudView.cs`
- `Views/UI/Stunts/StuntsHubView.cs` + `StuntsCardView.cs`
- `Views/UI/Garage/GarageView.cs` + `CarCardView.cs` + `UpgradeRowView.cs`
- `Views/UI/Overlay/MatchEndView.cs`
- `Views/UI/Overlay/PauseOverlayView.cs`
- Presenters рядом (не View): `Views/UI/Garage/GaragePresenter.cs` и т.п.

### Factories (единственное место доступа к `IPoolManager`)
- `Factories/Car/CarFactory.cs` + `ICarFactory` (создаёт игрока и AI-машины по конфигам, после `pool.Get()` делает `_container.InjectGameObject(go)` + явный `Launch(...)`)
- `Factories/Pickup/PickupFactory.cs` + `IPickupFactory`
- `Factories/VFX/VfxFactory.cs` + `IVfxFactory` (искры столкновений, дым от повреждений)
- `Factories/Driver/DriverRagdollFactory.cs` + `IDriverRagdollFactory` (опционально, если ragdoll создаётся отдельно от машины)
- `Factories/Destructible/DestructibleFactory.cs` + `IDestructibleFactory`

### Installers
- `Installers/GameLifetimeScope.cs` — Composition Root: регистрирует все сервисы, фабрики, конфиги, MonoBehaviour-контроллеры через `RegisterComponent` (а не `RegisterInstance` — см. `CLAUDE.md`), выбирает `IInputProvider` по платформе.

### Bootstrap
- `GameBootstrap.cs` — точка входа, инициализирует контейнер, грузит первую сцену через `GameFlowService`.

---

## 13. EventBus Events

Все события — `readonly struct`, опубликованы через `IEventBus` (см. `CLAUDE.md`).

| Event                       | Payload                                                          | Publisher                       |
| --------------------------- | ---------------------------------------------------------------- | ------------------------------- |
| `CarCollisionEvent`         | `CarId attacker, CarId victim, float impactSpeed, Vector3 point` | `CarController` (через коллбэк) |
| `CarDamagedEvent`           | `CarId target, float damage, float remainingHp`                  | `DamageService`                 |
| `CarDestroyedEvent`         | `CarId target, CarId killer`                                     | `DamageService`                 |
| `DriverEjectedEvent`        | `CarId source, Vector3 ejectVelocity, EjectReason reason`        | `EjectCommand`                  |
| `DriverLandedEvent`         | `float peakHeight, int flipCount, float totalAirTime`            | `DriverFlightService`           |
| `FlipDetectedEvent`         | `FlipAxis axis, float speedDegPerSec`                            | `FlipDetectorService`           |
| `StuntsScoredEvent`         | `StuntType type, int score, bool isNewRecord`                    | `HighJumpScoreService`          |
| `DerbyMatchEndedEvent`      | `CarId winner, int playerKills, float duration`                  | `DerbyMatchService`             |
| `CurrencyChangedEvent`      | `int oldValue, int newValue, int delta`                          | `EconomyService`                |
| `CarPurchasedEvent`         | `string carId`                                                   | `EconomyService`                |
| `UpgradePurchasedEvent`     | `UpgradeCategory category, int newLevel`                         | `EconomyService`                |
| `GameStateChangedEvent`     | `GameState from, GameState to`                                   | `GameFlowService`               |
| `GamePausedEvent`           | (empty)                                                          | `GameFlowService`               |
| `GameResumedEvent`          | (empty)                                                          | `GameFlowService`               |

---

## 14. Scalability Hooks

Расширения проекта делятся на **три категории по стоимости**. Категория = объём работы программиста, а не сложность задачи.

### 14.1 Pure content — только SO и ассеты, кода НЕ трогаем

Это и есть «масштабирование через ScriptableObject». Может делать геймдизайнер без программиста.

- **Дополнительные машины** — entries в `CarCatalogConfig` + prefab + `CarStatsConfig` SO + 3 mesh-стейджа повреждений + (опц.) entry в `DetachablePartsConfig`.
- **Дополнительные уровни существующих апгрейдов** — entries в `UpgradeLevelEntry[]` внутри `UpgradeCatalogConfig`.
- **Дополнительные personality-профили AI** — новые `EnemyPersonalityConfig` SO, добавляются в `DerbyMatchConfig`.
- **Балансировка экономики** — числа в `RewardConfig` и каталогах.
- **Косметика машин/водителя** — материалы как поле в `CarCatalogConfig` (опционально).
- **Новые hazards / разрушаемые объекты на арене** — новый prefab + `DestructibleConfig` SO.

### 14.2 Light code — 1 класс + конфиги + сцена/UI

Работа программиста на 1–3 дня. Существующие сервисы не переписываются, только добавляется новый.

- **Новая Derby-арена** — новая сцена + `ArenaCatalogConfig` SO + selector-логика в `DerbyMatchService` (выбор арены по конфигу матча). Запускать через editor-скрипт `FlatOutLike/Scenes/Bake NavMesh For Arena`.
- **Новый тип pickup'а** — новая реализация `IPickup` + `PickupConfig` SO + регистрация в `PickupSpawnerService`. Inteface и spawner уже готовы.
- **Гонки на трассе** — новый `GameState.RaceMatch` + `RaceMatchService` + сцена с чекпоинтами + новый HUD. AI-машины уже умеют ездить, нужен только waypoint-following вместо combat-логики (новая стейт-машина в `EnemyAIService` или отдельный `RacingAIService`).
- **Серверные leaderboards** — `MetaProgressService` уже изолирует source-of-truth; PlayerPrefs-реализация заменяется на серверную через тот же интерфейс. Объём = только интеграция SDK.

### 14.3 Heavy code — отдельная игровая система (НЕ контент)

Это **не масштабирование, а разработка**. Каждый пункт = новый ICommand/IService/IConfig стек + UI + 2–7 дней программирования. Архитектура к этому готова (через DI), но `Stunts*` и подобные стоит планировать как полноценные фичи, а не «добавил SO».

- **Новый Stunts-трюк** (Long Jump, Fire Rings, Bowling, Field Goal, High Card) — для каждого:
  - Новый `StuntType` enum value + entry в `StuntsCatalogConfig` (это тривиально).
  - Новая сцена с уникальной геометрией трюка.
  - Новый `IStuntsScoreService` (правила подсчёта очков уникальны: «пролететь сквозь кольцо», «сбить кегли», «забить гол» — это разная логика, общего scoring нет).
  - Новые контроллеры под объекты трюка: `FireRingController`, `BowlingPinController`, `GoalPostController` и т.п.
  - Новые events (`RingPassedEvent`, `PinKnockedEvent`, `GoalScoredEvent`).
  - Новый HUD-view для каждого трюка (показывает то, что специфично — счётчик колец, счётчик кеглей, и т.д.).
  - Часть каркаса переиспользуется бесплатно: eject (через `EjectCommand`), полёт (`DriverFlightService`), ragdoll (`DriverController`), orbit-camera. Это экономит ~30–40% работы на каждом трюке по сравнению с написанием с нуля. Но **не 100%**.
- **Активные перки и pre-match loadout** — новый enum `PerkType` или система тегов, `IActivePerkService` (применяет эффекты во время матча по input или по триггеру), UI выбора и экипировки перков, расширение `MetaProgressService` (хранение слотов и unlocked-перков). Полноценная фича.
- **Branching upgrade trees** — замена линейного `UpgradeLevelEntry[]` на graph-структуру с зависимостями. Затрагивает `EconomyService`, `CarStatsService`, всё UI прокачки. Новые конфиги, новый presenter в Garage.
- **Уникальные апгрейды на конкретные машины** — переход с глобального прогресса апгрейдов на per-car. Изменение `MetaProgressService` save-формата + миграция сохранений.

Прозрачное правило: **если в существующих сервисах надо менять интерфейс или логику — это 14.3, а не 14.1.**

---

## 15. Mid-core Progression Systems

В вертикальный срез эти системы НЕ входят, но mid-core игры на них живут (retention D1–D30, монетизация). Раздел нужен, чтобы:
1. Не сделать `MetaProgressService` богом-объектом, в который потом ничего нельзя добавить.
2. Заранее обозначить, **где архитектурно** будут жить эти системы — чтобы CLI при имплементации среза не закрыл им путь.

### 15.1 Куда они архитектурно ложатся

Все системы прогрессии — это новые сервисы под `Services/Progression/` (новая папка) + новые конфиги под `Data/Configs/Progression/` + новые UI-сцены/виды + новые events в `Core/Events/Events.Progression.cs`. Они **подписываются** на уже существующие события (`DerbyMatchEndedEvent`, `StuntsScoredEvent`, `CarPurchasedEvent`) — никаких изменений в существующих сервисах не требуется.

`MetaProgressService` остаётся source-of-truth для **профиля игрока** (валюта, машины, апгрейды). Прогрессия-системы хранят свои данные **отдельно**:
- `IMissionsProgressService` — состояние daily missions.
- `ISeasonProgressService` — XP, текущий tier баттлпасса.
- `IEnergyService` — текущая энергия, время до восстановления.
- `ITournamentService` — текущая позиция в лиге, история матчей сезона.

Это **строго отдельные сервисы**, потому что:
- Их save-формат меняется чаще (новые сезоны, новые типы миссий) — не хочется деформировать главный профиль каждый раз.
- Их легко выпиливать или включать через feature flags.
- При переезде на cloud save их можно мигрировать независимо.

### 15.2 Список систем с примерным объёмом работы

| Система | Что делает | Объём (программист) | Когда внедрять |
|---|---|---|---|
| **Daily Missions** | «Убей 5 машин в Derby», «Сделай 3 флипа» — фоновые задачи с reward | 3–5 дней | Сразу после среза (D1 retention) |
| **Player Level / XP** | XP за матчи, уровни дают unlock'и (новые машины/трюки) | 2–3 дня | Сразу после среза |
| **Battle Pass / Season** | Сезонный track из 30–50 tier'ов с reward'ами, free + paid треки | 7–14 дней | После первой стабильной версии |
| **Energy / Lives** | Ограничение количества попыток в час (мягкая стенка для монетизации) | 2 дня | Только если монетизация требует |
| **Tournaments / Leagues** | AI-оппоненты с растущей сложностью, недельные лиги, leaderboard | 5–10 дней | Mid-game retention |
| **IAP Shop** | Покупка валюты, скинов, паков за реальные деньги | 3 дня + SDK интеграция | Когда есть, что продавать |
| **Rewarded Ads** | Удвоить reward за матч, доп. попытка за рекламу | 1–2 дня + SDK | Параллельно с IAP |
| **Cloud Save** | PlayFab / Firebase / UGS | 2–3 дня | Перед публичным релизом |
| **Loot boxes / gacha** | Crate'ы с случайным дропом машин/скинов | 5–7 дней | Опционально, риск регуляторов |

### 15.3 Что нужно ВПИСАТЬ в архитектуру среза прямо сейчас (бесплатные предохранители)

Эти изменения занимают минуты при изначальной имплементации, но делают будущее расширение безболезненным. **CLI должен их применить при работе над срезом.**

- **Папка `Services/Progression/` создаётся пустой** — чтобы новые сервисы прогрессии падали туда, а не в `Services/Meta/`.
- **`MetaProgressService` инкапсулирует доступ через `IPlayerProfile`** — отдельный интерфейс, описывающий только текущие данные среза (currency, owned cars, upgrade levels). Не «MetaProgress = вся мета», а «MetaProgress = текущий профиль». Это позволит позже добавить параллельные `IMissionsProgress`, `ISeasonProgress` без переписки.
- **Все источники reward'ов публикуют унифицированное событие `RewardEarnedEvent { RewardSource source, int coinAmount, int xpAmount }`** — даже если `xpAmount` пока всегда 0 (XP-системы нет). Когда появится Player Level, он просто подписывается на это событие и считает XP. Никаких правок в `DerbyMatchService` / `HighJumpScoreService`.
- **`GameFlowService` поддерживает push-pop состояний (для Pause)** — этот же механизм потом используется для модалок Daily Missions, Battle Pass UI, Shop UI поверх любого экрана. Не делать «pause через флаг», делать через push-pop сразу.
- **`MetaProgressService` save/load выделен в отдельный `IProfileStorage` интерфейс** с реализацией `PlayerPrefsProfileStorage`. Когда придёт время cloud save — добавляется `CloudProfileStorage`, регистрация меняется в `GameLifetimeScope`, остальной код не трогается.

---

## 16. Acceptance Criteria

Срез считается готовым, когда **все** пункты ниже выполняются.

### Derby
- [ ] Игрок может стартовать Derby-матч из главного меню и попадает на арену с 3–6 AI-машинами (число читается из `DerbyMatchConfig`).
- [ ] Игрок управляет машиной: разгон, поворот, ручник, нитро.
- [ ] Машины получают физический урон при столкновениях; HP уменьшается; визуальные стейджи переключаются на пороговых значениях (через выбранную стратегию в `CarDamageVisualConfig` — material swap / mesh swap / blend shapes); на финальном стейдже отваливаются части хотя бы при сильном ударе.
- [ ] При HP ≤ 0 машина перестаёт ехать, водитель автоматически вылетает через лобовое.
- [ ] AI трёх разных профилей ведёт себя различимо (визуально проверяемо: Aggressor лезет напролом, Coward убегает при низком HP цели нет, PlayerHunter преследует игрока).
- [ ] Pickup'ы (repair, nitro) подбираются и работают, респаунятся по таймеру.
- [ ] Hazards (бочки, заборы) реагируют на удар (смещаются / разрушаются).
- [ ] Матч заканчивается, когда остаётся одна машина; объявляется победитель; начисляются coins.
- [ ] При смерти игрока матч продолжается до победителя или досрочного выхода (UX-вариант: показать "you died" + кнопку "watch / leave").

### Stunts: High Jump
- [ ] Из главного меню / Stunts Hub игрок попадает в `Stunts_HighJump`.
- [ ] Игрок управляет машиной, разгоняется по полосе, въезжает на трамплин.
- [ ] На верхушке рампа срабатывает trigger-зона, происходит авто-eject: водитель отделяется от машины, ragdoll-Rigidbody получает forward+up импульс.
- [ ] Камера переключается на orbit вокруг водителя.
- [ ] В полёте `Look`-инпут корректирует направление полёта со штрафом к скорости.
- [ ] Кнопка midair-boost доступна один раз за полёт, даёт импульс.
- [ ] Считаются флипы (вращения), множитель применяется к финальному счёту.
- [ ] При приземлении замеряется пиковая высота, показывается результат, обновляется рекорд в PlayerPrefs.

### Garage / Meta
- [ ] Игрок видит баланс DerbyCoins.
- [ ] Игрок может купить машину (если хватает coins) и выбрать её как активную.
- [ ] Игрок может купить апгрейд (Armor / Power / Nitro); следующий матч применяет апгрейд.
- [ ] Прогресс сохраняется между сессиями (PlayerPrefs).

### Stunts Hub
- [ ] Видно 6 карточек; 1 активная, 5 disabled с лейблом.
- [ ] Активная карточка ведёт в `Stunts_HighJump`.

### Поток состояний и UI
- [ ] Main Menu → любой режим → MatchEnd / Scoring → Garage → Main Menu — переходы работают без багов.
- [ ] Pause работает во всех геймплейных состояниях, корректно резюмит.
- [ ] HUD показывает релевантные данные в нужной фазе.

### Платформы и input
- [ ] На PC играется клавиатурой и мышью.
- [ ] На мобиле (или в Unity Remote / Simulator) работают виртуальные стики и кнопки.
- [ ] Переключение между провайдерами — через `GameLifetimeScope`, без правки контроллеров.

### Тех-критерии
- [ ] Нет `Resources.Load` нигде в коде.
- [ ] Все идентификаторы (теги, animator-параметры, ключи пулов, имена сцен) — в enum/const, не в строках по месту.
- [ ] Все игровые параметры — в SO, никаких magic numbers в Controllers.
- [ ] Все триггер-зоны (`EjectTriggerZone`, `LandingZone`, pickup'ы) имеют kinematic Rigidbody.
- [ ] Объекты из пула проходят `InjectGameObject` и явный `Launch(...)` (нет инициализации в `Start` / `OnEnable`).
- [ ] Все MonoBehaviour-зависимости в `GameLifetimeScope` регистрируются через `RegisterComponent`, не `RegisterInstance`.

---

## 17. Editor Tooling

Любая рутина — через `[MenuItem("FlatOutLike/<Category>/<Action>")]`. Иерархия и правила — `CLAUDE.md` + skill `add-editor-script` + `.claude/rules/editor-files.md`.

Скрипты, требуемые для среза:

- `EDITOR_SCRIPT_NEEDED: FlatOutLike/Scenes/Bake NavMesh For Derby Arena` — bake NavMesh в `Derby_Arena_01` (если AI будет использовать NavMesh для path-planning; если чисто Rigidbody-based — можно опустить).
- `EDITOR_SCRIPT_NEEDED: FlatOutLike/Setup/Setup Car Damage Visual` — выделить prefab машины → заполнить `CarDamageVisualConfig` Pristine-стейдж (текущие материалы), зарегистрировать DetachablePart'ы, проверить какие стратегии применимы (есть ли blend shapes на SkinnedMeshRenderer → подсказать использовать стратегию C; есть ли альтернативные меши в папке → подсказать стратегию B; иначе — генерируется заготовка под стратегию A с пустыми material slot'ами). Скрипт стратегия-агностик: НЕ предполагает, какая стратегия будет использована.
- `EDITOR_SCRIPT_NEEDED: FlatOutLike/Setup/Setup Driver Ragdoll` — настроить ragdoll на Mixamo-риге (учесть нюанс из `CLAUDE.md`: Ragdoll Setup Wizard может ломаться на Mixamo).
- `EDITOR_SCRIPT_NEEDED: FlatOutLike/Setup/Add Kinematic Rigidbody To Selected Triggers` — добавить kinematic Rigidbody на все выделенные триггер-зоны (защита от забывания).
- `EDITOR_SCRIPT_NEEDED: FlatOutLike/Configs/Create Empty SO Set` — создаёт пустые `.asset`-инстансы всех ключевых SO в `Assets/Development/Data/Configs/` с правильной структурой папок.
- `EDITOR_SCRIPT_NEEDED: FlatOutLike/Validation/Validate Composition Root` — проверка, что все интерфейсы из списка зарегистрированы в `GameLifetimeScope` и нет `RegisterInstance` там, где должен быть `RegisterComponent` для MonoBehaviour.
- `EDITOR_SCRIPT_NEEDED: FlatOutLike/Validation/Find Magic Strings` — поиск строковых литералов в Controllers, которые должны быть в enum/const (теги, animator-параметры).
- `EDITOR_SCRIPT_NEEDED: FlatOutLike/Spawn/Populate Derby Arena Spawn Points` — расставить 7 SpawnPoint'ов по окружности арены равномерно.
- `EDITOR_SCRIPT_NEEDED: FlatOutLike/Stunts/Build HighJump Ramp` — построить рамп с заданным углом и длиной из параметров окна, с готовой `EjectTriggerZone` на верхушке (kinematic Rigidbody уже добавлен).

---

## 18. Open Questions

Пункты, требующие решения **до начала имплементации** или **в первой итерации после первого playable build**. Ответы превращаются в правки SO-конфигов и не должны менять архитектуру.

1. **NavMesh vs Rigidbody-only AI.** Использует ли `EnemyAIService` NavMesh для path-planning к цели, или чисто рулит машиной через `IInputProvider`-mock + проверки видимости? — Влияет на Editor-скрипт baking'а и на DLOD.
2. **Точные пороги mesh-стейджей.** Сейчас 3 стейджа условно (Pristine → Dented → Wrecked). По каким HP%? — 100/66/33? 100/50/20? Подбор по playtest.
3. **Сколько уровней у каждого апгрейда.** 3? 5? — Влияет только на размер SO.
4. **Формула очков High Jump.** `score = height_m * 10 + flipBonus * multiplier` — нужна точная формула с playtest-балансом.
5. **Цены машин/апгрейдов.** Стартовый баланс exposed в `RewardConfig` и каталогах — нужны конкретные значения после первого playtest.
6. **Что происходит при смерти игрока в Derby.** Спектатор-камера до конца матча? Сразу в Garage? Перезапуск? — UX-решение.
7. **Стратегия визуализации повреждений.** В `CarDamageVisualConfig` доступны три стратегии (A: material swap, B: mesh swap, C: blend shapes). На какой стартовать срез? Рекомендация: A на старте (0 работы в Blender), переход на C через Claude + Blender MCP после playtest, если визуал не дотянет. Решение по факту первого playable build.
8. **Звук.** Не входит в acceptance criteria среза формально, но без него ощущения от игры будут слабые. Подключить заглушки через `IAudioService` сразу или отложить?

---

## 19. Implementation Order (рекомендация для Claude Code CLI)

Чтобы получить играбельный билд максимально быстро, реализовывать в таком порядке:

1. **Каркас**: `GameLifetimeScope`, `GameBootstrap`, `IInputProvider` (keyboard сначала), `IEventBus`, FSM, базовые enum/const/Tags/AnimatorHashes.
2. **Машина одна, без врагов**: `CarController` + `CarMovementService` + `CarStatsConfig` + `ChaseCarCameraBehaviour`. Цель: ездить по пустой сцене.
3. **Урон и визуал**: `DamageService`, `CarDamageVisualController`, `CarCollisionEvent`. Цель: машина "ломается" при ударе об стену.
4. **AI**: `EnemyAIService` + `EnemyCarController` + 1 personality. Цель: одна вражеская машина таранит игрока.
5. **Derby-матч**: `DerbyMatchService`, win/lose, 3 personality, спавн 3–6 AI, hazards и pickup'ы. Цель: первый играбельный Derby.
6. **Eject + полёт**: `EjectCommand`, `DriverController` ragdoll switch, `DriverFlightService`, `OrbitDriverCameraBehaviour`. Цель: водитель вылетает при гибели машины.
7. **High Jump**: сцена, рамп, `EjectTriggerZone`, `LandingZone`, `HighJumpScoreService`, `FlipDetectorService`. Цель: первая Stunts-попытка.
8. **Meta**: `MetaProgressService`, `EconomyService`, Garage UI, Stunts Hub. Цель: цикл «играй → копи → апай → играй».
9. **Mobile input**: `MobileInputProvider` + `MobileInputCanvas`. Цель: играется на телефоне.
10. **Polish**: kill-feed, pause, баланс через playtest, ответы на Open Questions.

После каждого шага — playable build, проверяемый по соответствующим пунктам Acceptance Criteria.
