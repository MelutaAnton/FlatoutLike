# CLI Workflow — работа с Claude Code CLI по проекту FlatOutLike

Этот документ — практический регламент работы с Claude Code CLI на этом конкретном проекте. Не методичка по CLI вообще, а **что делать пошагово**, чтобы не утонуть в переделках.

Источники правды для CLI:
- `CLAUDE.md` — архитектурные правила, нюансы Unity/VContainer, workflow оркестратора, ссылки на скилы.
- `docs/game-design.md` — ТЗ вертикального среза.
- `docs/progress.md` — текущее состояние: какой шаг, что сделано, отклонения, итоги ревью. Единственное место, где живёт состояние между сессиями.
- `.claude/settings.json` + `.claude/hooks/` — детерминированные проверки правил `CLAUDE.md` (grep-уровень), работают без участия модели.
- Этот файл — регламент промптинга.

---

## Базовые принципы

### 1. Один шаг — один промпт — одна проверка в Unity

Не «реализуй весь срез», не «сделай несколько шагов сразу». Один шаг из Implementation Order (§19 `game-design.md`) = одна сессия CLI = один Unity-checkpoint. После каждого шага запускаешь Unity, проверяешь acceptance-критерий, коммитишь.

### 2. План всегда перед кодом

В любом промпте, который требует от CLI писать новый код, **обязательно** есть фраза «Сначала покажи план файлов, жди моего \"ок\" перед началом написания». Без этого Claude может уйти в 2000 строк не туда. Прочитать план занимает 5 минут, откат плохого кода — час.

### 3. Источник правды — документы, не «общие знания»

В промпте всегда явная ссылка на конкретный раздел `game-design.md` или `CLAUDE.md`. «Сделай как в FlatOut 2» или «используй best practices» — фразы под запретом. Только: «по §X документа Y».

### 4. Запрет на расширение скоупа

В каждом промпте есть запрет на самодеятельность: «не выдумывай новые сервисы или конфиги сверх плана, если хочется добавить — спрашивай». Иначе CLI «из лучших побуждений» добавит 5 «полезных» классов, которых в game-design нет, и потом аудит вылавливает их три часа.

### 5. После каждых 3–4 шагов — архитектурный аудит

Накопление долга в AI-сгенерированном коде — реальная проблема. Лучше ловить нарушения еженедельно, чем разгребать в конце.

### 6. Состояние — в `progress.md`, а не в контексте сессии

Контекст сессии сжимается и теряется, файл — нет. Всё, что нужно помнить дольше одного хода (текущий шаг, Definition of Done, отклонения от ГДД, итоги ревью), оркестратор пишет в `docs/progress.md`. Хук `SessionStart` подгружает его в каждую новую сессию и после каждой компакции автоматически.

### 7. Механику проверяют хуки, смысл — ревьюер

Нарушения, которые ловятся grep'ом (`Resources.Load`, строковые идентификаторы, `Instantiate` вне фабрик, `DateTime.Now`), проверяет хук сразу после каждой записи файла — надёжно и без расхода внимания модели. Ревьюер тратит внимание на то, что grep не видит: границы слоёв, SOLID, контракты событий, FSM, magic numbers, соответствие Definition of Done.

---

## Роли моделей и effort

Оркестратор и ревьюер — одна модель (актуальный Opus). Поэтому независимость ревью обеспечивается **организацией**, а не разными моделями: ревьюер работает в чистом контексте и не видит рассуждений оркестратора (см. «Цикл шага» → ревью).

Исполнители, чья задача заранее определена одобренным планом, работают на актуальном Sonnet: на них приходится основной объём токенов, а страховку дают план, `arch-guard` и ревью на Opus. Диагноз и оценка — на Opus.

| Роль | Где задаётся | Модель | Effort |
|---|---|---|---|
| Оркестратор — планирование шага, архитектурные развилки, аудит, разбор ошибки после первого неудачного фикса, разбор второго FAIL ревью | основная сессия | `opus` | `xhigh` |
| Оркестратор — ведение шага после одобренного плана | основная сессия | `opus` | `high` |
| `code-writer` — по умолчанию | frontmatter агента | `sonnet` | `high` |
| `code-writer` — эскалация (см. ниже) | параметр `model` при вызове | `opus` | `high` |
| `editor-script-creator` | frontmatter агента | `sonnet` | `high` |
| `unity-error-analyzer` | frontmatter агента | `opus` | `high` |
| `code-reviewer` | frontmatter агента | `opus` | `high` |

