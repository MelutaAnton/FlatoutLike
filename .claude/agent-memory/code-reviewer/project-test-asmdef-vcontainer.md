---
name: project-test-asmdef-vcontainer
description: EditMode test asmdef УЖЕ ссылается на VContainer (проверено 2026-10-06) — new-инстанс ITickable-сервиса в тестах безопасен; риск CS0012 закрыт
metadata:
  type: project
---
`Assets/Development/Tests/EditMode/FlatOutLike.Tests.EditMode.asmdef` references `["FlatOutLike","VContainer","UnityEngine.TestRunner","UnityEditor.TestRunner"]` + nunit (проверено на ревью 11b, 2026-10-06).

**Why:** раньше (до шага 7) ссылки на VContainer не было, и `new XxxService()` для класса с `VContainer.Unity.ITickable` грозил CS0012 на всю тест-сборку. Ссылку добавили; `HubLaunchServiceTests`, `HighJumpSessionServiceTests` компилируются.

**How to apply:** не флагать `new`-инстанс ITickable/IStartable-сервиса в тестах как риск. Если asmdef снова изменится — перепроверить список references. Фактический результат прогона — `%LOCALAPPDATA%Low/DefaultCompany/Flatuot2_Like/TestResults.xml` (атрибуты total/passed/failed у `<test-run>`, можно грепнуть имя фикстуры).
