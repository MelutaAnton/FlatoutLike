# Progress — FlatOutLike

## Текущее состояние
- Текущий шаг: 11 «BaseHub-каркас», подшаг 11c (LoadSceneAsync + фейд) — статус: plan (11a, 11b done)
- Модели: code-writer — Opus (VContainer, FSM, ввод, триггеры), editor-script-creator — Sonnet
- Последний проверенный в Unity шаг: 11b (плейтест ок 2026-10-07)
- 11a итог: компиляция чистая с 1-го раза; меню прогнаны через MCP (Mobile Input Canvas в Derby, Create Building SO Set, Build Hub Character Prefab, Build Base Hub Layout); сцены MainMenu/Stunts_Hub удалены, Build Settings: Boot/BaseHub/Garage/Derby/HighJump; AudioConfig: запись StuntsHub удалена YAML-правкой (Setup Audio не гонялся — пересохранил бы Stunts_HighJump с незакоммиченными правками пользователя); EditMode 167/167
- После шага 11: `CarFactory` → спавн через пул. 10d (баланс OQ#2/#3/#5, пороги AI/движения/разлёта → SO, прогон §16 кора) — на шаге 13 или после idle-arcade слоя
- Отложено пользователем: спавн декалей мимо фабрики (`CarDamageVisualController.cs:219`)

## Definition of Done текущего шага
11b (§19 шаг 11, §6 «Запуск режимов», §10 BaseHub, OQ#11):
- [x] `BuildingEntry`: HasLaunchZone/LaunchState/LaunchZoneOffset/LaunchZoneSize; каталог: LaunchConfirmSeconds, LaunchIndicatorHeight; миграция ассета без затирания тюнинга
- [x] `HubLaunchService` (pure C#, ITickable): вход → таймер → один `ChangeState(LaunchState)`; выход сбрасывает; только в BaseHub; тесты `HubLaunchServiceTests`
- [x] `LaunchZoneController`: триггер + kinematic RB, детект по `HubCharacterController`, инъекция в build callback
- [x] `HubHudView`+`HubPresenter` (монеты, PLAY → DerbyMatch), `LaunchConfirmView`+`LaunchConfirmPresenter` (world-space круг над активной зоной)
- [x] Editor: зоны + маркеры, LaunchConfirmCanvas, HubHud в UICanvas (Button-стиль, raycastTarget), прошивка scope; HubGroup в префабе выключен; пути сцен из SceneKeys
- [x] Хвосты 11a: `_pausedFromHub`; ожидаемые предупреждения камер в хабе/Garage → Log
- [x] Доработка по плейтесту 2026-10-07: подписи площадок (`DisplayName`, двухстрочные у заглушек), круг — белое кольцо + жёлтый диск (выбор пользователя), автомасштаб по `VisualFootprint` с центрированием по bounds, раскладка ±15 / зоны 7.5–8.5 м от центра здания (без перекрытий), земля 60×60, камера (0,20,-15)/FOV45; префабы LaunchConfirmCanvas/HubHud НЕ делаем (канвасы в одной сцене — правило CLAUDE.md)
- [x] Unity: зоны Derby/Garage/HighJump запускают режимы через ~1 с, выход сбрасывает круг; PLAY → Derby; монеты в HUD; возвраты → хаб

## Отклонения от game-design.md
- Шаг 1/8: `_TempHubMenu` не было — были MainMenu/StuntsHub; заменены BaseHub в 11a — одобрено: да
- Шаг 1: `ITimeProvider` создаётся на шаге 13 — одобрено: да
- Шаг 3: декали `Instantiate` мимо фабрики — одобрено: нет (отложено, логика может быть удалена)
- Шаг 3: стратегия A заменена шейдером `_DamageAmount`, поля материалов удалены — одобрено: да
- Шаг 5: `CarFactory` без пула (§12) — одобрено: нет; исправить после шага 11
- Шаг 6: ручной eject по E (§3 допускает, §4 запрещает) — одобрено: да, оставляем
- Шаг 7: `LandingZone` удалён, приземление по успокоению таза — одобрено: да
- Шаг 7/8: High Jump — только высота, множитель флипов/score выпилены (§4, §7, §8, §16) — одобрено: да
- Шаг 8: per-scene scope, экран = загрузка сцены через `SceneLoaderService`; апгрейды per-car; нитро стартует полным — одобрено: да
- Полиш: Cinemachine 3 (`Cinemachine*Behaviour`); звук вне среза §1/§18 — одобрено: да
- Шаги 5–10: сервисы сверх §12 (Pause, SceneLoader, HighJumpSession, CarRegistry, RewardCrediter, CameraShake, Vfx, DamageStageResolver, AIInputProvider) — одобрено: да
- Шаг 10: 10d перенесён на шаг 13+ — одобрено: да
- Шаг 11: визуал башни — `Prefabs/Environment/HighRamp.prefab`; DerbyArena/Garage/заглушки — временные Synty-префабы в `BuildingCatalogConfig` — одобрено: да
- Шаг 11: камера BaseHub — одна vcam вне CameraRig/CameraService (§11), Follow/LookAt прошивает editor — одобрено: да
- Шаг 11: разбит на 11a/11b/11c; удаление MainMenu/StuntsHub перенесено в 11a (enum ломал компиляцию) — одобрено: да
- Шаг 11b: зона HighJumpTower запускает High Jump без гейта стройкой (гейт — шаг 12); OQ#11 закрыт — кнопка PLAY в HubHud ведёт сразу в Derby — одобрено: да (план 11b)

## Открытые вопросы
- `arch-guard` даст ложное срабатывание на `CarStatsConfig.cs:77` (`Instantiate(this)`) — пользователь
- `RootNamespace: Flatuot2_Like` в Project Variables агентов и `.claude/rules/editor-files.md` — фактически `FlatOutLike` — пользователь
- Папки `.claude/agent-memory/` внутри `Assets/Development/**`; пустая `Assets/Content` (метафайлы) — пользователь
- Противоречия в ГДД: §13 издатель `RewardEarnedEvent`; `_TempHubMenu` в §9/§16/§19; имена editor-скриптов §17 — пользователь
- `IProfileStorage` типизирован под `IPlayerProfile` — обобщить — шаг 12
- `▸` kill-feed нет в RussoOne/LiberationSans → квадрат; в Bungee нет кириллицы — пользователь
- `Build HighJump Scene` строит сетку в сцене — при наличии префаба HighRamp создаст дубль — пользователь

## Ревью текущего шага
- 11b: R1 PASS; R2 PASS (доработка по плейтесту); R3 FAIL — зоны Derby/Garage/HighJump перекрывались, сервис не возвращался к оставшейся зоне → исправлено (раскладка ±15, сервис хранит занятые зоны + 3 теста, editor-проверка перекрытий с радиусом капсулы); R4 PASS. Открытые Suggestions: ISP для IHubLaunchService, billboard круга, Undo при правке Image круга, GetScenePath → ScenePaths в 3 скриптах

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
- Шаг 10 — done — 2026-10-03 — 8f54e973 — pause, kill-feed, death-UX (10a–c); 10d перенесён на шаг 13+
- Сессия 0.5 — 2026-10-02 — 076cdaf0 — хуки, settings.json, progress.md; Opus 5.5, Sonnet — алиас
- Аудит — 2026-10-03 — только отчёт: критичных 0; Instantiate декалей 1, magic numbers ~20, логи шага 3
- Фиксы по аудиту — done — 2026-10-03 — логи, мёртвые поля, magic numbers → константы; ревью PASS, плейтест ок
- Шаг 11a — done — 2026-10-05 — cdba6810 — BaseHub: GameState-миграция, InputContext.Hub, персонаж+камера, сцена editor-скриптом, MainMenu\/Stunts_Hub удалены; ревью PASS, EditMode 167\/167, плейтест ок
- Шаг 11b — done — 2026-10-07 — (коммит ниже) — зоны запуска + круг, HubHud + PLAY (OQ#11), подписи/автомасштаб/раскладка хаба, Walking в аниматоре; ревью R1–R2 PASS, R3 FAIL→fix, R4 PASS; EditMode 185/185, плейтест ок
