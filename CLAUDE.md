# Flatuot2_Like — правила проекта

Геймдизайн и сущности — в `docs/game-design.md`. Регламент работы по шагам — `docs/cli-workflow.md`. Текущее состояние (шаг, DoD, отклонения, итоги ревью) — `docs/progress.md`, подгружается в контекст хуком `SessionStart`.

## Технологии

- Unity 6 (6000.3.9f1 LTS), URP
- Cinemachine 3.1 — все игровые камеры (vcam-адаптеры реализуют `ICameraBehaviour`, переключение через Priority; при сомнении в API смотреть исходник в `Library/PackageCache/com.unity.cinemachine@*/Runtime/`, не гадать по CM2)
- VContainer (DI-контейнер)
- C# с SOLID и Clean Architecture
- Active Input Handling: **Both** (Project Settings → Player)
- DOTween для оптимизации анимаций и сложных математических вычислений, и приведению GUI к стандартам гибрид-кэжа

## MCP Unity (мост к редактору)

В проекте подключён MCP-сервер `mcp-unity` (пакет `com.gamelovers.mcp-unity`, конфиг `.mcp.json`, настройки `ProjectSettings/McpUnitySettings.json`; установка/грабли — глобальное правило `~/.claude/rules/mcp-unity.md`). Когда Unity открыт, Claude обязан пользоваться мостом вместо ручных просьб:

- **Прогон editor-меню** — `execute_menu_item` с нашими `FlatOutLike/...`-пунктами (не просить пользователя кликать, если шаг не требует его решений).
- **Диагностика «не работает»** — сначала `get_console_logs` (стектрейсы без копипаста) и инспекция иерархии/проводки сцены, потом гипотезы.
- **Проверка после editor-скриптов** — иерархия/компоненты/сериализованные поля читаются мостом.
- **Тесты** — `run_tests`.

За пользователем остаются: плейтест (ощущения, баланс, визуал), Unity-checkpoint «работает/не работает», решения по дизайну.

## Архитектурные паттерны

- **DI**: VContainer, Composition Root в `GameLifetimeScope.cs`
- **EventBus**: типизированная шина `Core/Events/EventBus.cs` (struct-события)
- **Command**: `ICommand` с `CanExecute()` / `Execute()`
- **State Machine**: generic FSM для GameState (Movement / Shooting / EnemyDown)
- **Object Pool**: `IPoolManager` для пуль, VFX, ресурсов
- **MVP** для UI: Model ← Presenter → View
- **Factory**: фабрики инкапсулируют пулы; внешний код не работает с `IPoolManager` напрямую
- **ScriptableObject Data-Driven**: вся конфигурация в SO

## Структура папок

