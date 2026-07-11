# CLI Workflow — работа с Claude Code CLI по проекту FlatOutLike

Этот документ — практический регламент работы с Claude Code CLI на этом конкретном проекте. Не методичка по CLI вообще, а **что делать пошагово**, чтобы не утонуть в переделках.

Источники правды для CLI:
- `CLAUDE.md` — архитектурные правила, нюансы Unity/VContainer, workflow оркестратора, ссылки на скилы.
- `docs/game-design.md` — ТЗ вертикального среза.
- Этот файл — регламент промптинга.

---

## Базовые принципы

### 1. Один шаг — один промпт — одна проверка в Unity

Не «реализуй весь срез», не «сделай несколько шагов сразу». Один шаг из Implementation Order (§19 `game-design.md`) = одна сессия CLI = один Unity-checkpoint. После каждого шага запускаешь Unity, проверяешь acceptance-критерий, коммитишь.

### 2. План всегда перед кодом

В любом промпте, который требует от CLI писать новый код, **обязательно** есть фраза «Сначала покажи план файлов, жди моего "ок" перед началом написания». Без этого Claude может уйти в 2000 строк не туда. Прочитать план занимает 5 минут, откат плохого кода — час.

### 3. Источник правды — документы, не «общие знания»

В промпте всегда явная ссылка на конкретный раздел `game-design.md` или `CLAUDE.md`. «Сделай как в FlatOut 2» или «используй best practices» — фразы под запретом. Только: «по §X документа Y».

### 4. Запрет на расширение скоупа

В каждом промпте есть запрет на самодеятельность: «не выдумывай новые сервисы или конфиги сверх плана, если хочется добавить — спрашивай». Иначе CLI «из лучших побуждений» добавит 5 «полезных» классов, которых в game-design нет, и потом аудит вылавливает их три часа.

### 5. После каждых 3–4 шагов — архитектурный аудит

Накопление долга в AI-сгенерированном коде — реальная проблема. Лучше ловить нарушения еженедельно, чем разгребать в конце.

---

## Сессия 0 — Первичная инициализация CLI

Запускается **один раз** при старте проекта, либо когда начинаешь новую сессию после долгого перерыва. Цель — синхронизировать понимание Claude с реальным состоянием проекта.

### Промпт сессии 0

```
Прочитай CLAUDE.md и docs/game-design.md в корне проекта.
Это твои единственные источники правды по архитектуре и геймдизайну.
Если что-то в твоих действиях будет противоречить им — приоритет 
у этих документов.

Также просмотри структуру Assets/ командой ls/tree (особенно
Assets/Development/ и Assets/ThirdParty/), чтобы понять что уже есть.
Не читай содержимое файлов в ThirdParty — это покупные ассеты, 
не наш код.

После этого ответь ТОЛЬКО на следующие вопросы, ничего не делая:
1. Какие шаги из Implementation Order (§19 game-design.md) уже 
   выполнены судя по структуре проекта? Какие пусты?
2. Какие сторонние ассеты ты видишь в Assets/ThirdParty/ и 
   как они мапятся на потребности проекта 
   (машины / арена / VFX / UI / audio)?
3. Установлен ли VContainer? Если да — какая версия. 
   Если нет — это блокер, надо ставить через Package Manager 
   до начала работы.
4. Какие правила из CLAUDE.md и .claude/rules/ ты считаешь 
   наиболее критичными для первой задачи 
   (Implementation Order шаг 1: каркас)?

НЕ начинай писать код, пока я не дам команду. Сначала ответы 
на вопросы.
```

### Что проверять в ответе

- Claude корректно идентифицировал содержимое `Assets/ThirdParty/` (видит 1Poly Cars, Battle Stadium и т.д.).
- Понимает, что для текущего этапа мы используем **стратегию A** из §4 game-design (material swap), без Blender и mesh-стейджей.
- Упоминает критичные нюансы из CLAUDE.md: `RegisterComponent` vs `RegisterInstance`, kinematic Rigidbody на триггерах, инъекция объектов из пула.
- Не предлагает «общую структуру проекта» от себя — а опирается на `Assets/Development/Scripts/` иерархию из CLAUDE.md.

Если ответ слабый — задаёшь уточняющие вопросы до тех пор, пока понимание не станет адекватным. Только потом переходишь к сессии 1.

---

## Сессия 1 — Implementation Order шаг 1 (Каркас)

