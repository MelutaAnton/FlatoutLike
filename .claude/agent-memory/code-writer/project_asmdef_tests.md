---
name: project-asmdef-tests
description: asmdef структура и тесты AI-мозга, добавленные в проект
metadata:
  type: project
---

Введены три asmdef-файла (июнь 2026):
- `Assets/Development/Scripts/FlatOutLike.asmdef` — runtime, references: ["VContainer"]. Unity.InputSystem НЕ добавлен (legacy Input, InputSystem нигде не используется).
- `Assets/Development/Scripts/Editor/FlatOutLike.Editor.asmdef` — Editor only, references: ["FlatOutLike"]. VContainer не нужен в editor-скриптах.
- `Assets/Development/Tests/EditMode/FlatOutLike.Tests.EditMode.asmdef` — тесты, references: ["FlatOutLike", "UnityEngine.TestRunner", "UnityEditor.TestRunner"], precompiledReferences: ["nunit.framework.dll"], autoReferenced: false, defineConstraints: ["UNITY_INCLUDE_TESTS"].

Добавлен `Assets/Development/Scripts/Models/Layers.cs` — static class с const Vehicle/Wall/Hazard/Pickup, namespace FlatOutLike.Models, без using UnityEngine.

Тесты: `Assets/Development/Tests/EditMode/EnemyAIServiceTests.cs` (9 тестов, namespace FlatOutLike.Tests). Покрывают FSM-переходы EnemyAIService: Idle→Pursue, Pursue→Ram, Stuck→Recover, Orbit→Recover, Recover-реверс, Recover→Pursue+Grace, Grace-блок, Moonwalk-guard.

**Why:** архитектурная задача — изоляция сборок + coverage AI без Unity-зависимостей.
**How to apply:** при добавлении нового слоя кода проверь, что он попадёт в FlatOutLike asmdef (не вне Scripts/). Тесты в Tests/EditMode; для PlayMode создать отдельный asmdef по той же схеме.