Правила:
- Effort по умолчанию зависит от версии модели (Opus 5.5 стартует на `medium`), поэтому уровни задаются **явно**: основная сессия — `claude --model opus --effort xhigh` на старте шага, после твоего «ок» на план — `/effort high`; агенты — полем `effort` во frontmatter (оно перекрывает effort сессии).
- Модели задаются **алиасами** `opus` / `sonnet`: они всегда указывают на последнюю версию семейства, поэтому при выходе новой модели документы и агенты править не нужно. Сабагент с `opus` при оркестраторе на Opus работает ровно на той же версии, что и оркестратор.
- Версию, на которую указывает алиас, определяет версия Claude Code: новая модель приходит с `claude update`. Поэтому **`claude update` — только между шагами** (предыдущий шаг в статусе `done`), никогда посреди шага: иначе смена модели наложится на правки после ревью. После обновления — сверить модель в `/status` и записать строкой в журнал `progress.md` («Шаг N: модели — Opus X, Sonnet Y»).
- Если нужно заморозить версию (например, новая модель хуже на этом проекте или идёт финал среза), алиас переназначается в одном месте — блок `env` в `.claude/settings.json`: `"ANTHROPIC_DEFAULT_OPUS_MODEL": "<полный ID>"` и/или `"ANTHROPIC_DEFAULT_SONNET_MODEL": "<полный ID>"`. Документы и агенты при этом не меняются; разморозка — удалить эти строки.
- `unity-error-analyzer` остаётся на Opus сознательно: вызывается редко и коротко (экономия от Sonnet копеечная), а неверный диагноз стоит дорого — из него вырастают слои костылей.
- **`ultracode` не включать.** Он поверх `xhigh` запускает динамические workflow, которые ломают дисциплину «один шаг — одна проверка».
- `xhigh` не держать на всю сессию: на рутине он не даёт качества, но съедает лимиты.

### Frontmatter агентов

Существующие агенты в `.claude/agents/` не переписываются — меняются только перечисленные поля frontmatter, тело агента сохраняется.

**Контракт ревью живёт в одном месте — в теле `.claude/agents/code-reviewer.md`, раздел `## Контракт ревью`.** Блок ниже — заготовка для проектов, где у ревьюера такого раздела ещё нет: добавляется в конец тела агента **только если заголовка `## Контракт ревью` в файле нет**. Если раздел уже есть — ничего не добавлять и не сливать, правится только сам агент. Этот документ и оркестратор на контракт лишь ссылаются.

```yaml
# .claude/agents/code-writer.md и .claude/agents/editor-script-creator.md
model: sonnet
effort: high
```

```yaml
# .claude/agents/unity-error-analyzer.md
model: opus
effort: high
```

```yaml
# .claude/agents/code-reviewer.md
model: opus
effort: high
tools: Read, Grep, Glob, Bash   # без Edit/Write: ревьюер не правит код; Bash — только для git diff / git status
```

Блок, который добавляется в конец тела `code-reviewer.md`:

```markdown
## Контракт ревью

Вход (только это, другого контекста у тебя нет и быть не должно):
- изменения шага: `git diff HEAD` + новые файлы из `git status --porcelain -uall`;
- разделы docs/game-design.md, указанные в задаче;
- раздел «Definition of Done текущего шага» из docs/progress.md.

Код не правишь: Bash используешь только для git diff / git status / git log.

Задача — найти нарушения, а не подтвердить корректность. Если нарушений нет — это нужно
доказать проверкой каждого пункта DoD, а не общим впечатлением.

Проверять:
1. Каждый пункт DoD — выполнен / не выполнен / не проверяем без Unity (с причиной).
2. Файлы вне плана шага и логика, относящаяся к будущим шагам (расширение скоупа).
3. Границы слоёв: бизнес-логика только в pure C# Services; MonoBehaviour — только Unity API.
4. Magic numbers в Controllers/Services — параметры геймплея только из SO.
5. Правила CLAUDE.md, которые не ловятся grep'ом: RegisterComponent vs RegisterInstance,
   InjectGameObject + явный Launch(...) у объектов из пула, kinematic Rigidbody на триггерах,
   переходы FSM только через GameFlowService, события — readonly struct.
6. SOLID / Clean Architecture: интерфейс у каждого сервиса, зависимости через конструктор / Construct.

Формат ответа:
VERDICT: PASS | FAIL        (PASS — только при нуле Critical)
Critical:    файл:строка — нарушенное правило — как исправить
Warnings:    файл:строка — что и почему
Suggestions: коротко
DoD:         пункт — статус
```

### Эскалация `code-writer` на Opus

По умолчанию `code-writer` работает на Sonnet (frontmatter). Оркестратор вызывает его на Opus, передавая `model: opus` в вызове субагента — этот параметр приоритетнее frontmatter. Эскалация нужна там, где ошибки не ловятся ни компилятором, ни `arch-guard`, а проявляются только в Play Mode.

Эскалация — **весь шаг** на Opus, если шаг затрагивает:
- регистрации VContainer в `GameLifetimeScope` (новые сервисы с зависимостями, коллекции, Func-регистрации — циклы и конфликты коллекций видны только на `Build()`);
- FSM, push-pop и переходы `GameFlowService`;
- физику, триггеры, ragdoll, буферизацию ввода Update → FixedUpdate;
- save/load и время (`IProfileStorage`, `ITimeProvider`, offline-расчёт).

Эскалация — **только правка**, если ревью вернуло FAIL с Critical архитектурного характера (границы слоёв, DI, FSM, пул): исправление делает `code-writer` на Opus, остальной шаг — как был.

Без эскалации (Sonnet): enum/const, SO-классы конфигов, pure C# сервисы без новых DI-связей, MVP-view и презентеры по готовому образцу, правки SO-баланса.

