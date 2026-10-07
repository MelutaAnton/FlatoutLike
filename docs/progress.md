# Progress — FlatOutLike

## Текущее состояние
- Текущий шаг: 11 «BaseHub-каркас» — статус: done (11a–11d); next — `CarFactory` → спавн через пул (план не составлен)
- Модели: code-writer — Opus (VContainer, FSM, ввод, триггеры), editor-script-creator — Sonnet
- Последний проверенный в Unity шаг: 11d (плейтест ок 2026-10-08)
- 11a итог: компиляция чистая с 1-го раза; меню прогнаны через MCP (Mobile Input Canvas в Derby, Create Building SO Set, Build Hub Character Prefab, Build Base Hub Layout); сцены MainMenu/Stunts_Hub удалены, Build Settings: Boot/BaseHub/Garage/Derby/HighJump; AudioConfig: запись StuntsHub удалена YAML-правкой (Setup Audio не гонялся — пересохранил бы Stunts_HighJump с незакоммиченными правками пользователя); EditMode 167/167
- После шага 11: `CarFactory` → спавн через пул. 10d (баланс OQ#2/#3/#5, пороги AI/движения/разлёта → SO, прогон §16 кора) — на шаге 13 или после idle-arcade слоя
- Отложено пользователем: спавн декалей мимо фабрики (`CarDamageVisualController.cs:219`)

## Definition of Done текущего шага
11d (§19 шаг 11; решение пользователя 2026-10-08, план одобрен 2026-10-08): экран загрузки вместо чёрного фейда — заголовок места назначения + случайная подсказка (EN) + слайдер с процентом. Слайдер: фейк-кривая к 75% (ease-out), затем медленный рост к 90%, рывок до 100% только после готовности (`allowSceneActivation=false` до progress 0.9) и минимального времени (хаб/гараж 0.8 с, Derby/HighJump 2 с — `LoadingScreenConfig`). Двухфазно per-scene: новая сцена закрыта экраном до первого кадра (build callback), прячет его в `Start`. Старая сцена под экраном заморожена (timeScale 0, звук на паузе, музыка не перезапускается), повторный переход отбрасывается до смены FSM. `ILoadingScreen`/`NullLoadingScreen`/`LoadingScreenView` заменяют фейд; префаб `Prefabs/UI/LoadingScreenCanvas` в 5 сценах (включая Boot); `ScreenFadeCanvas` и классы фейда удалены.
- [ ] Все переходы, включая старт с Boot, показывают экран с заголовком, подсказкой и слайдером
- [ ] Слайдер идёт к 75%, рывок до 100% только при готовой сцене; минимальное время соблюдается по месту назначения
- [ ] Стык сцен не виден (экран на 100% без подсказки в обеих фазах)
- [ ] Старая сцена под экраном заморожена: нет звука/движения, трек новой сцены не стартует дважды; пауза во время загрузки невозможна
- [ ] Двойной запрос перехода не меняет состояние FSM (`ChangeState`/`Push` при `IsLoading` игнорируются)
- [ ] Прямой запуск любой сцены в редакторе без ошибок; без префаба — мгновенный переход (NullLoadingScreen); Game view в Edit Mode не закрыт
- [ ] `SceneManager.LoadScene*` — только в `SceneLoaderService`
- [ ] EditMode-тесты зелёные (+ `LoadingProgressModelTests`, обновлённые SceneLoader/Pause/GameFlow-тесты)

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
- Шаг 11c: переходы сцен — `LoadSceneAsync` (в ГДД нет; решение 2026-07-16), двухфазно per-scene; чёрный фейд 11c заменён экраном загрузки в 11d — одобрено: да (план 11c)
- Шаг 11d: экран загрузки (заголовок + EN-подсказка + слайдер с фейк-кривой, минимальное время по месту назначения в `LoadingScreenConfig`) вместо чёрного фейда; показывается и в Boot; старая сцена под экраном заморожена (timeScale 0, звук на паузе), переходы во время загрузки отбрасываются — в ГДД нет, решение 2026-10-08 — одобрено: да (план 11d)
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
- 11d: реализация готова — компиляция чистая с 1-го раза; меню `Build Loading Screen (All Scenes)` прогнано через MCP (конфиг 4 места + 12 подсказок, префаб, 5 сцен включая Boot, старый фейд удалён без Missing-ссылок); EditMode 205/205; бар на встроенном UISprite, виньетка пропущена; R1 FAIL — Critical: нет строки 11d в «Отклонениях» (исправлено); warnings исправлены (музыка в Boot, Pop при загрузке, заморозка с Null-экраном, дубли дефолтов, 3 неверные подсказки); правки: компиляция чистая, EditMode 207/207; R2 PASS, плейтест ок. Warnings R2: ветка «сцены нет в Build Settings» оставляет timeScale 0 и чужое FSM-состояние (ошибка разработчика); первый кадр новой сцены — рывок unscaledDeltaTime может съесть растворение (как в 11c); пока allowSceneActivation=false, Unity держит прочие async-операции. Suggestions: строки лога GameFlowService строятся до проверки IsLoading; подсказка «Eject» в Derby — правка контента
- (закрыт) 11c: реализация готова — компиляция чистая с 1-го раза; меню `Build Screen Fade Canvas (All Scenes)` прогнано через MCP (префаб + 4 сцены, scope прошит, Boot не тронут); EditMode 191/191; R1 PASS, плейтест ок. Warnings: (1) окно перехода — FSM/музыка/ввод уже нового состояния, старая сцена живёт под шторкой ~0.25 с + загрузка (трек стартует дважды, второй ChangeState проходит в FSM) — на решение пользователя после плейтеста; (2) префаб alpha=1 → Game view в Edit Mode закрыт чёрным; (3) пересериализация scope в Garage/Derby (удалены устаревшие поля) — шум. Suggestions: длительности → UI-конфиг SO при появлении; фейд — строка в «Отклонения» (добавлена)
- (закрыт) 11b: R1 PASS; R2 PASS (доработка по плейтесту); R3 FAIL — зоны Derby/Garage/HighJump перекрывались, сервис не возвращался к оставшейся зоне → исправлено (раскладка ±15, сервис хранит занятые зоны + 3 теста, editor-проверка перекрытий с радиусом капсулы); R4 PASS. Открытые Suggestions: ISP для IHubLaunchService, billboard круга, Undo при правке Image круга, GetScenePath → ScenePaths в 3 скриптах

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
- Шаг 11b — done — 2026-10-07 — 4200c61f — зоны запуска + круг, HubHud + PLAY (OQ#11), подписи/автомасштаб/раскладка хаба, Walking в аниматоре; ревью R1–R2 PASS, R3 FAIL→fix, R4 PASS; EditMode 185/185, плейтест ок
- Шаг 11c — done — 2026-10-08 — c4e4f10f — LoadSceneAsync + фейд в чёрное (двухфазно per-scene, ScreenFadeCanvas в 4 сценах, Boot без фейда); ревью R1 PASS, EditMode 191/191, плейтест ок
- Шаг 11d — done — 2026-10-08 — HASH11D — экран загрузки (заголовок + подсказка + фейк-слайдер, мин. время по месту назначения), заморозка старой сцены, переходы во время загрузки отбрасываются, Boot с экраном; ревью R1 FAIL (доки) → R2 PASS, EditMode 207/207, плейтест ок
