---
name: project-asmdef-ui-missing
description: FlatOutLike и FlatOutLike.Editor asmdef УЖЕ ссылаются на UnityEngine.UI и Unity.TextMeshPro (проверено 2026-10-06) — `using UnityEngine.UI` безопасен
metadata:
  type: project
---
`Assets/Development/Scripts/FlatOutLike.asmdef` и `Assets/Development/Scripts/Editor/FlatOutLike.Editor.asmdef` содержат в references `UnityEngine.UI`, `Unity.TextMeshPro`, `VContainer`, `Unity.Cinemachine`, URP Runtime/Core (проверено на ревью 11b).

**Why:** раньше ссылки на UnityEngine.UI не было, и первый `using UnityEngine.UI` ронял всю сборку (CS0234). Исправлено.

**How to apply:** не флагать `using UnityEngine.UI` / TMP в коде проекта. При новом пакете (namespace вне этого списка) — проверить, что asmdef на него ссылается; ссылки asmdef не транзитивны (см. [[project-asmdef-vcontainer-transitive]]).