Для этого проекта по умолчанию на Opus: шаги **1** (каркас, DI, FSM), **5** (коллекции профилей и спавн матча), **6** (eject, ragdoll, переключение камер по состоянию), **7** (триггеры, детекция вращений), **11** (контекст ввода Hub, зоны-триггеры), **13** (время, offline-расчёт, push-pop попапа). Остальные — Sonnet. Список — отправная точка: оркестратор указывает модель `code-writer` в плане шага, и твоё «ок» на план утверждает её.

---

## `docs/progress.md` — внешняя память пайплайна

Файл создаётся в Сессии 0.5 и ведётся **только оркестратором**. Хук `SessionStart` вставляет его в контекст целиком, а лимит такой вставки — 10 000 символов, поэтому файл держится коротким: в журнале одна строка на шаг, раунды ревью закрытого шага удаляются.

### Шаблон

```markdown
# Progress — FlatOutLike

## Текущее состояние
- Текущий шаг: <N> «<название>» — статус: plan | in-progress | review | awaiting-unity-check | done
- Последний проверенный в Unity шаг: <N-1> (коммит <hash>)
- Следующее действие: <одна строка>

## Definition of Done текущего шага
- [ ] <критерий из §19 / §16 game-design.md>
- [ ] <…>

## Отклонения от game-design.md
- Шаг <N>: <что и почему> — одобрено пользователем: да / нет

## Открытые вопросы
- <вопрос> — к кому: пользователь / следующий шаг

## Ревью текущего шага
- Раунд 1: FAIL — Critical: <кратко> → исправлено
- Раунд 2: PASS

## Журнал (одна строка на шаг)
- Шаг 1 — done — <дата> — <коммит> — <одна строка итога>
```

### Когда обновляется

| Момент | Что пишет оркестратор |
|---|---|
| План одобрен | статус `in-progress`, DoD шага (из §19 / §16) |
| Код написан, хуки чистые | статус `review` |
| Ревью завершено | строка раунда в «Ревью текущего шага»; при PASS — статус `awaiting-unity-check` |
| Пользователь подтвердил Unity-checkpoint | статус `done`, строка в журнал, раунды ревью удаляются, DoD очищается под следующий шаг |
| Любое отступление от ГДД | строка в «Отклонения» — до того, как отступление попадёт в код |

Статус `done` выставляется **только** после твоего явного «ок» по проверке в Unity. Хук `progress-guard` не даёт оркестратору закончить ход, если код в `Assets/Development/` изменён позже `progress.md`.

---

## Hooks — детерминированные проверки

Три хука, все на PowerShell (Windows), лежат в `.claude/hooks/`, подключаются в `.claude/settings.json` проекта (коммитится в git). Хуки из settings срабатывают и внутри субагентов, то есть проверяют и то, что пишет `code-writer`.

| Хук | Событие | Что делает | Эффект |
|---|---|---|---|
| `session-context.ps1` | `SessionStart` (startup, resume, clear, compact) | печатает `docs/progress.md` | файл попадает в контекст сессии и восстанавливается после компакции |
| `arch-guard.ps1` | `PostToolUse` на `Edit\|Write` | прогоняет механические правила `CLAUDE.md` по только что изменённому `.cs` в `Assets/Development/` | exit 2 → список нарушений `файл:строка` уходит Claude, он обязан исправить |
| `progress-guard.ps1` | `Stop` | если есть `.cs` в `Assets/Development/`, изменённые позже `progress.md` | exit 2 → Claude не может закончить ход, пока не обновит `progress.md` |

Проверка компиляции хуком не делается (для неё нужен открытый Unity) — её выполняет оркестратор через MCP `get_console_logs` перед ревью, см. «Цикл шага».

### `.claude/settings.json` (блок `hooks`, сливается с существующими настройками)

```json
{
  "hooks": {
    "SessionStart": [
      {
        "matcher": "startup|resume|clear|compact",
        "hooks": [
          {
            "type": "command",
            "command": "powershell.exe",
            "args": ["-NoProfile", "-ExecutionPolicy", "Bypass", "-File", "${CLAUDE_PROJECT_DIR}/.claude/hooks/session-context.ps1"]
          }
        ]
      }
    ],
    "PostToolUse": [
      {
        "matcher": "Edit|Write",
        "hooks": [
          {
            "type": "command",
            "command": "powershell.exe",
            "args": ["-NoProfile", "-ExecutionPolicy", "Bypass", "-File", "${CLAUDE_PROJECT_DIR}/.claude/hooks/arch-guard.ps1"],
            "statusMessage": "arch-guard: проверка правил CLAUDE.md"
          }
        ]
      }
    ],
    "Stop": [
      {
        "hooks": [
          {
            "type": "command",
            "command": "powershell.exe",
            "args": ["-NoProfile", "-ExecutionPolicy", "Bypass", "-File", "${CLAUDE_PROJECT_DIR}/.claude/hooks/progress-guard.ps1"]
          }
        ]
      }
    ]
  }
}
```

### `.claude/hooks/session-context.ps1`