### Промпт

```
Реализуй шаг 1 из Implementation Order (§19 game-design.md): 
«Каркас».

Конкретно: GameLifetimeScope, GameBootstrap, IInputProvider 
(только KeyboardInputProvider пока, MobileInputProvider — позже 
на шаге 9), IEventBus, generic FSM, базовые enum/const 
(GameState с состоянием BaseHub, Tags, AnimatorHashes, CarId, 
StuntType, UpgradeCategory, HazardType, BuildingId, RewardSource), 
ITimeProvider + SystemTimeProvider в Core/Time/, и пустые папки 
для будущих сервисов включая Services/Progression/ (см. §15.3).

Регламент работы:
- Сначала покажи план: список файлов которые создашь, в каком 
  порядке, и какие интерфейсы у каждого сервиса. Жди моего 
  «ок» перед началом написания кода.
- Используй скилы из CLAUDE.md: основная работа через 
  code-writer, по завершении автоматически прогоняй 
  code-reviewer (как описано в Workflow секции CLAUDE.md).
- Если для какой-то задачи нужен Editor-скрипт 
  (например, создание SO-инстансов или Composition Root setup) — 
  помечай EDITOR_SCRIPT_NEEDED: и параллельно делегируй 
  editor-script-creator.
- Не лезь в задачи, относящиеся к последующим шагам 
  (никакой логики машин, AI, повреждений и т.д.) — только каркас.
- Не выдумывай новые сервисы или конфиги сверх перечисленных
  в плане. Если возникает желание добавить что-то — сначала 
  спрашивай меня.

Также проверь и применяй §15.3 mid-core предохранители:
- Папка Services/Progression/ создана пустой.
- MetaProgressService будет создаваться через IPlayerProfile 
  абстракцию (даже если сейчас в IPlayerProfile только заглушки 
  под currency).
- GameFlowService поддерживает push-pop состояний с самого начала, 
  не «pause через флаг».
- Save/load выделен в IProfileStorage интерфейс с реализацией 
  PlayerPrefsProfileStorage.
- Всё время — через ITimeProvider (SystemTimeProvider, UTC), 
  никакого DateTime.Now по коду.

После завершения шага 1 жди от меня результат проверки в Unity, 
прежде чем переходить дальше.
```

### Что проверять в Unity после шага 1

- Пустая сцена с `GameBootstrap` стартует без ошибок в Console.
- Логирование показывает, что `GameLifetimeScope` инициализировался, `IEventBus` зарегистрирован.
- `GameFlowService` стартует в `BaseHub` состоянии (до шага 11 — временная сцена-заглушка `_TempHubMenu`).
- Структура папок `Assets/Development/Scripts/` соответствует CLAUDE.md.

---

## Сессии 2-13 — Шаблон промпта для остальных шагов

После того, как шаг N проверен и работает, переходишь к шагу N+1 по этому шаблону. Имена и детали шагов смотри в Implementation Order (§19 `game-design.md`).

### Универсальный шаблон промпта

```
Шаг <N-1> проверен и работает. Переходим к шагу <N> из 
Implementation Order (§19 game-design.md): «<название шага>».

Что должно появиться:
`CarController` + `CarMovementService` + `CarStatsConfig` + `ChaseCarCameraBehaviour`.

- Использовать prefab из `Assets/ThirdParty/1Poly_DestructibleCars/` для visual; не создавать свою машину.
- `CarMovementService` — pure C#, физика крутящих моментов на колёсах через `CarController` (MonoBehaviour).
- `ChaseCarCameraBehaviour` — отдельный класс, не зашивать камеру в `CarController`.
- Тестовая сцена `_TestDriving` (с подчёркиванием = временные сцены) с plane-полом.

Цель шага по game-design: Цель: ездить по пустой сцене.

Регламент тот же, что и на предыдущих шагах:
1. Сначала план файлов, жди ок.
2. code-writer для нового кода, code-reviewer параллельно.
3. EDITOR_SCRIPT_NEEDED: помечай и делегируй editor-script-creator.
4. Не лезь в последующие шаги.
5. После завершения — жди моей проверки.

Дополнительные ограничения для этого шага:
- <специфика, например: «для машин используй prefab Car_01 
  из Assets/ThirdParty/1Poly_DestructibleCars/, не создавай свой меш»>
- <«для damage visual использовать стратегию A (material swap) 
  из §4 game-design — пустые поля для damagedMesh и blendShapeWeights»>
- <«для арены использовать Battle Stadium из 
  Assets/ThirdParty/BattleStadium/, конкретно префаб stadium_main»>
```

