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
