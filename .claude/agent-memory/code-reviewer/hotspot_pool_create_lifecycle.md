---
name: hotspot-pool-create-lifecycle
description: GameObjectPool.CreateInstance делает Instantiate(активный prefab) → Awake/OnEnable в позе префаба, затем OnDisable ДО Construct; OnDisable пуловых компонентов обязан быть null-guarded
metadata:
  type: project
---

`GameObjectPool.CreateInstance` = `Object.Instantiate(_prefab)` + `SetActive(false)`. Для активного префаба это значит:
Awake + OnEnable в позе корня префаба (обычно ноль) → OnDisable → (Get) SetPositionAndRotation → OnEnable → InjectGameObject/Construct.
С CarFactory→пул (пост-11, 2026-10-08) так живут и машины Derby.

**Why:** «позиция ставится до активации» верно только для очереди; свежий инстанс уже прошёл Awake/OnEnable в другой позе, и OnDisable у него зовётся ДО Construct (раньше с Instantiate(prefab,pos,rot) — никогда).

**How to apply:** при ревью новых компонентов на пулящихся префабах (машины, пикапы, VFX, обломки) — OnDisable/OnDestroy обязаны проверять `_eventBus == null`/`_initialized`; Awake не должен кэшировать мировую позицию. Компоненты ThirdParty на машине (RealisticEngineSound) — не проверяемы, флагать как риск плейтеста. См. [[project-di-lazy-singletons]].