### Конкретные дополнительные ограничения по шагам

Эти заметки добавляешь в раздел «Дополнительные ограничения» промпта на соответствующем шаге.

**Шаг 2 (Машина одна, без врагов):**
- Использовать prefab из `Assets/ThirdParty/1Poly_DestructibleCars/` для visual; не создавать свою машину.
- `CarMovementService` — pure C#, физика крутящих моментов на колёсах через `CarController` (MonoBehaviour).
- `ChaseCarCameraBehaviour` — отдельный класс, не зашивать камеру в `CarController`.
- Тестовая сцена `_TestDriving` (с подчёркиванием = временные сцены) с plane-полом.

**Шаг 3 (Урон и визуал):**
- `CarDamageVisualConfig` поддерживает все три стратегии из §4 game-design, но в SO-инстансах для среза заполнять **только стратегию A** (material slot + decals + VFX).
- Поля `damagedMesh` и `blendShapeWeights` в SO оставлять пустыми (null/empty array).
- `CarDamageVisualController` применяет все три стратегии параллельно, пустые поля просто игнорируются — никаких if/else по «активной стратегии» в коде.
- Detachable parts — отдельные дочерние GameObject'ы с Rigidbody и FixedJoint, регистрируются через `Setup Car Damage Visual` editor-скрипт.

