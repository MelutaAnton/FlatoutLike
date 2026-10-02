# Progress — FlatOutLike

## Текущее состояние
- Текущий шаг: 10 «Polish кора» — in-progress (10a Pause / 10b Kill-feed / 10c Death-UX — done, playtested; осталось 10d баланс + прогон §16 по кору)
- Последний проверенный в Unity шаг: 9 (1c066307); пост-полиш, аудио и фиксы по аудиту 2026-10-03 проверены плейтестом
- Следующее действие: 10d — playtest баланса (OQ#2, OQ#5) → правки SO; заодно пороги AI/движения/разлёта из констант → SO; решить, объединять ли 10d с ребалансом шага 13
- Отложено решением пользователя: спавн декалей мимо фабрики (`CarDamageVisualController.cs:219`) — логика декалей, возможно, будет удалена

## Definition of Done текущего шага
Функциональная часть (§19 шаг 10):
- [x] §16 Derby — смерть игрока: спектатор, Wrecked → Watch / Leave (OQ#6)
- [x] §16 «Поток состояний и UI» — Pause во всех геймплейных состояниях, корректный resume
- [x] HUD релевантен фазе; kill-feed работает
- [x] OQ#1, OQ#4 (High Jump = высота), OQ#6, OQ#7 отражены
- [ ] OQ#2 — пороги damage-стейджей по плейтесту в SO
- [ ] OQ#3 — число уровней апгрейдов подтверждено в `UpgradeCatalogConfig`
- [ ] OQ#5 — цены машин/апгрейдов, награды по плейтесту в SO
- [ ] Финальный прогон §16 Derby / Stunts / Garage / Платформы

Тех-критерии §16 (входят в DoD каждого шага):
- [x] Нет `Resources.Load`; идентификаторы в enum/const (аудит 2026-10-03)
- [ ] `Instantiate` / `IPoolManager` только в `Factories/`, `Core/Pool/` — `CarDamageVisualController.cs:219` (отложено)
- [ ] Время только через `ITimeProvider` — провайдера нет (DateTime по коду тоже нет)
- [x] Нет magic numbers в Controllers — константы (фиксы по аудиту); геймплейные пороги AI/движения/разлёта — кандидаты в SO на 10d
- [x] Триггер-зоны с kinematic Rigidbody (проверено по YAML 2026-10-03)
- [x] Пул: `InjectGameObject` + явный `Launch`/`Activate`; MB в scope — `RegisterComponent`

## Отклонения от game-design.md
- Шаг 1/8: нет `GameState.BaseHub` и `_TempHubMenu`; вместо них `MainMenu`/`StuntsHub`, сцены `MainMenu`, `Stunts_Hub`, `Garage` с View/Presenter (Stunts Hub UI вопреки cli-workflow). Миграция на BaseHub — шаг 11 — одобрено: да
- Шаг 1: нет `Core/Time/ITimeProvider` + `SystemTimeProvider` (§15.3). Предложение: создать на шаге 13 — одобрено: нет (вынести решение)
- Шаг 3: декали спавнятся `Instantiate` мимо фабрики — одобрено: нет (отложено пользователем, логика может быть удалена)
- Шаг 3: стратегия A (material swap) заменена шейдером `_DamageAmount`; поля `materials`/`damageMaterial` удаляются — одобрено: да (2026-10-03)
- Шаг 5: `CarFactory` спавнит машины `Object.Instantiate`, без пула (§12: «после `pool.Get()`») — одобрено: нет (вынести решение)
- Шаг 6: ручной eject по E в `KeyboardInputProvider` (§3 допускает, §4 запрещает) — одобрено: нет (вынести решение)
- Шаг 7: `LandingZone` удалён, приземление по успокоению скорости таза — одобрено: да
- Шаг 7/8: High Jump — результат и рекорд только по высоте; множитель за флипы и score выпилены (`StuntsScoredEvent.Height`); расходится с §4, §7, §8, §16 — одобрено: да
- Шаг 8: per-scene scope без DontDestroyOnLoad; экран = загрузка сцены через `SceneLoaderService` — одобрено: да
- Шаг 8: апгрейды per-car (§8, §14.3); 8d: нитро стартует полным — одобрено: да
- Полиш: камеры на Cinemachine 3 (`Cinemachine*Behaviour`); звук (`AudioService`, мост RES в `BridgeScripts/`) вне среза §1/§18 — одобрено: да
- Шаги 5–10: сервисы сверх §12 — `PauseService`, `SceneLoaderService`, `HighJumpSessionService`, `CarRegistry`, `RewardCrediterService`, `CameraShakeService`, `VfxService`, `DamageStageResolver`, `AIInputProvider` — одобрено: да

## Открытые вопросы
- `arch-guard` даст ложное срабатывание на `CarStatsConfig.cs:77` (`Instantiate(this)` — клон SO) — пользователь: `Allow` или переписать клон
- `RootNamespace: Flatuot2_Like` в Project Variables 4 агентов и примеры в `.claude/rules/editor-files.md` — фактически `FlatOutLike` — пользователь
- Папки `.claude/agent-memory/` внутри `Assets/Development/**` (6 мест) — пользователь
- Противоречия в ГДД: §13 издатель `RewardEarnedEvent`; `_TempHubMenu` в §9/§16/§19; имена editor-скриптов §17 — пользователь
- `IProfileStorage` типизирован под `IPlayerProfile` — обобщить для save-блока стройки — шаг 12
- Долг 8c: мёртвое поле `StuntsCardEntry._sceneKey` — шаг 11
- Kill-feed: символа `▸` (`DerbyHudPresenter.FormatKillFeedEntry`) нет ни в RussoOne, ни в LiberationSans, глобальные фоллбэки TMP пусты → вероятно квадрат; в Bungee нет кириллицы (важно для RU-локализации) — пользователь (найдено 2026-10-03; перевод шрифтов в Static отменён — остаются Dynamic)

## Ревью текущего шага
- 10a–10c: ревью пройдено до введения пайплайна (память step10-polish-status)
## Журнал (одна строка на шаг)
- Шаг 1 — done — 2026-05-22 — f08a2393 — каркас (без ITimeProvider)
- Шаг 2 — done — 2026-06-25 — 7793a988 — машина + chase-камера
- Шаг 3 — done — 2026-06-26 — b487ef38 — урон, damage-шейдер, detachable parts
- Шаг 4 — done — 2026-06-28 — 9f52e295 — AI Aggressor, Rigidbody-only
- Шаг 5 — done — 2026-07-02 — 4237ff54 — Derby: N AI, 3 профиля, пикапы, hazards, win/lose
- Шаг 6 — done — 2026-07-03 — 3ef5610b — eject + ragdoll + orbit-камера
- Шаг 7 — done — 2026-07-04 — 1b35903e — High Jump, рекорд по высоте
- Шаг 8 — done — 2026-07-11 — df750d2f — экономика, сцены+FSM, UI меты, HUD
- Шаг 9 — done — 2026-07-11 — 1c066307 — mobile input
- Сессия 0.5 — 2026-10-02 — 076cdaf0 — хуки, settings.json, progress.md; Opus 5.5, Sonnet — алиас (сверить в /status)
- Аудит — 2026-10-03 — только отчёт: критичных 0; Instantiate декалей 1, magic numbers ~20, логи шага 3, 2 неучтённых отклонения
- Фиксы по аудиту — done — 2026-10-03 — логи, мёртвые поля стратегии A, magic numbers → константы (+`CameraPriorities`, `_gearRpmFloor`); ревью PASS с 1-го раунда, EditMode 164/164, плейтест ок
