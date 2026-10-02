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
