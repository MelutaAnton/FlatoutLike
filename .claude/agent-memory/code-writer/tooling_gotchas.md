---
name: tooling-gotchas
description: Shell/tooling traps in this repo — arch-guard only fires on Edit/Write, python is a Store stub, files are LF
metadata:
  type: project
---

- Хук `arch-guard` срабатывает только на Edit/Write. .cs, записанные через Bash (heredoc/sed/perl), он НЕ проверяет — прогонять вручную:
  `echo '{"tool_input":{"file_path":"<abs path>"}}' | powershell -NoProfile -File .claude/hooks/arch-guard.ps1` (пустой вывод = чисто).
- `python` в Git Bash — заглушка Microsoft Store (печатает "Python" и ничего не делает). Для многострочных правок — `perl -0pi -e` или Edit.
- .cs в Assets/Development — LF (git конвертирует в CRLF при checkout-е), вставки с `\n` безопасны.
- Корневой неймспейс — `FlatOutLike` (Project Variables агента с `Flatuot2_Like` устарели), меню — `FlatOutLike/...`.

**Why:** обнаружено в шаге 11a (2026-10-03) — без ручного прогона хука нарушения ушли бы на ревью.
**How to apply:** при массовых правках через Bash — в конце прогнать хук по `git status` списку .cs.
