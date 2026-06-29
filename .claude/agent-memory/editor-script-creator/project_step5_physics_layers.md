---
name: step5-physics-layers
description: Physics layers setup — CreatePhysicsLayersEditor, AddCrashWallToTestDrivingEditor WallName visibility, AISensorConfig mask fix
metadata:
  type: project
---

## Physics Layers Editor Scripts (Step 5)

### Новые скрипты

**CreatePhysicsLayersEditor.cs** — `Assets/Development/Scripts/Editor/Setup/`
- Меню: `FlatOutLike/Setup/Create Physics Layers`
- Регистрирует слои Vehicle/Wall/Hazard/Pickup в TagManager (индексы 8–31)
- Паттерн: загружает TagManager ОДИН РАЗ, вызывает EnsureLayer для каждого слоя, потом ApplyModifiedProperties если были изменения
- Назначает слой Vehicle рекурсивно всем CarController.transform.root
- Назначает слой Wall объекту AddCrashWallToTestDrivingEditor.WallName
- SetLayerRecursively — хелпер, важен для коллайдеров на дочерних объектах

### Правки существующих скриптов

**AddCrashWallToTestDrivingEditor.cs** — `WallName` изменён с `private` на `internal const`
- Позволяет CreatePhysicsLayersEditor ссылаться на имя "CrashTestWall" без дублирования строки

**CreateDefaultAISensorConfigEditor.cs**
- Добавлен `using FlatOutLike.Models;`
- `SyncFields`: заменил `maskProp.intValue = ~0` на `LayerMask.GetMask(Layers.Wall, Layers.Hazard)`
- Защита: если GetMask вернул 0 → LogWarning, маска НЕ меняется (не ломаем avoidance)
- Новый ассет: теперь тоже вызывает `SyncFields(config)` после CreateAsset (раньше пропускал)
- Обновлён Debug.Log в SyncFields — показывает фактическое значение маски

### Порядок запуска меню

1. `FlatOutLike/Setup/Add Crash Wall to Test Driving` (если нужна стена)
2. **`FlatOutLike/Setup/Create Physics Layers`** ← сначала!
3. `FlatOutLike/Setup/Setup Enemy Car`
4. `FlatOutLike/Configs/Create Default AISensorConfig` ← зависит от слоёв

### Ключевые нюансы

- `Layers` (константы строк) — `FlatOutLike.Models.Layers`, создаётся параллельным агентом code-writer
- LayerMask.NameToLayer работает сразу после ApplyModifiedProperties в том же вызове Editor (Unity 6)
- `Undo.RegisterFullObjectHierarchyUndo` — использовать для рекурсивной смены слоя иерархии
- Индексы 0–7 — встроенные слои Unity, не трогать; пользовательские — 8–31

**Why:** Без физических слоёв AI-сенсор воспринимал машины как препятствия (маска Everything), что вызывало «орбитальное» поведение врага вокруг игрока.

**How to apply:** При любых задачах на физические слои — использовать этот скрипт как образец; не хардкодить строки слоёв, всегда брать из `Layers.*`.
