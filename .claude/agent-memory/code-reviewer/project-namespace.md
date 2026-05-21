---
name: project-namespace
description: Корневой namespace проекта — FlatOutLike (НЕ Flatuot2_Like), даже несмотря на имя репозитория
metadata:
  type: project
---

Корневой namespace всего C#-кода в проекте — `FlatOutLike` (например, `FlatOutLike.Core.Events`, `FlatOutLike.Services.Flow`, `FlatOutLike.Models`), несмотря на то, что репозиторий и папка проекта называются `Flatuot2_Like`. Это противоречит значению `RootNamespace: Flatuot2_Like` из системного промпта — но фактически в коде используется `FlatOutLike`.

**Why:** Имя проекта в Unity и имя репозитория — это игровое название с опечаткой и цифрой (Flatuot2_Like ~ FlatOut 2 Like), а namespace выбран по чистовому английскому имени бренда (FlatOut).

**How to apply:** При ревью считать `FlatOutLike.*` корректным namespace. Менюшный root для `[MenuItem]` тоже `FlatOutLike/...`, не `Flatuot2_Like/...` (хотя `.claude/rules/editor-files.md` упоминает `Flatuot2_Like/...` — это устаревшая инструкция, реальный код использует `FlatOutLike/...`). Если встретится `Flatuot2_Like.*` в namespace — это, наоборот, нарушение.