```
Assets/
├── ThirdParty/                     # Купленные/импортированные сторонние ассеты
│   ├── RealFireAndSmoke/           # VFX-пак
│   └── ...                         # Один паблишер/пак = одна папка
│
└── Development/                    # Весь наш собственный контент и код
    ├── Scripts/
    │   ├── Core/                   # Переиспользуемый фреймворк
    │   │   ├── Commands/           # ICommand, игровые команды
    │   │   ├── DI/                 # ServiceLocator (fallback)
    │   │   ├── Events/             # EventBus + game events (struct'ы)
    │   │   ├── Input/              # IInputProvider, InputRouter, Keyboard/Mobile providers
    │   │   ├── Pool/               # GameObjectPool, PoolManager
    │   │   ├── StateMachine/       # Generic StateMachine<TEnum>
    │   │   └── Time/               # ITimeProvider, SystemTimeProvider — единственное место с DateTime
    │   ├── Data/Configs/           # Определения ScriptableObject (один файл = один класс)
    │   ├── Models/                 # Чистые данные, enums (GameState, ResourceType, VfxEffectType)
    │   ├── Services/               # Бизнес-логика (pure C#, без MonoBehaviour)
    │   │   └── Progression/        # Mid-core системы (миссии, XP, energy, см. game-design §15)
    │   ├── Controllers/            # MonoBehaviour: Unity API only (input, physics, render)
    │   ├── Views/UI/               # MVP-View'ы (только UI; никаких VFX-Controller'ов здесь)
    │   ├── Factories/              # Фабрики; единственное место доступа к IPoolManager
    │   ├── Installers/             # GameLifetimeScope (VContainer Composition Root)
    │   ├── Editor/                 # Editor-скрипты (см. skill `add-editor-script`)
    │   └── GameBootstrap.cs        # Точка входа
    │
    ├── Prefabs/                    # Наши prefab'ы с логикой (см. правила ниже)
    │   ├── Vehicles/               # Префабы машин с прицеплёнными контроллерами
    │   ├── Characters/             # Префабы водителя (Mixamo + ragdoll + Animator)
    │   ├── Pickups/                # RepairPickup, NitroPickup и т.п.
    │   ├── Environment/            # Композитные модули окружения (BarrelCluster и т.п.)
    │   ├── VFX/                    # Наши настроенные VFX-префабы (поверх ThirdParty-эмиттеров)
    │   ├── UI/                     # Canvas'ы и UI-префабы
    │   └── Systems/                # GameLifetimeScope, EventBus root и др. service-префабы сцены
    │
    ├── Data/Configs/               # Инстансы ScriptableObject (.asset)
    │   ├── Car/
    │   ├── Driver/
    │   ├── Enemies/
    │   └── ...
    │
    ├── Scenes/                     # Игровые сцены
    │   ├── Boot.unity
    │   ├── MainMenu.unity
    │   ├── Derby_Arena_01.unity
    │   ├── Stunts_HighJump.unity
    │   └── _TestSandbox.unity      # Временные сцены, имя с подчёркиванием
    │
    └── Art/                        # Наши доработанные арт-ассеты
        ├── Materials/              # Материалы (включая URP-конверсии)
        ├── Textures/               # Наши текстуры, decals, иконки
        ├── Models/                   # Доработанные ассеты (если есть)
        └── ...
```

## Правила хранения контента

### Купленные ассеты — только в Assets/ThirdParty/

Любой импортированный пак с Asset Store / itch.io / Polyhaven кладётся в **отдельную папку под `Assets/ThirdParty/<PackName>/`**. Содержимое этой папки **не редактируется**: ни материалы, ни префабы, ни скрипты внутри пакета не меняются.

Зачем:
- Обновление пакета через Package Manager не сносит твою работу.
- Чёткая граница «что чужое, что наше» для лицензий, аудитов, git diff.
- Claude Code CLI получает команду «не читать ThirdParty» — это работает только при изоляции.

Если нужна доработка контента из пака — копируешь конкретный файл (меш, материал, текстуру) в `Assets/Development/Art/` или `Assets/Development/Prefabs/` и правишь копию.

### Префабы с нашей логикой — в Assets/Development/Prefabs/<Category>/

Любой префаб, к которому привязаны наши MonoBehaviour'ы (контроллеры, view'ы, фабрики, конфиги), хранится в `Assets/Development/Prefabs/<Category>/`. Категории — по назначению, не по типу скрипта:
- `Vehicles/` — машины (наши префабы поверх мешей из ThirdParty).
- `Characters/` — водитель.
- `Pickups/` — repair/nitro и будущие pickup'ы.
- `Environment/` — композитные модули окружения.
- `VFX/` — наши настроенные эффекты (даже если эмиттер из ThirdParty).
- `UI/` — Canvas'ы.
- `Systems/` — служебные префабы сцены (GameLifetimeScope и т.п.).

Префаб **никогда** не лежит:
- В `Assets/ThirdParty/...` — нарушает изоляцию.
- В `Assets/Development/Scripts/...` — скрипты и префабы разные слои.
- В корне `Assets/Development/` или `Assets/` — нет категории.

### Связь Prefab → ThirdParty через ссылки, не копированием

Префаб машины в `Development/Prefabs/Vehicles/Car_Sedan.prefab` **ссылается** на меш `Assets/ThirdParty/1Poly_DestructibleCars/Meshes/Sedan.fbx` через `MeshFilter.mesh`. Сам меш не дублируется. Это позволяет обновить пак без потери настройки префаба (Unity сохраняет ссылку по GUID).

### Editor-скрипты автоматизируют сборку