```powershell
# SessionStart: подгружает docs/progress.md в контекст новой сессии и после компакции
[Console]::OutputEncoding = [Text.Encoding]::UTF8
$progress = Join-Path $env:CLAUDE_PROJECT_DIR 'docs/progress.md'
if (Test-Path -LiteralPath $progress) {
    Write-Output 'Текущее состояние пайплайна (docs/progress.md, подгружено SessionStart-хуком):'
    Get-Content -LiteralPath $progress -Raw -Encoding UTF8
} else {
    Write-Output 'docs/progress.md ещё не создан: проект до Сессии 0.5 из docs/cli-workflow.md.'
}
exit 0
```

### `.claude/hooks/arch-guard.ps1`

```powershell
# PostToolUse(Edit|Write): механические правила CLAUDE.md по только что изменённому .cs
[Console]::InputEncoding  = [Text.Encoding]::UTF8
[Console]::OutputEncoding = [Text.Encoding]::UTF8

# true, если в CLAUDE.md Active Input Handling = Input System (New) и legacy Input запрещён
$legacyInputForbidden = $false

$payload = [Console]::In.ReadToEnd() | ConvertFrom-Json
$path = $payload.tool_input.file_path
if (-not $path -or $path -notmatch '\.cs$') { exit 0 }
$norm = $path -replace '\\', '/'
if ($norm -notmatch '/Assets/Development/') { exit 0 }
if (-not (Test-Path -LiteralPath $path)) { exit 0 }
$isEditor = $norm -match '/Editor/'

# Pattern — regex; Allow — пути, где правило не действует; Editor — действует ли правило в Editor-скриптах
$rules = @(
    @{ Pattern = 'Resources\.Load'; Message = 'Resources.Load запрещён'; Editor = $true },
    @{ Pattern = 'GetComponent(InChildren|InParent)?<[^>]+>\(\)\s*\?\?'; Message = '?? с Unity-объектом запрещён, только явный if (x == null)'; Editor = $true },
    @{ Pattern = '\bInstantiate\s*\('; Message = 'Спавн только через фабрики поверх IPoolManager'; Allow = '/Factories/|/Core/Pool/' },
    @{ Pattern = '\bIPoolManager\b'; Message = 'IPoolManager доступен только фабрикам'; Allow = '/Factories/|/Core/Pool/|/Installers/' },
    @{ Pattern = 'DateTime\.(Utc)?Now'; Message = 'Время только через ITimeProvider'; Allow = '/Core/Time/' },
    @{ Pattern = 'CompareTag\s*\(\s*"'; Message = 'Строковый тег: вынести в Tags' },
    @{ Pattern = '\.(SetBool|SetFloat|SetInteger|SetTrigger|ResetTrigger|GetBool|GetFloat|GetInteger|Play|CrossFade)\s*\(\s*"'; Message = 'Строковый идентификатор анимации: использовать AnimatorHashes' },
    @{ Pattern = 'Animator\.StringToHash'; Message = 'StringToHash только в AnimatorHashes'; Allow = 'AnimatorHashes\.cs$' },
    @{ Pattern = 'SceneManager\.LoadScene(Async)?\s*\(\s*"'; Message = 'Имя сцены строкой: использовать enum/const' }
)
if ($legacyInputForbidden) {
    $rules += @{ Pattern = '(?<![\w.])Input\.(Get|mouse|touch|any)'; Message = 'Legacy UnityEngine.Input запрещён' }
}

$lines = @(Get-Content -LiteralPath $path -Encoding UTF8)
$violations = @()
for ($i = 0; $i -lt $lines.Count; $i++) {
    $line = $lines[$i]
    if ($line.TrimStart().StartsWith('//')) { continue }
    foreach ($r in $rules) {
        if ($isEditor -and -not $r.Editor) { continue }
        if ($r.Allow -and $norm -match $r.Allow) { continue }
        if ($line -match $r.Pattern) { $violations += ('{0}:{1}  {2}' -f $norm, ($i + 1), $r.Message) }
    }
}

if ($violations.Count -eq 0) { exit 0 }
[Console]::Error.WriteLine('arch-guard: нарушения правил CLAUDE.md в только что изменённом файле:')
$violations | ForEach-Object { [Console]::Error.WriteLine("  $_") }
[Console]::Error.WriteLine('Исправь до передачи на ревью. Если срабатывание ложное, укажи это в отчёте шага и в docs/progress.md.')
exit 2
```

### `.claude/hooks/progress-guard.ps1`

