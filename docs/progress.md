# Progress — FlatOutLike

## Текущее состояние
- Текущий шаг: 12 «Стадийная стройка», подшаг 12a — done (плейтест ок 2026-10-13); next — план 12b (стадии из Synty + VFX/звук)
- Модели: code-writer — Opus (экономика+save, триггеры, VContainer), editor-script-creator — Sonnet
- Последний проверенный в Unity шаг: 12a (плейтест ок 2026-10-13)
- 11a итог: компиляция чистая с 1-го раза; меню прогнаны через MCP (Mobile Input Canvas в Derby, Create Building SO Set, Build Hub Character Prefab, Build Base Hub Layout); сцены MainMenu/Stunts_Hub удалены, Build Settings: Boot/BaseHub/Garage/Derby/HighJump; AudioConfig: запись StuntsHub удалена YAML-правкой (Setup Audio не гонялся — пересохранил бы Stunts_HighJump с незакоммиченными правками пользователя); EditMode 167/167
- 10d (баланс OQ#2/#3/#5, пороги AI/движения/разлёта → SO, прогон §16 кора) — на шаге 13 или после idle-arcade слоя
- Отложено пользователем: спавн декалей мимо фабрики (`CarDamageVisualController.cs:219`)
- Доп. задача (2026-10-13, одобрено): editor-only чит «5555» → +1000 монет (`Services/Dev/CheatSequence` + `DevCoinsCheat` под `#if UNITY_EDITOR`, EntryPoint во всех сценах) — done 2026-10-13 (5d665fdf), плейтест ок

## Definition of Done текущего шага
12a (§19 шаг 12 «Стадийная стройка», §6/§8/§16 BaseHub; план одобрен 2026-10-09; разбивка 12a петля на заглушках → 12b стадии из Synty + VFX/звук): `BuildProgressService` (потоковое вложение пачками `InvestBatchSeconds`, `InvestStreamRate` 25/с), `IEconomyService.TrySpend`, отдельный save-блок `IHubSaveStorage` (`FlatOutLike.Hub.*`), одна зона на здание (роль по состоянию: вложение / запуск; `LaunchZone*` → `Zone*`), `BuildSiteController` (свап стадий-заглушек), `BuildingPanelView`+Presenter (world-space ценник), гейтинг HighJump стройкой. Цены: HighJump 25/40/60, TicketBooth 60/100/140 (+5/8/12 в мин), Tribunes 100/150/200 (+8/12/18) — стартовые, тюнинг после плейтеста.
- [ ] HighJumpTower строится за 3 этапа потоком из баланса, стоя в зоне; каждый этап меняет визуальную стадию
- [ ] Завершение башни открывает запуск HighJump; до этого зона HighJump не запускает режим
- [ ] TicketBooth и Tribunes строятся; ценник показывает «осталось X» и растущий rate
- [ ] 5 заглушек «Coming soon» — без зоны и вложений
- [ ] Недостаток монет останавливает поток без «рывка» после пополнения; на паузе/под экраном загрузки поток стоит
- [ ] Прогресс стройки и баланс сохраняются согласованно и переживают перезапуск (отдельный save-блок)
- [ ] EditMode-тесты зелёные (+ BuildProgressService, TrySpend, save-блок, гейтинг HubLaunchService)

## Отклонения от game-design.md
- Шаг 1/8: `_TempHubMenu` не было — были MainMenu/StuntsHub; заменены BaseHub в 11a — одобрено: да
- Шаг 1: `ITimeProvider` создаётся на шаге 13 — одобрено: да
- Шаг 3: декали `Instantiate` мимо фабрики — одобрено: нет (отложено, логика может быть удалена)
- Шаг 3: стратегия A заменена шейдером `_DamageAmount`, поля материалов удалены — одобрено: да
- Шаг 5: `CarFactory` без пула (§12) — исправлено после шага 11: машины выдаются через `IPoolManager`, но в пул не возвращаются (спавн один раз на сцену; возврат со сбросом — при появлении респавна) — одобрено: да (вариант A)
- Шаг 6: ручной eject по E (§3 допускает, §4 запрещает) — одобрено: да, оставляем
- Шаг 7: `LandingZone` удалён, приземление по успокоению таза — одобрено: да
- Шаг 7/8: High Jump — только высота, множитель флипов/score выпилены (§4, §7, §8, §16) — одобрено: да
- Шаг 8: per-scene scope, экран = загрузка сцены через `SceneLoaderService`; апгрейды per-car; нитро стартует полным — одобрено: да
- Полиш: Cinemachine 3 (`Cinemachine*Behaviour`); звук вне среза §1/§18 — одобрено: да
- Шаги 5–10: сервисы сверх §12 (Pause, SceneLoader, HighJumpSession, CarRegistry, RewardCrediter, CameraShake, Vfx, DamageStageResolver, AIInputProvider) — одобрено: да
- Шаг 10: 10d перенесён на шаг 13+ — одобрено: да
- Шаг 11: визуал башни — `Prefabs/Environment/HighRamp.prefab`; DerbyArena/Garage — временные Synty-префабы в `BuildingCatalogConfig`; с 12a заглушки трюков рисуются пустырём + «Coming soon» (их `VisualPrefab` в каталоге не используется) — одобрено: да
- Шаг 11: камера BaseHub — одна vcam вне CameraRig/CameraService (§11), Follow/LookAt прошивает editor — одобрено: да
- Шаг 11: разбит на 11a/11b/11c; удаление MainMenu/StuntsHub перенесено в 11a (enum ломал компиляцию) — одобрено: да
- Шаг 11c: переходы сцен — `LoadSceneAsync` (в ГДД нет; решение 2026-07-16), двухфазно per-scene; чёрный фейд 11c заменён экраном загрузки в 11d — одобрено: да (план 11c)
- Шаг 11d: экран загрузки (заголовок + EN-подсказка + слайдер с фейк-кривой, минимальное время по месту назначения в `LoadingScreenConfig`) вместо чёрного фейда; показывается и в Boot; старая сцена под экраном заморожена (timeScale 0, звук на паузе), переходы во время загрузки отбрасываются — в ГДД нет, решение 2026-10-08 — одобрено: да (план 11d)
- Шаг 12a: одна зона на здание, роль по состоянию (недостроено — вложение `InvestZoneController`, построено — запуск `LaunchZoneController`, шаг 13 — сбор); зона — дочерний объект площадки, а не часть `BuildSiteController` (§6); запуск HighJump гейтится запросом `IBuildProgressService.IsBuilt` в `HubLaunchService`, без авто-запуска при завершении стройки (нужно перезайти в зону) — одобрено: да (план 12a)
- Шаг 12a: отдельный save-блок стройки — собственный интерфейс `IHubSaveStorage` (`PlayerPrefsHubSaveStorage`, ключи `FlatOutLike.Hub.Build.<int>.*`) по паттерну `IProfileStorage`, а не сам `IProfileStorage` (он типизирован под `IPlayerProfile`); §8/§16; закрывает открытый вопрос «обобщить IProfileStorage» — одобрено: да (план 12a)
- Шаг 12: разбит на 12a (петля стройки, стадии — editor-примитивы) и 12b (префабы стадий в `Prefabs/Hub/<BuildingId>/` из Synty + VFX/звук) — одобрено: да (план 12a)
- Шаг 11b: зона HighJumpTower запускает High Jump без гейта стройкой (гейт — шаг 12; введён в 12a); OQ#11 закрыт — кнопка PLAY в HubHud ведёт сразу в Derby — одобрено: да (план 11b)

## Открытые вопросы
- `arch-guard` даст ложное срабатывание на `CarStatsConfig.cs:77` (`Instantiate(this)`) — пользователь
- `RootNamespace: Flatuot2_Like` в Project Variables агентов и `.claude/rules/editor-files.md` — фактически `FlatOutLike` — пользователь
- Папки `.claude/agent-memory/` внутри `Assets/Development/**`; пустая `Assets/Content` (метафайлы) — пользователь
- Противоречия в ГДД: §13 издатель `RewardEarnedEvent`; `_TempHubMenu` в §9/§16/§19; имена editor-скриптов §17 — пользователь
- `▸` kill-feed нет в RussoOne/LiberationSans → квадрат; в Bungee нет кириллицы — пользователь
- `Build HighJump Scene` строит сетку в сцене — при наличии префаба HighRamp создаст дубль — пользователь
- Возврат машин в пул (вариант B): нужен контракт сброса (урон/шейдер, детали, водитель-ragdoll с перепарентованными костями, нитро, AI, VFX); ловушка — у машины игрока `EnemyCarController` снесён DestroyImmediate, а префаб общий с врагами — при появлении респавна/рематча
- 12a: баланс сохраняется вместе со стройкой только во flush-точках (этап/выход из зоны/Dispose); если между пачками кто-то вызовет `_meta.Save()` (Credit/покупка) и процесс убьют — потеря монет до одного несохранённого хвоста стройки; актуально для idle-дохода — шаг 13

## Ревью текущего шага
- (закрыт) 12a: реализация готова — компиляция чистая с 1-го раза; меню Create Building SO Set + Build Base Hub Layout прогнаны через MCP (стадии/зоны/панели/BuildSiteController на 10 зданиях, перекрытий зон нет, повторный прогон без изменений); EditMode 246/246 (+34); R1 FAIL — Critical: (1) нет строки про `IHubSaveStorage` в «Отклонениях» (исправлено + разбивка 12a/12b, строка 11 про заглушки обновлена, OQ закрыт); (2) `RegisterInstance<IReadOnlyList<BuildingPanelView>>` для MonoBehaviour → RegisterComponent (правка у code-writer Opus) + warning: PlayerPrefs.Save на каждую пачку → flush на завершении этапа/выходе из зоны/Dispose; правки: компиляция чистая, EditMode 256/256 (+`IsUnderConstruction` в IBuildProgressService для презентера); R2 PASS. Warnings R2: (1) PLAY, стоя в зоне стройки, — flush только в Dispose, порядок OnDestroy старой сцены и Awake новой не гарантирован → возможен «возврат» монет — исправлено: flush в первом Tick вне BaseHub (+тест, EditMode 257/257); (2) Time.deltaTime в сервисе (как HubLaunchService) — на шаг 13 с ITimeProvider; (3) FindObjectsByType панелей в Configure; (4) два PlayerPrefs.Save подряд — микроокно; ПЛЕЙТЕСТ 2026-10-13: scope BaseHub не собирается — `Conflict implementation type BuildingPanelView` (VContainer запрещает несколько Singleton-регистраций одного типа в коллекцию; R1-фикс RegisterComponent в цикле ошибочен, оба ревью пропустили) → держатель `BuildingPanelViews` (plain C#) через RegisterInstance — исправлено: компиляция чистая, Play Mode smoke через MCP (Boot → BaseHub) без ошибок и warning, EditMode 257/257
- (закрыт) CarFactory→пул: реализация готова — компиляция чистая с 1-го раза, EditMode 210/210 (+3 теста пула); аудит жизненного цикла компонентов машины/водителя — проблем нет; R1 PASS. Warnings: (1) свежий инстанс пула создавался в позе префаба, не в точке спавна — исправлено (Instantiate сразу в позе; тест-зонд проверяет ПЕРВЫЙ OnEnable свежего и OnEnable инстанса из очереди); EditMode 212/212; плейтест ок; (2) у машин нет явного Launch (§16), поведение стартует в Construct после Configure — паттерн шага 5, не регрессия; (3) дубль логики двух Get — в правке (1)
- (закрыт) 11d: реализация готова — компиляция чистая с 1-го раза; меню `Build Loading Screen (All Scenes)` прогнано через MCP (конфиг 4 места + 12 подсказок, префаб, 5 сцен включая Boot, старый фейд удалён без Missing-ссылок); EditMode 205/205; бар на встроенном UISprite, виньетка пропущена; R1 FAIL — Critical: нет строки 11d в «Отклонениях» (исправлено); warnings исправлены (музыка в Boot, Pop при загрузке, заморозка с Null-экраном, дубли дефолтов, 3 неверные подсказки); правки: компиляция чистая, EditMode 207/207; R2 PASS, плейтест ок. Warnings R2: ветка «сцены нет в Build Settings» оставляет timeScale 0 и чужое FSM-состояние (ошибка разработчика); первый кадр новой сцены — рывок unscaledDeltaTime может съесть растворение (как в 11c); пока allowSceneActivation=false, Unity держит прочие async-операции. Suggestions: строки лога GameFlowService строятся до проверки IsLoading; подсказка «Eject» в Derby — правка контента
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
- Шаг 11d — done — 2026-10-08 — 5357c711 — экран загрузки (заголовок + подсказка + фейк-слайдер, мин. время по месту назначения), заморозка старой сцены, переходы во время загрузки отбрасываются, Boot с экраном; ревью R1 FAIL (доки) → R2 PASS, EditMode 207/207, плейтест ок
- CarFactory→пул — done — 2026-10-09 — cee92862 — машины через IPoolManager.Get(prefab, pos, rot) без Return (вариант A), свежий инстанс сразу в позе; закрыто отклонение шага 5; ревью PASS, EditMode 212/212, плейтест ок
- Шаг 12a — done — 2026-10-13 — HASH12A — петля стройки: BuildProgressService (поток пачками, flush баланс+стройка), IHubSaveStorage, одна зона на здание, стадии-заглушки, ценники, гейт HighJump; ревью R1 FAIL (доки + DI) → R2 PASS; плейтест: Conflict implementation type и ценник HighJump вне кадра → исправлено; EditMode 262/262
