---
name: hotspot-hub-thirdparty-visuals
description: Хаб-визуалы из ThirdParty-паков — Junkyard_1 без коллайдеров, Battle_Stadium Arena_Environment тащит рантайм-скрипты и глобальный Volume; что проверять при смене VisualPrefab
metadata:
  type: project
---

При смене `VisualPrefab` в `BuildingCatalogConfig` (ревью 2026-10-22, «визуалы хаба») проверять содержимое префаба, а не только путь:

- **Junkyard_1** (`Assets/ThirdParty/Junkyard_1/Prefabs/*`: factory_007, wreck_car_*, tires_*, barrel_001, container_001) — НИ ОДНОГО коллайдера. Составной `Prefabs/Hub/Garage/Garage_Final` из них = персонаж хаба проходит сквозь здание (прежний Synty-офис имел 2 коллайдера). R1 этой задачи — Critical; R2 PASS: `BuildHubGaragePrefabEditor.EnsureColliders` кладёт OBB BoxCollider на sibling `Collider_<деталь>[_n]` (9 шт.), сироты при удалении детали не чистятся. Новые Junkyard-составные — проверять тот же механизм.
- **Battle_Stadium / `Prefabs/Hub/Arena/Arena_Environment`** (префаб пользователя) — внутри глобальный URP `Volume` (выключается override'ом в `BuildBaseHubLayoutEditor.DisableVolumes`), 21 Animator + 14 SMR, рантайм-скрипты пака: `CharacterRandom` ×12 (таймер не сбрасывается → `SetInteger` каждый кадр после 10 с), `EntryPoint` (демо, `_shouldInitOnStart: 0`), `ObjectSwitcher` (Awake гасит объекты), `MoveAnimation`, `OscillateRotation`, `NavMeshModifierVolume`. Габарит по рендерам ~794 м → вписан в 12 м (scale 0.015).
- `keepPlaceholderCollider` в SyncStages берёт «твёрдость» заглушек из наличия коллайдера у `VisualPrefab` — визуал без коллайдеров делает бескоулайдерными и плейсхолдеры стадий.
- `ComputeLaunchZoneForward` = footprint/2 + 3 + 0.5; при правке `VisualFootprint` в ассете проверять, что `ZoneOffset` пересчитан (Garage 10 м оставлен 7.5 при формуле 8.5).

**How to apply:** при ревью новых хаб-визуалов грепать `^--- !u!(54|6[45]|13[56]) ` (Rigidbody/коллайдеры), `!u!114` → m_Script GUID → `*.cs.meta` в ThirdParty, и `172515602e62fb746b5d573b38a5fe58` (URP Volume). См. [[project-step12b-stage-scale]], [[project-hub-launch-zones]].