```powershell
# Stop: не даёт закончить ход, пока docs/progress.md отстаёт от кода в Assets/Development
[Console]::InputEncoding  = [Text.Encoding]::UTF8
[Console]::OutputEncoding = [Text.Encoding]::UTF8
$payload = [Console]::In.ReadToEnd() | ConvertFrom-Json
if ($payload.stop_hook_active) { exit 0 }   # ход уже продолжен из-за этого хука, не зацикливаемся

$root     = $env:CLAUDE_PROJECT_DIR
$progress = Join-Path $root 'docs/progress.md'

$changed = @(git -C $root -c core.quotePath=false status --porcelain -uall -- 'Assets/Development' 2>$null |
    ForEach-Object { ($_.Substring(3) -split ' -> ')[-1].Trim('"') } |
    Where-Object { $_ -match '\.cs$' })
if ($changed.Count -eq 0) { exit 0 }

if (-not (Test-Path -LiteralPath $progress)) {
    [Console]::Error.WriteLine('progress-guard: docs/progress.md не найден, создай его по шаблону из docs/cli-workflow.md.')
    exit 2
}

$progressTime = (Get-Item -LiteralPath $progress).LastWriteTimeUtc
$stale = @($changed | Where-Object {
    $p = Join-Path $root $_
    (Test-Path -LiteralPath $p) -and ((Get-Item -LiteralPath $p).LastWriteTimeUtc -gt $progressTime)
})
if ($stale.Count -eq 0) { exit 0 }

[Console]::Error.WriteLine('progress-guard: код изменён после последнего обновления docs/progress.md:')
$stale | Select-Object -First 10 | ForEach-Object { [Console]::Error.WriteLine("  $_") }
[Console]::Error.WriteLine('Обнови docs/progress.md (статус шага, отклонения, итоги ревью) и только потом заверши ход.')
exit 2
```

Требования к скриптам:
- Сохранять в **UTF-8 with BOM** — Windows PowerShell 5.1 без BOM читает кириллицу в строках скрипта как ANSI, и сообщения превращаются в кракозябры.
- Коды выхода: блокирует только `exit 2`; `exit 1` Claude Code считает неблокирующей ошибкой и молча продолжает.
- Список правил `arch-guard` — дополняется по мере появления новых механических граблей в `CLAUDE.md` (новое правило = одна строка в `$rules`).

---

## Цикл шага

Порядок внутри одного шага Implementation Order. Он дополняет Workflow оркестратора из `CLAUDE.md`, не заменяет его.

1. **План** (`xhigh`). Оркестратор проходит чек-лист планирования из `CLAUDE.md`, показывает план файлов и модель `code-writer` на шаг (Sonnet / Opus по правилу эскалации, с одной строкой причины) и ждёт «ок».
2. **Фиксация.** После «ок» — `/effort high`; в `progress.md` записываются статус `in-progress` и DoD шага.
3. **Реализация.** `code-writer` на модели из утверждённого плана (+ `editor-script-creator` параллельно при `EDITOR_SCRIPT_NEEDED:`). `arch-guard` проверяет каждый записанный файл; его замечания исправляются сразу, до ревью.
4. **Компиляция.** Оркестратор через MCP `get_console_logs` убеждается, что в консоли нет ошибок компиляции. Ревью некомпилирующегося кода не запускается.
5. **Ревью** в чистом контексте. Оркестратор передаёт `code-reviewer` **только** номер и название шага, номера разделов `game-design.md` и ссылку на DoD в `progress.md`. Не передаются: собственные рассуждения, «почему сделано так», самооценка, пересказ изменений — ревьюер читает diff сам. Шаблон делегирования:

   ```
   Ревью шага <N> «<название>».
   Спецификация: docs/game-design.md §<X>, §<Y>.
   DoD: раздел «Definition of Done текущего шага» в docs/progress.md.
   Изменения: git diff HEAD + новые файлы из git status --porcelain -uall.
   Работай строго по «Контракту ревью» из своего описания.
   ```