**Шаг 4 (AI):**
- Один personality профиль `Aggressor` для начала, остальные два (Coward, PlayerHunter) — на шаге 5.
- `EnemyAIService` — pure C#, не MonoBehaviour. Получает `IInputProvider`-mock для подачи команд в `CarController` через тот же интерфейс, что и игрок.
- NavMesh пока не используем (Open Question #1 — решение: чисто Rigidbody-based AI на старте).

**Шаг 5 (Derby-матч):**
- `DerbyMatchConfig` SO определяет состав матча: массив `EnemySpawnEntry`.
- Hazards и pickup'ы — entries в конфиге арены, не хардкод в коде.
- 3 personality профиля как 3 разных `EnemyPersonalityConfig` SO в `Assets/Development/Data/Configs/Enemies/`.

**Шаг 6 (Eject + полёт):**
- `EjectCommand` реализует `ICommand` (см. CLAUDE.md).
- `DriverController` — switch между Animated mode (parented в машине) и Ragdoll mode (detached, Rigidbody).
- Mixamo водитель + ragdoll: использовать editor-скрипт `Setup Driver Ragdoll` с учётом нюанса из CLAUDE.md (Mixamo ломает Ragdoll Wizard).
- Камера: `CameraService` переключает между `ChaseCarCameraBehaviour` и `OrbitDriverCameraBehaviour` по `GameStateChangedEvent`.

**Шаг 7 (High Jump):**
- Сцена `Stunts_HighJump` собирается editor-скриптом `Build HighJump Ramp`, не вручную.
- `EjectTriggerZone` обязательно с **kinematic Rigidbody** (см. CLAUDE.md — иначе триггер не сработает).
- `FlipDetectorService` детектирует вращения по углам Эйлера ragdoll'а, пороги — в `DriverFlightConfig`.

**Шаг 8 (Meta):**
- `IProfileStorage` интерфейс уже создан на шаге 1 (§15.3 предохранитель). Сейчас реализуется `PlayerPrefsProfileStorage`.
- `EconomyService` публикует `RewardEarnedEvent` с XP-полем (даже если XP пока = 0) — §15.3 предохранитель.
- Garage — MVP: View + Presenter, читают из `IPlayerProfile`.
- Вместо Stunts Hub — временная заглушка `_TempHubMenu` с кнопками Derby / High Jump / Garage (заменяется картой BaseHub на шаге 11; отдельный Stunts Hub UI не делаем).

**Шаг 9 (Mobile input):**
- `MobileInputProvider` реализует тот же `IInputProvider`, что и `KeyboardInputProvider`.
- Выбор провайдера в `GameLifetimeScope` через `Application.platform` или флаг.
- `MobileInputCanvas` — отдельная сцена/префаб с виртуальными стиками; используем UI-ассеты из `Assets/ThirdParty/<UI pack>/`.

**Шаг 10 (Polish кора):**
- На этом этапе уже не «делаем фичу», а закрываем acceptance criteria кора из §16 game-design.
- Kill-feed, pause, баланс — через правки SO, не кода (если архитектура сделана правильно — а аудиты по ходу это контролировали).

**Шаг 11 (BaseHub-каркас):**
- Сцена `BaseHub` собирается editor-скриптом `Build Base Hub Layout` по `BuildingCatalogConfig`, не вручную.
- `HubCameraBehaviour` реализует `ICameraBehaviour`; pan/zoom параметры — только из `HubCameraConfig` SO.
- Тап-зоны зданий — Collider + **kinematic Rigidbody** (правило CLAUDE.md).
- `GameState.BaseHub` уже в enum с шага 1 — меняется только сцена (заглушка `_TempHubMenu` → карта), FSM и переходы не трогаем.
- Заглушки трюков (LongJump, FireRings, Bowling, FieldGoal, HighCard) — те же `BuildSiteController` в disabled-режиме через флаг `enabled=false` в конфиге, не отдельный класс.

**Шаг 12 (Стадийная стройка):**
- `BuildProgressService` — pure C#, в `Services/Progression/` (§15.1). Save — **отдельный блок** через `IProfileStorage`, не внутри профиля `MetaProgressService`.
- Все цены этапов — в `BuildingCatalogConfig`; unlock-эффекты декларативно в SO (`BuildingId` → unlock payload), никаких switch по BuildingId в коде сервиса.
- Разблокировка через подписку на `BuildingCompletedEvent` — никаких прямых вызовов из `BuildProgressService` в `GameFlowService`.
- Визуальные стадии — свап дочерних префабов в `BuildSiteController`; префабы стадий из `Assets/Development/Prefabs/Hub/<BuildingId>/`.
- High Jump с этого шага гейтится стройкой башни (до шага 12 был доступен свободно через заглушку — это ок).

**Шаг 13 (Idle-доход):**
- Всё время — через `ITimeProvider` (UTC), созданный на шаге 1. `DateTime.Now` по коду = нарушение для аудита.
- Offline-расчёт: `min(elapsed, offlineCapHours)`, отрицательный elapsed (перевод часов назад) клампить в 0.
- Доход публикуется через `RewardEarnedEvent { source = IdleIncome }` — §15.3 предохранитель в действии, `EconomyService` не переписывается.
- `WelcomeBackPopup` — через push-pop состояний `GameFlowService` (тот же механизм, что Pause), не отдельный флаг/канвас поверх.
- Кнопка «x2 за рекламу» — disabled-заглушка, никакой SDK-интеграции в срезе.
- В этом же шаге ребаланс `RewardConfig` под sink стройки (Open Question #9) — правки только в SO.

---

## Архитектурный аудит — каждые 3-4 шага

После шагов 3, 6, 9 и 12 (или когда чувствуешь, что накопилось — больше двух «странных» решений CLI подряд) запусти явный аудит.

### Промпт аудита

```
Прогон skill audit-architecture по всему текущему коду 
проекта (Assets/Development/Scripts/).

Особое внимание:
- Дубликаты строковых ключей (теги, ключи пулов, animator-параметры).
  Должны быть в enum/const, не в строках по месту.
- MonoBehaviour с бизнес-логикой. Должно быть только Unity API; 
  бизнес-логика в pure C# сервисах.
- Прямые обращения к IPoolManager вне фабрик. Доступ к пулу — 
  только из *Factory классов.
- RegisterInstance там, где должен быть RegisterComponent. 
  MonoBehaviour-зависимости в GameLifetimeScope = RegisterComponent.
- Resources.Load — запрещён.
- Magic numbers в Controllers — должны быть в SO.
- Объекты из пула без InjectGameObject + явного Launch().
- Триггер-зоны без kinematic Rigidbody.
- Прямые DateTime.Now / DateTime.UtcNow вместо ITimeProvider.

Покажи отчёт с конкретными файлами и строками. Не исправляй 
автоматически — я выберу, что фиксить.
```

### Что делать с отчётом

- Тривиальные нарушения (одна-две magic-number или один забытый kinematic Rigidbody) — фиксь точечными промптами «исправь нарушение X в файле Y».
- Системные нарушения (например, бизнес-логика во всех Controllers) — это означает, что Claude **систематически** уходит от архитектуры. Откатываешь к последнему коммиту и **пересматриваешь свой промпт** — что в нём дало Claude свободу так делать.

---

## Обработка ошибок Unity Console

Когда что-то падает или не работает, не пиши «исправь». Пиши так:

```
Получил ошибку в Unity Console:
<paste полный stacktrace, включая InvalidOperationException 
или NullReferenceException и все строки after that>

Используй unity-error-analyzer скил для разбора. Прежде чем 
писать фикс — расскажи мне:
1. Причину одной фразой.
2. Где именно нарушено (файл:строка).
3. План фикса.

Не правь код до моего «ок».
```

**Зачем это:** иначе Claude видит ошибку, пишет «фикс», который не работает (или маскирует симптом), потом ещё один поверх, потом ещё один — через час у тебя 5 слоёв костылей вместо чистого решения.

Частые причины ошибок на этом проекте (для подстраховки — Claude должен искать сначала здесь):

- `NullReferenceException` в `OnEnable`/`Start` MonoBehaviour: `Construct()` ещё не вызван, нужен флаг `_initialized` или перенос логики в `Construct()`. См. CLAUDE.md.
- `OnTriggerEnter` не срабатывает: ни у одного из участников нет Rigidbody. Добавить kinematic Rigidbody на триггер-зону.
- VContainer не инжектит: `RegisterInstance` вместо `RegisterComponent`.
- Объект из пула «не инициализируется»: забыт `_container.InjectGameObject(go)` + явный `Launch(...)` после `pool.Get()`.

---

## Между сессиями — синхронизация контекста

Каждая новая сессия CLI начинается с **синхронизирующего префикса**, потому что у Claude нет памяти между сессиями (если не используются memory-функции, и даже с ними лучше явно).

### Префикс новой сессии

```
Я закончил шаги <X-Y> Implementation Order. Сейчас работаем 
над шагом <Z>. 

Прочитай docs/game-design.md §19 и подтверди, что шаги <X-Y> 
действительно реализованы в коде (быстрый осмотр 
Assets/Development/Scripts/, не полный аудит). Если видишь 
расхождение с моим утверждением о завершённости — скажи сразу.

Затем переходим к промпту шага <Z>.
```

Это нужно, потому что Claude в новой сессии может «уверенно» начать переписывать то, что уже сделано, увидев «незавершённую структуру». Подтверждение состояния — защита от этого.

---

## Что НЕ делать

- **Не писать** «реализуй весь вертикальный срез» одним промптом.
- **Не писать** «сделай как в FlatOut 2» — Claude её не играл.
- **Не писать** «используй свои best practices» — в CLAUDE.md уже всё прописано.
- **Не позволять** CLI самостоятельно переходить к следующему шагу. Каждый шаг = твой явный «ок».
- **Не пропускать** проверку плана файлов перед написанием кода.
- **Не пропускать** проверку в Unity между шагами — иначе ошибка из шага N всплывёт в шаге N+3 и будет в пять раз сложнее найти.
- **Не пропускать** архитектурные аудиты — долг накапливается тихо.
- **Не игнорировать** EDITOR_SCRIPT_NEEDED пометки — Claude их пишет не для красоты, и без editor-скриптов рутина в Unity растягивается на часы.

---

## Чек-лист перед запуском CLI впервые

- [ ] Проект Unity 6 (6000.3.9f1 LTS) открыт, URP включён, Active Input Handling = Both.
- [ ] VContainer установлен через Package Manager.
- [ ] TextMeshPro Essentials импортирован.
- [ ] ProBuilder установлен.
- [ ] `Assets/ThirdParty/` содержит купленные ассеты (1Poly Cars, Battle Stadium и т.д.).
- [ ] `CLAUDE.md` и `docs/game-design.md` присутствуют в корне.
- [ ] `.claude/rules/editor-files.md` присутствует (упомянут в CLAUDE.md).
- [ ] Скилы `code-writer`, `code-reviewer`, `editor-script-creator`, `audit-architecture`, `unity-error-analyzer`, `setup-ragdoll`, `add-vfx-effect`, `add-editor-script`, `update-claude-md` зарегистрированы в Claude Code.
- [ ] Папка `docs/` существует, в ней `game-design.md` и этот файл `cli-workflow.md`.
- [ ] Создан git-репозиторий, чтобы можно было коммитить после каждого шага.

После прохождения чек-листа — открывай CLI и начинай с **Сессии 0**.