Сборка префабов из сырого ThirdParty-контента — рутина, и она автоматизируется. См. editor-скрипт `Setup Car Damage Visual` в `game-design.md` §17: выделил FBX из ThirdParty → editor-скрипт создал prefab в `Development/Prefabs/Vehicles/` с правильной структурой, контроллерами и ссылками на SO-конфиги.

## Правила комментирования
Оставлять комментарии к коду на русском языке

## Правила кодирования

- Один MonoBehaviour / ScriptableObject = один файл, имя файла = имя класса.
- Зависимости — через конструктор (pure C#) или `[Inject] Construct(...)` (MonoBehaviour).
- MonoBehaviour содержит **только** Unity API (ввод, физика, рендер). Бизнес-логика — в сервисах.
- Запуск поведения объекта вызывается явно (`Launch(direction)`), а не из `Start` / `OnEnable`.
- Сервисы — pure C#, тестируемые без Unity.
- События — `readonly struct`, публикуются через `IEventBus`.
- Именование: `PascalCase` для публичных, `_camelCase` для приватных полей.
- Каждый сервис имеет интерфейс (`IShootingService`, `IRagdollService`).
- Команды реализуют `ICommand` с `CanExecute()` / `Execute()`.
- Доступ к пулу инкапсулируется в фабрике; никакой внешний код не вызывает `IPoolManager` напрямую.
- Views — только для UI.
- Префабы лежат в `Assets/Development/Prefabs/<Category>/`. Сторонние ассеты — в `Assets/ThirdParty/<PackName>/`, без редактирования.
- По возможности покрывать бизнес-логику Unit-тестами.

## Известные нюансы Unity 6

- `OnEnable()` вызывается **до** VContainer `Construct()` — всегда проверять null перед подпиской и/или использовать флаг `_initialized`.
- `Input.GetAxisRaw()` / `GetKeyDown()` не работают если Input Handling = "New" — у нас стоит **Both**.
- **`GetKeyDown`, потребляемый из `FixedUpdate`, сгорает** (за render-кадр может пройти 0 физтиков) — буферизовать флагом в `Update`, сбрасывать после потребления; детали — `.claude/rules/physics-input.md` (auto-loaded для Controllers/Core/Input).
- Инициализацию переносить в `Construct()`, а не в `Start()`.
- `NavMeshAgent` требует baked NavMesh — автоматизировать через Editor-скрипт.
- Mixamo-модели могут ломать Ragdoll Setup Wizard — см. skill `setup-ragdoll`.
- **`??` / `?.` запрещены с Unity-объектами** (`GetComponent<T>() ?? AddComponent<T>()` — бага): fake-null Unity-объекта обходит перегруженный `==`, в Editor-контексте `GetComponent` при промахе возвращает такой fake-null → `MissingComponentException` при первом обращении. Всегда явный `if (x == null)`.
- **Ragdoll + SkinnedMeshRenderer = исчезающий меш**: SMR не пересчитывает bounds по вершинам — когда ragdoll уводит кости от запечённого AABB, фрустум-каллинг вырезает меш в зависимости от ракурса (тело пропадает, остаются мелкие рендереры на костях). При активации ragdoll ставить `smr.updateWhenOffscreen = true` (см. `DriverController.Eject`).
- **Ragdoll модульного аватара = «иглы» на одежде при дальнем полёте**: служебные кости скелета под контейнером `Root` (не под физкостями) после детача остаются в точке активации ragdoll — вершины с их весами растягиваются от тела до места старта, тем длиннее, чем дальше улетел. Фикс: при активации ragdoll перепарентовать таз под корень персонажа, а контейнер скелета — под таз (`worldPositionStays: true`, см. `DriverController.Eject`).

## Известные нюансы Unity Physics

### Триггеры требуют Rigidbody (КРИТИЧНО)

`OnTriggerEnter` / `OnTriggerExit` **не срабатывают** если ни на одном из участников нет Rigidbody. Типичная ситуация: триггер-зона `BoxCollider(isTrigger=true)` + персонаж с `NavMeshAgent` + `CapsuleCollider` — Rigidbody нет ни у кого, событие молчит.

Решение — кинематический Rigidbody на триггер-зоне:
```csharp
var rb = triggerZone.AddComponent<Rigidbody>();
rb.isKinematic = true;
rb.useGravity  = false;
```

## Известные нюансы VContainer

### `RegisterInstance` vs `RegisterComponent` (КРИТИЧНО)

`RegisterInstance` регистрирует объект как источник зависимости, но **не вызывает** `[Inject] Construct()` на самом объекте. Для MonoBehaviour, которому нужна инъекция, использовать `RegisterComponent`. Симптом: `_initialized` остаётся `false`, ничего не работает.

```csharp
// НЕВЕРНО — Construct() не будет вызван:
builder.RegisterInstance(_playerController);
// ВЕРНО:
builder.RegisterComponent(_playerController);
// Через интерфейс:
builder.RegisterComponent<IHUDView>(_hudView);
```

`RegisterInstance` подходит только для plain C# объектов и ScriptableObject, которым инъекция не нужна (конфиги, данные).

### Объекты из пула не получают инъекцию автоматически

После `pool.Get()` нужно вручную:
```csharp
_container.InjectGameObject(gameObject);  // using VContainer.Unity
```
И затем явно вызвать `Launch(...)` — `OnEnable` уже отработал до инъекции.

## Запрещённые практики

### Дублирование строковых ключей

Любой идентификатор (имя тега, animator-параметра, ключ пула, имя сцены) определяется **один раз** — в enum или в единственном SO. Все остальные места ссылаются на это определение.

- VFX: `VfxEffectType` enum + `VfxConfig` — единственный источник.
- Animator: `static readonly int FooHash = Animator.StringToHash("Foo");`.
- Теги: `static class Tags { public const string Enemy = "Enemy"; }`.
- Сцены: enum или константы в одном файле.

Поиск нарушений автоматизирован: в момент записи файла — хук `arch-guard` (см. «Состояние и хуки»), по всему коду — skill `audit-architecture`.

### `Resources.Load` запрещён

Файлы в `Resources/` не исключаются из сборки, ссылки не проверяются компилятором. Использовать `[SerializeField]` (через `GameLifetimeScope` или конфиг) или Addressables.

## Workflow (orchestrator)

Ты оркестратор. Полный регламент шага — «Цикл шага» в `docs/cli-workflow.md`; ниже — порядок, который действует в каждой сессии. Когда пользователь просит написать код:

1. **Планирование** — пройти чек-лист ниже; показать план файлов и модель `code-writer` на шаг (Sonnet / Opus по правилу эскалации, с одной строкой причины). Ждать «ок» пользователя.
2. **Фиксация** — после «ок» записать в `docs/progress.md` статус `in-progress` и Definition of Done шага (строка «DoD» шага в §19 `game-design.md` + тех-критерии §16).
3. **Реализация** — делегировать `code-writer` на утверждённой модели. Если в его ответе есть строка `EDITOR_SCRIPT_NEEDED: ...` — параллельно запустить `editor-script-creator` (контракт имён — пункт 4 чек-листа). Замечания хука `arch-guard` исправлять сразу, до ревью.
4. **Компиляция** — через MCP `get_console_logs` убедиться, что ошибок компиляции нет. Некомпилирующийся код на ревью не отправляется.
5. **Ревью** — `code-reviewer` в **чистом контексте**: передать только номер и название шага, разделы `game-design.md` и ссылку на DoD в `progress.md`. Не передавать свои рассуждения, обоснования решений и пересказ изменений — ревьюер читает diff сам.
6. **Правки** — по Critical делегировать исправления `code-writer` (архитектурные Critical — на Opus), затем **новый** вызов `code-reviewer`, не продолжение прежнего. Максимум 2 раунда; второй FAIL — стоп: записать ситуацию в `progress.md` и вынести решение пользователю.
7. **Завершение** — при PASS: `progress.md` → `awaiting-unity-check`, отчёт пользователю (что сделано, что проверить в Unity, какие пункты DoD проверяемы только плейтестом). Статус `done` — только после «ок» пользователя по Unity-checkpoint.

### Агенты и модели

Модели — алиасами `opus` / `sonnet` (последняя версия семейства). Заморозка версии — только через `env` в `.claude/settings.json` (см. `cli-workflow.md` → «Роли моделей и effort»); `claude update` — только между шагами.

| Агент | Модель | Отвечает за |
|---|---|---|
| `code-writer` | `sonnet`; эскалация — `opus` через параметр `model` при вызове | новый C#: интерфейс + сервис + регистрация |
| `editor-script-creator` | `sonnet` | Editor-скрипты для автоматизации Unity Editor |
| `unity-error-analyzer` | `opus` | разбор stacktrace из Unity Console |
| `code-reviewer` | `opus`, без Edit/Write | ревью git diff на SOLID, Clean Architecture, правила CLAUDE.md, DoD шага — по «Контракту ревью» в своём описании |

**Эскалация `code-writer` на Opus:**
- **весь шаг**, если он затрагивает регистрации VContainer, FSM и переходы `GameFlowService`, физику / триггеры / ragdoll / буферизацию ввода, save/load или время — эти ошибки не видны ни компилятору, ни `arch-guard` и проявляются только в Play Mode;
- **только правка**, если ревью вернуло FAIL с Critical архитектурного характера (границы слоёв, DI, FSM, пул).

### Состояние и хуки

- `docs/progress.md` — единственное хранилище состояния шага между сессиями и после компакции. Обновлять в моменты из `cli-workflow.md` («Когда обновляется»). Любое отступление от `game-design.md` — сначала строка в «Отклонения», потом код.
- Хук `arch-guard` (после каждой записи `.cs` в `Assets/Development/`) при нарушении механических правил этого файла возвращает список `файл:строка` — исправить до ревью. Ложное срабатывание — указать в отчёте шага и в `progress.md`; не обходить (не переписывать код ради обхода шаблона, не отключать хуки).
- Хук `progress-guard` не даёт завершить ход, пока `progress.md` старше изменённого кода: сначала обновить `progress.md`.

### Чек-лист этапа планирования (пройти ДО делегирования)

Каждый пункт — накопленные грабли проекта; проверять на этапе плана, а не после ошибки компиляции:

1. **Память**: прочитать step-файлы из MEMORY.md по теме шага — уроки там, а не в коде; сверить с разделами «Отклонения» и «Открытые вопросы» в `docs/progress.md`.
2. **Зависимость на код ThirdParty-пака**: проверить наличие asmdef у пака. Нет asmdef → его классы в Assembly-CSharp и из asmdef-сборок проекта НЕДОСТУПНЫ. Варианты: (а) своя реализация (прецедент — VirtualJoystickView вместо Joystick Pack); (б) мост в `Assets/Development/BridgeScripts/` (+`BridgeScripts/Editor/`) — папки БЕЗ asmdef компилируются в Assembly-CSharp(-Editor) и видят и классы пака, и публичное API FlatOutLike (autoReferenced) — прецедент CarEngineSoundBridge/RealisticEngineSound. Мост — тонкая проводка без бизнес-логики и без DI.
3. **Новые классы в под-неймспейсах**: точные `using` для под-неймспейсов в промптах агентам (C# не импортирует детей через родительский); в `Controllers.*`/`Views.*` голые имена Unity-типов могут схлопнуться с соседним под-неймспейсом (например `Camera` → под-неймспейс `<Root>.Controllers.Camera`, CS0118) — квалифицировать `UnityEngine.*`.
4. **Параллельные агенты** (code-writer ∥ editor-script-creator): контракт имён — классы, SerializeField-поля, пункты меню, пути префабов — зафиксировать в ОБОИХ промптах заранее, дословно.
5. **Новые SerializeField на scope**: поле может быть не прошито в части сцен → SO-конфиг регистрировать с фоллбэк-инстансом (`RegisterInstance(null)` = NRE), MonoBehaviour-view — условный `RegisterComponent` по `!= null`.
6. **Расширение интерфейса или ctor**: перечислить в промпте ВСЕ тестовые фейки и call-sites (иначе CS0535/CS7036).
7. **UI-иерархия**: оверлей (панель результата/смерти/паузы) не должен быть ПОТОМКОМ узла, который гасится кодом (`SetActive(false)` родителя выключает всё поддерево — `Show()` ребёнка молча не работает). Выносить оверлеи сиблингами гасимого контейнера в том же канвасе; новые канвасы не плодить (отдельный канвас — только по слою поверх других фич или для изоляции часто обновляемой геометрии). Pointer-held элементы (стики, кнопки-удержания) сбрасывают состояние в `OnDisable`.
   Любая тапабельная кнопка (включая кастомные pointer-view поверх `IPointerDown/Up`) собирается ВМЕСТЕ с компонентом `Button` — тап должен визуально считываться. Настройка: `Transition = Color Tint` с прошитым `targetGraphic` (без него tint молча не работает), `selectedColor = normalColor` (иначе после тапа кнопка залипает в selected-цвете), `Navigation = None` на тач-HUD. `Button` и кастомный pointer-view сосуществуют на одном GameObject без конфликтов.
   Канвас, повторяющийся в нескольких сценах, существует ОДНИМ префабом в `Prefabs/UI/`, а не пер-сценными копиями: editor-скрипт собирает его один раз и сохраняет через `PrefabUtility.SaveAsPrefabAssetAndConnect`, сцены держат connected-инстансы (образец — MobileInputCanvas). Дубли-копии расходятся при первой же правке, и каждый sweep/стилизация правит N мест вместо одного. Пер-сценное допустимо только в связях: ссылки инстанса на сценные объекты (scope, презентеры) прошиваются на инстансе как overrides.
   UI, собираемый editor-скриптами, выключает дефолтные флаги Graphic там, где они не нужны (Unity включает оба по умолчанию, а нужны они в меньшинстве случаев): `raycastTarget = false` на всём неинтерактивном — тексты, иконки, бары, декоративные Image (каждый включённый флаг удорожает graphic-raycast каждого тапа/клика; `true` — только на элементах, ловящих ввод: кнопки, стики, фоны-блокеры); `maskable = false`, если элемент не должен обрезаться родительской маской (`Mask`/`RectMask2D`) — флаг тянет stencil/материальный оверхед. Относится и к `Image`, и к `TextMeshProUGUI`.
8. **Editor-скрипт правит существующую сцену/канвас** → миграционная идемпотентность: дособрать недостающее в собранное старой версией, без дублей.
9. **Одноразовый ввод, потребляемый физикой** → буферизация Update→FixedUpdate (`.claude/rules/physics-input.md`).
10. **Глобальный static-стейт** (`Time.timeScale` и т.п.): переживает загрузку сцены при per-scene scope — планировать страховки сброса.
11. **Модель `code-writer`**: определить по правилу эскалации («Агенты и модели») и указать в плане с причиной. Сомнение — Opus.

## Editor-автоматизация (обязательно)

Любая рутина в Unity Editor автоматизируется через `[MenuItem(...)]` скрипт под `FlatOutLike/<Category>/<Action>`. Никогда не писать "сделайте это руками в Inspector".

- Категории и менюшная иерархия — skill `add-editor-script`.
- Низкоуровневые нюансы (Undo, идемпотентность, теги, заполнение SerializeField на Composition Root) — `.claude/rules/editor-files.md` (auto-loaded для `**/Editor/**`).

## Расширение проекта — куда смотреть

| Задача | Куда |
|---|---|
| Новый VFX-эффект | skill `add-vfx-effect` |
| Новый тип ресурса | skill `add-resource-type` |
| Ragdoll-настройка | skill `setup-ragdoll` |
| Новый Editor-скрипт | skill `add-editor-script` + `.claude/rules/editor-files.md` |
| Формальный аудит кода | skill `audit-architecture` |
| Обновление CLAUDE.md / MEMORY.md | skill `update-claude-md` |
| Новое механическое правило (ловится поиском по строке) | пункт в этот файл + строка в `$rules` хука `.claude/hooks/arch-guard.ps1` |
| Новый сервис | интерфейс + реализация + регистрация в `GameLifetimeScope` |
| Новое игровое состояние | значение в `GameState` enum + реализация `IState` + регистрация в `GameFlowService` |
| Новая команда | класс `: ICommand`, вызывать из контроллера |
