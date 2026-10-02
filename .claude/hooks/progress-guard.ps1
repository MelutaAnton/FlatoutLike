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
