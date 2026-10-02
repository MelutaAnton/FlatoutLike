# Progress — FlatOutLike

## Текущее состояние
- Текущий шаг: 10 «Polish кора» — статус: in-progress (10a Pause / 10b Kill-feed / 10c Death-UX — done, playtested; осталось 10d баланс + финальный прогон §16 по кору)
- Последний проверенный в Unity шаг: 9 (коммит 1c066307); пост-полиш и аудио до 9f0b2b5b проверены плейтестом
- Следующее действие: 10d — playtest баланса (OQ#2 пороги стейджей, OQ#5 цены) → правки SO; затем чек-лист §16 по кору. Решить, объединять ли 10d с ребалансом шага 13 (план в памяти предлагает объединить)

## Definition of Done текущего шага
Функциональная часть (§19 шаг 10):
- [x] §16 Derby — поведение при смерти игрока: спектатор, Wrecked → Watch / Leave (OQ#6)
- [x] §16 «Поток состояний и UI» — Pause во всех геймплейных состояниях, корректный resume
- [x] HUD релевантен фазе (Derby HUD, HighJump HUD по фазам Driving/Flight/Scoring)
- [x] Kill-feed работает
- [x] OQ#1 (Rigidbody-only AI), OQ#4 (High Jump = только высота), OQ#6, OQ#7 (стратегия A) отражены
- [ ] OQ#2 — пороги damage-стейджей подобраны плейтестом и отражены в SO
- [ ] OQ#3 — число уровней апгрейдов подтверждено в `UpgradeCatalogConfig`
- [ ] OQ#5 — цены машин/апгрейдов, награды подобраны плейтестом и отражены в SO
- [ ] Финальный прогон §16 Derby / Stunts / Garage / Платформы по пунктам

Тех-критерии §16 (входят в DoD каждого шага):
- [x] Нет `Resources.Load` (скан 2026-10-02: 0)
- [x] Идентификаторы в enum/const (скан 2026-10-02: строковых тегов/animator/сцен в runtime 0)
- [ ] `Instantiate` / `IPoolManager` только в `Factories/` и `Core/Pool/` — нарушение `CarDamageVisualController.cs:219` (декали), см. Отклонения
- [ ] Время только через `ITimeProvider` — провайдера нет (DateTime по коду тоже нет), см. Отклонения
- [ ] Все параметры в SO, нет magic numbers в Controllers — не проверялось аудитом после шага 10
- [x] Все триггер-зоны с kinematic Rigidbody (EjectTriggerZone, пикапы — через editor-скрипты)
- [x] Объекты из пула: `InjectGameObject` + явный `Launch(...)`
- [x] MonoBehaviour в `GameLifetimeScope` — `RegisterComponent` (аудит 2026-07-12)

## Отклонения от game-design.md
- Шаг 1/8: нет `GameState.BaseHub` и `_TempHubMenu`; вместо них `GameState.MainMenu`/`StuntsHub`, сцены `MainMenu`, `Stunts_Hub`, `Garage`, их View/Presenter (Stunts Hub UI сделан вопреки cli-workflow). Миграция на BaseHub с удалением — шаг 11 — одобрено пользователем: да
- Шаг 1: нет `Core/Time/ITimeProvider` + `SystemTimeProvider` (§15.3, DoD шага 1). Предложение: создать на шаге 13 — одобрено пользователем: нет (вынести решение)
- Шаг 3: декали damage-стейджа спавнятся `Instantiate` в `CarDamageVisualController.cs:219` мимо фабрики — одобрено пользователем: нет (вынести решение: фабрика или исключение)
- Шаг 7: `LandingZone` удалён, приземление — по успокоению скорости таза (`DriverFlightService.Track`) — одобрено пользователем: да
- Шаг 7/8: High Jump — результат и рекорд только по высоте; множитель за флипы и композитный score выпилены (`StuntsScoredEvent.Height`, нет `FlipScoreMultiplier`/`BaseFlightSpeed` в `DriverFlightConfig`); расходится с §4, §7, §8, §16 Stunts — одобрено пользователем: да
- Шаг 8: per-scene scope без DontDestroyOnLoad; смена экрана = загрузка сцены через `SceneLoaderService` — одобрено пользователем: да
- Шаг 8: прогресс апгрейдов per-car (§8 «или global», §14.3) — одобрено пользователем: да
- Шаг 8d: нитро стартует полным — одобрено пользователем: да
- Полиш: камеры на Cinemachine 3 (`Cinemachine*Behaviour` вместо имён §11) — одобрено пользователем: да
- Полиш: звук (`AudioService`, мост RealisticEngineSound в `BridgeScripts/`) сделан, хотя по §1/§18 вне среза — одобрено пользователем: да
- Шаги 5–10: сервисы сверх §12 — `PauseService`, `SceneLoaderService`, `HighJumpSessionService`, `CarRegistry`, `RewardCrediterService`, `CameraShakeService`, `VfxService`, `DamageStageResolver`, `AIInputProvider` — одобрено пользователем: да
- Шаг 6: ручной eject по E в `KeyboardInputProvider` (§3 допускает, §4 запрещает) — одобрено пользователем: нет (вынести решение)

## Открытые вопросы
- `arch-guard` даст ложное срабатывание на `Data/Configs/Car/CarStatsConfig.cs:77` (`Instantiate(this)` — клон SO) — к кому: пользователь (добавить `Allow` в правило или переписать клон)
- `RootNamespace: Flatuot2_Like` в Project Variables всех 4 агентов и примеры `Flatuot2_Like/...` в `.claude/rules/editor-files.md` — фактически `FlatOutLike` (asmdef, весь код) — к кому: пользователь (исправить отдельной командой)
- Папки `.claude/agent-memory/` внутри `Assets/Development/**` (6 мест) — память агентов размазана — к кому: пользователь
- Противоречия внутри ГДД: §13 издатель `RewardEarnedEvent` = `EconomyService` (в коде — `DerbyMatchService`/`HighJumpScoreService`); `_TempHubMenu` в §9/§16/§19; имена editor-скриптов §17 (`Build HighJump Ramp`, `Setup Driver Ragdoll`, `Create Empty SO Set`; нет `Validate Composition Root`) — к кому: пользователь
- `IProfileStorage` типизирован под `IPlayerProfile` — для отдельного save-блока стройки нужно обобщение — к кому: шаг 12
- Долг 8c: мёртвое поле `StuntsCardEntry._sceneKey` — к кому: шаг 11

## Ревью текущего шага
- 10a–10c: ревью пройдено до введения пайплайна (детали — память проекта step10-polish-status)

## Журнал (одна строка на шаг)
- Шаг 1 — done — 2026-05-22 — f08a2393 — каркас: scope, bootstrap, EventBus, FSM, input, enum'ы (без ITimeProvider)
- Шаг 2 — done — 2026-06-25 — 7793a988 — машина + CarMovementService + chase-камера
- Шаг 3 — done — 2026-06-26 — b487ef38 — DamageService, damage-шейдер, стратегия A, detachable parts
- Шаг 4 — done — 2026-06-28 — 9f52e295 — EnemyAIService + Aggressor, Rigidbody-only
- Шаг 5 — done — 2026-07-02 — 4237ff54 — Derby: спавн N AI, 3 профиля, нитро/пикапы, hazards, win/lose
- Шаг 6 — done — 2026-07-03 — 3ef5610b — eject + ragdoll + orbit-камера + стекло
- Шаг 7 — done — 2026-07-04 — 1b35903e — High Jump: рамп, eject-зона, полёт, буст, рекорд по высоте
- Шаг 8 — done — 2026-07-11 — df750d2f — экономика, сцены+FSM, UI меты, per-car прокачка, HUD
- Шаг 9 — done — 2026-07-11 — 1c066307 — MobileInputProvider + MobileInputCanvas
- Сессия 0.5 — 2026-10-02 — хуки, settings.json, progress.md; модели — Opus 5.5 (оркестратор), Sonnet — алиас `sonnet` (версию сверить в /status)
