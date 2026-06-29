---
name: project-asmdef-vcontainer-transitive
description: Editor-asmdef обязан ссылаться на VContainer — editor-скрипты трогают GameLifetimeScope : LifetimeScope (база в VContainer); ссылки asmdef не транзитивны
metadata:
  type: project
---

При ревью/правке любого `.asmdef`, охватывающего папку `Scripts/Editor/`, проверяй наличие ссылки на `VContainer`.

**Why:** несколько editor-скриптов (BuildBootstrapSceneEditor, BuildDrivingTestSceneEditor, FillGameLifetimeScopeRefsEditor, SetupEnemyCarEditor) используют `FindAnyObjectByType<GameLifetimeScope>()` / `Undo.AddComponent<GameLifetimeScope>()`. `GameLifetimeScope : LifetimeScope`, а `LifetimeScope` лежит в сборке `VContainer`. Generic-вызов с constraint (`where T : Component/Object`) заставляет компилятор пройти по цепочке базовых типов → нужна ссылка на VContainer. Ссылки между asmdef **НЕ транзитивны**: `FlatOutLike.Editor → FlatOutLike → VContainer` не даёт Editor-сборке доступ к VContainer. Без явной ссылки — `error CS0012`. До введения asmdef всё компилировалось, потому что Assembly-CSharp-Editor авто-референсит все autoReferenced-пакеты.

**How to apply:** runtime `FlatOutLike.asmdef` и editor `FlatOutLike.Editor.asmdef` оба должны иметь `"VContainer"` в `references`. Тестовый asmdef VContainer НЕ требует (тесты трогают только pure-C# EnemyAIService и ScriptableObject-конфиги). Связано с [[project-namespace]].