6. **Правки.** При FAIL — исправления через `code-writer` (на Opus, если среди Critical есть архитектурные — см. «Эскалация `code-writer` на Opus»), затем **новый** вызов `code-reviewer` (не продолжение прежнего: чистый контекст важнее экономии). Максимум 2 раунда. Второй FAIL — стоп: оркестратор переключается на `xhigh`, записывает ситуацию в `progress.md` и выносит решение тебе.
7. **Отчёт.** При PASS — статус `awaiting-unity-check`, краткий отчёт: что сделано, что проверить в Unity, какие пункты DoD проверяемы только плейтестом.
8. **Твой Unity-checkpoint** → «ок» → статус `done`, строка в журнал, коммит.

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
5. Что из инфраструктуры пайплайна (docs/cli-workflow.md, разделы 
   «Роли моделей и effort», «docs/progress.md», «Hooks») уже есть:
   docs/progress.md, .claude/settings.json с блоком hooks, 
   .claude/hooks/*.ps1, поля model/effort во frontmatter агентов 
   code-writer, code-reviewer, editor-script-creator, 
   unity-error-analyzer? 
   Перечисли, чего не хватает.

НЕ начинай писать код, пока я не дам команду. Сначала ответы 
на вопросы.
```

### Что проверять в ответе

- Claude корректно идентифицировал содержимое `Assets/ThirdParty/` (видит 1Poly Cars, Battle Stadium и т.д.).
- Понимает, что для текущего этапа мы используем **стратегию A** из §4 game-design (material swap), без Blender и mesh-стейджей.
- Упоминает критичные нюансы из CLAUDE.md: `RegisterComponent` vs `RegisterInstance`, kinematic Rigidbody на триггерах, инъекция объектов из пула.
- Не предлагает «общую структуру проекта» от себя — а опирается на `Assets/Development/Scripts/` иерархию из CLAUDE.md.
- Список недостающей инфраструктуры пайплайна точный — он станет ТЗ для Сессии 0.5.

Если ответ слабый — задаёшь уточняющие вопросы до тех пор, пока понимание не станет адекватным. Только потом переходишь к сессии 0.5.

---

## Сессия 0.5 — Инфраструктура пайплайна

Запускается **один раз**, до шага 1. Разворачивает хуки, `progress.md` и настройки агентов из этого документа — без ручных операций.

### Промпт сессии 0.5

```
Разверни инфраструктуру пайплайна строго по docs/cli-workflow.md, 
разделы «Роли моделей и effort», «docs/progress.md — внешняя память 
пайплайна» и «Hooks — детерминированные проверки».

1. Создай .claude/hooks/session-context.ps1, arch-guard.ps1, 
   progress-guard.ps1 — содержимое дословно из документа, 
   кодировка UTF-8 with BOM.
2. Добавь блок hooks в .claude/settings.json. Если файл уже есть — 
   слей с существующими настройками, ничего в них не удаляя.
3. Во frontmatter .claude/agents/code-writer.md, 
   editor-script-creator.md, unity-error-analyzer.md, 
   code-reviewer.md выставь поля model/effort (и tools для 
   code-reviewer) из документа — алиасами opus / sonnet, не полными ID. 
   Тело агентов не переписывай. Если в code-reviewer.md нет 
   заголовка «## Контракт ревью» — добавь в конец тела блок-заготовку 
   из документа дословно; если заголовок есть — ничего не добавляй.
4. Создай docs/progress.md по шаблону: текущий шаг — 1 «Каркас», 
   статус plan, DoD — критерии шага 1 из §19 game-design.md, 
   журнал пустой.
5. Проверь arch-guard: создай временный 
   Assets/Development/Scripts/_HookSmokeTest.cs со строкой 
   Resources.Load, убедись, что хук вернул нарушение, затем удали 
   файл (и .meta, если Unity успел его создать).

Сначала покажи план (список файлов и что в каждом меняется), 
жди моего «ок». Код игры в этой сессии не пишется.
```

### Что проверять после сессии 0.5

- Перезапусти CLI (`claude --model opus --effort xhigh`) — при старте в контексте должно появиться содержимое `progress.md` (спроси «какой сейчас шаг и статус?» — ответ без чтения файлов).
- `/hooks` показывает три хука из Project Settings.
- В `.claude/agents/code-reviewer.md` есть `model`, `effort`, `tools` и ровно один раздел `## Контракт ревью` (проверка: поиск по заголовку даёт одно совпадение).
- Смоук-тест `arch-guard` сработал, временный файл удалён.
- Коммит инфраструктуры отдельным коммитом.

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

Регламент работы — «Цикл шага» из docs/cli-workflow.md:
- Сначала покажи план: список файлов которые создашь, в каком 
  порядке, и какие интерфейсы у каждого сервиса. Жди моего 
  «ок» перед началом написания кода.
- После «ок» — запиши статус и DoD шага в docs/progress.md.
- Используй агентов из CLAUDE.md: основная работа через 
  code-writer — на этом шаге на Opus (каркас DI и FSM, 
  см. «Эскалация code-writer на Opus»), по завершении — проверка компиляции через MCP 
  и code-reviewer в чистом контексте (передаёшь только шаг, 
  разделы ГДД и ссылку на DoD, без своих пояснений).
- Замечания arch-guard исправляются сразу, до ревью.
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

После завершения шага 1 обнови progress.md (статус 
awaiting-unity-check) и жди от меня результат проверки в Unity, 
прежде чем переходить дальше.
```

### Что проверять в Unity после шага 1

- Пустая сцена с `GameBootstrap` стартует без ошибок в Console.
- Логирование показывает, что `GameLifetimeScope` инициализировался, `IEventBus` зарегистрирован.
- `GameFlowService` стартует в `BaseHub` состоянии (до шага 11 — временная сцена-заглушка `_TempHubMenu`).
- Структура папок `Assets/Development/Scripts/` соответствует CLAUDE.md.
- `docs/progress.md` отражает шаг 1 со статусом `awaiting-unity-check` и результатами ревью.

---

## Сессии 2-13 — Шаблон промпта для остальных шагов

После того, как шаг N проверен и работает, переходишь к шагу N+1 по этому шаблону. Имена и детали шагов смотри в Implementation Order (§19 `game-design.md`).

### Универсальный шаблон промпта

```
Шаг <N-1> проверен в Unity и работает — отметь его done в 
docs/progress.md и добавь строку в журнал. Переходим к шагу <N> 
из Implementation Order (§19 game-design.md): «<название шага>».

Что должно появиться:
<классы / конфиги / сцены из §19 для этого шага>

Цель шага по game-design: <цель из §19>.

Регламент — «Цикл шага» из docs/cli-workflow.md:
1. План файлов и модель code-writer (по правилу эскалации), 
   жди ок. После ок — DoD шага в progress.md.
2. code-writer на утверждённой модели; замечания arch-guard — сразу.
3. EDITOR_SCRIPT_NEEDED: помечай и делегируй editor-script-creator.
4. Компиляция через MCP, затем code-reviewer в чистом контексте 
   (только шаг, разделы ГДД, ссылка на DoD). Максимум 2 раунда.
5. Не лезь в последующие шаги.
6. После завершения — progress.md (awaiting-unity-check) и жди 
   моей проверки.

Дополнительные ограничения для этого шага:
- <специфика из раздела «Конкретные дополнительные ограничения 
  по шагам» ниже>
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
- Сцена `BaseHub` собирается editor-скриптом `Build Base Hub Layout` по `BuildingCatalogConfig` (площадки, зоны, spawn point персонажа), не вручную.
- `HubCharacterController` — MonoBehaviour с Unity API only (`CharacterController.Move`, Animator); движение читает из `IInputProvider.Move` — тот же интерфейс, что машина (KeyboardInputProvider WASD и MobileInputProvider-стик с шага 9 уже работают). Параметры — только из `HubCharacterConfig` SO.
- Персонаж — префаб водителя (Mixamo Humanoid) с локомоушн-аниматором Idle/Run; НЕ трогать `DriverController` (ragdoll-логика), это отдельный контроллер.
- В `InputRouter` добавляется контекст `Hub` (Move, Pause) — активен в `GameState.BaseHub`.
- `HubCameraBehaviour` реализует `ICameraBehaviour`, follow за персонажем; параметры — из `HubCameraConfig` SO.
- Все зоны (вложение, сбор, запуск) — Trigger-Collider + **kinematic Rigidbody** (правило CLAUDE.md).
- `LaunchZoneController`: вход → радиальный таймер (~1 сек, значение в SO) → переход состояния; выход из зоны сбрасывает таймер. Никакого мгновенного запуска.
- `GameState.BaseHub` уже в enum с шага 1 — меняется только сцена (заглушка `_TempHubMenu` → карта), FSM и переходы не трогаем.
- Заглушки трюков (LongJump, FireRings, Bowling, FieldGoal, HighCard) — те же `BuildSiteController` в disabled-режиме через флаг `enabled=false` в конфиге, не отдельный класс.

**Шаг 12 (Стадийная стройка):**
- `BuildProgressService` — pure C#, в `Services/Progression/` (§15.1). Save — **отдельный блок** через `IProfileStorage`, не внутри профиля `MetaProgressService`.
- Вложение — ПОТОКОВОЕ: игрок стоит в зоне площадки, монеты списываются пачками по `InvestStreamRate` из `HubCharacterConfig`. Никаких кнопок «Вложить». Прогресс — `BuildingInvestProgressEvent` → world-space ценник `BuildingPanelView`.
- Все цены этапов — в `BuildingCatalogConfig`; unlock-эффекты декларативно в SO (`BuildingId` → unlock payload), никаких switch по BuildingId в коде сервиса.
- Разблокировка через подписку на `BuildingCompletedEvent` — никаких прямых вызовов из `BuildProgressService` в `GameFlowService`.
- Визуальные стадии — свап дочерних префабов в `BuildSiteController`; префабы стадий из `Assets/Development/Prefabs/Hub/<BuildingId>/`.
- High Jump с этого шага гейтится стройкой башни (до шага 12 был доступен свободно через заглушку — это ок).

**Шаг 13 (Idle-доход):**
- Всё время — через `ITimeProvider` (UTC), созданный на шаге 1. `DateTime.Now` по коду = нарушение для аудита (и блок от `arch-guard`).
- Offline-расчёт: `min(elapsed, offlineCapHours)`, отрицательный elapsed (перевод часов назад) клампить в 0.
- `CoinStackController` — чистая ВИЗУАЛИЗАЦИЯ баланса `IPlayerProfile` (кап отображаемых монет из SO), не отдельный ресурс и не второй source of truth.
- Сбор дохода — проходом персонажа через зону сбора (`CoinBubbleController`), не тапом.
- Доход публикуется через `RewardEarnedEvent { source = IdleIncome }` — §15.3 предохранитель в действии, `EconomyService` не переписывается.
- `WelcomeBackPopup` — через push-pop состояний `GameFlowService` (тот же механизм, что Pause), не отдельный флаг/канвас поверх.
- Кнопка «x2 за рекламу» — disabled-заглушка, никакой SDK-интеграции в срезе.
- В этом же шаге ребаланс `RewardConfig` под sink стройки (Open Question #9) — правки только в SO.

---

## Архитектурный аудит — каждые 3-4 шага

После шагов 3, 6, 9 и 12 (или когда чувствуешь, что накопилось — больше двух «странных» решений CLI подряд) запусти явный аудит.

Аудит запускается **в свежей сессии** (`/clear` или новый запуск CLI) на `xhigh`: состояние придёт из `progress.md` через `SessionStart`, а контекст не будет засорён рассуждениями, которыми код писался. `arch-guard` проверяет только изменяемые файлы в момент записи — аудит прогоняет те же механические правила по всему коду (ловит то, что было написано до появления правила) и дальше занимается семантикой.

### Промпт аудита

```
/effort xhigh

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
- Расхождения кода с разделом «Отклонения от game-design.md» 
  в docs/progress.md: отклонения, которых нет в списке, 
  и одобренные отклонения, которые так и не реализованы.

Покажи отчёт с конкретными файлами и строками. Не исправляй 
автоматически — я выберу, что фиксить. Итог аудита одной 
строкой добавь в журнал docs/progress.md.
```

### Что делать с отчётом

- Тривиальные нарушения (одна-две magic-number или один забытый kinematic Rigidbody) — фиксь точечными промптами «исправь нарушение X в файле Y».
- Системные нарушения (например, бизнес-логика во всех Controllers) — это означает, что Claude **систематически** уходит от архитектуры. Откатываешь к последнему коммиту и **пересматриваешь свой промпт** — что в нём дало Claude свободу так делать.
- Механическое нарушение, которое аудит нашёл, а `arch-guard` пропустил, — повод добавить правило в `$rules` хука, чтобы дальше оно ловилось в момент записи.

---

## Обработка ошибок Unity Console

> **С подключённым MCP Unity** (см. `~/.claude/rules/mcp-unity.md`) стектрейс можно не
> копипастить: если Unity открыт, Claude сам заберёт логи через `get_console_logs` и
> осмотрит сцену. Достаточно написать «в консоли ошибка, разбери» — регламент ниже
> (причина → файл:строка → план фикса → моё «ок») при этом сохраняется.

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

**Если первый фикс не помог** — переключай оркестратор на `/effort xhigh` перед повторным разбором и проси записать гипотезу и результат в «Открытые вопросы» `progress.md`: так следующая попытка (или следующая сессия) не повторит отброшенную гипотезу.

Частые причины ошибок на этом проекте (для подстраховки — Claude должен искать сначала здесь):

- `NullReferenceException` в `OnEnable`/`Start` MonoBehaviour: `Construct()` ещё не вызван, нужен флаг `_initialized` или перенос логики в `Construct()`. См. CLAUDE.md.
- `OnTriggerEnter` не срабатывает: ни у одного из участников нет Rigidbody. Добавить kinematic Rigidbody на триггер-зону.
- VContainer не инжектит: `RegisterInstance` вместо `RegisterComponent`.
- Объект из пула «не инициализируется»: забыт `_container.InjectGameObject(go)` + явный `Launch(...)` после `pool.Get()`.

---

## Между сессиями — синхронизация контекста

У Claude нет памяти между сессиями, но теперь её роль выполняет `docs/progress.md`: хук `SessionStart` вставляет его в контекст при каждом запуске, `/clear` и компакции. Синхронизирующий префикс поэтому короче — он не пересказывает состояние, а просит сверить файл с кодом.

### Префикс новой сессии

```
Состояние проекта — в docs/progress.md (уже у тебя в контексте).
Быстро сверь его с кодом: шаги со статусом done действительно 
реализованы в Assets/Development/Scripts/ (осмотр структуры, 
не полный аудит), а незакоммиченные изменения соответствуют 
текущему шагу. Если видишь расхождение — скажи сразу, 
ничего не исправляя.

Затем переходим к промпту шага <Z>.
```

Это нужно, потому что Claude в новой сессии может «уверенно» начать переписывать то, что уже сделано, увидев «незавершённую структуру». Подтверждение состояния — защита от этого.

Если CLI отвечает, что `progress.md` в контексте нет, — хук `SessionStart` не отработал: проверь `/hooks` и запуск в доверенной папке проекта.

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
- **Не передавать** ревьюеру рассуждения оркестратора и пересказ изменений — только шаг, разделы ГДД и ссылку на DoD. Иначе ревью превращается в самоодобрение.
- **Не продолжать** прежний вызов `code-reviewer` после правок — каждый раунд ревью это новый вызов с чистым контекстом.
- **Не отключать** хуки (`disableAllHooks`) «чтобы быстрее» — ложное срабатывание чинится правкой правила в `arch-guard.ps1`, а не отключением проверки.
- **Не держать** `xhigh` всю сессию и **не включать** `ultracode`.
- **Не запускать** `claude update` посреди шага — с ним может смениться модель.
- **Не переводить** `unity-error-analyzer` и `code-reviewer` на Sonnet ради экономии — это те роли, где сильная модель окупается.
- **Не хранить** состояние шага только в чате — всё, что должно пережить сессию, в `progress.md`.

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
- [ ] Claude Code обновлён (`claude update`) до актуальной версии — от неё зависит, на какие модели указывают алиасы `opus` / `sonnet`. Дальше — обновлять только между шагами.
- [ ] Папка `docs/` существует, в ней `game-design.md` и этот файл `cli-workflow.md`.
- [ ] Создан git-репозиторий, чтобы можно было коммитить после каждого шага (на git опирается `progress-guard`).
- [ ] Папка проекта отмечена как доверенная в Claude Code — без этого хуки не запускаются.

После прохождения чек-листа — открывай CLI и начинай с **Сессии 0**, затем **Сессия 0.5** (хуки, `progress.md`, frontmatter агентов создаются там, вручную ничего не делаешь).
