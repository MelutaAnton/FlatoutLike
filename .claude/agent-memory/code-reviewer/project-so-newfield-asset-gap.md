---
name: project-so-newfield-asset-gap
description: Review hot-spot — new [SerializeField] in a config with existing .asset; top-level fields keep C# initializer, struct-array elements do NOT
metadata:
  type: project
---

Когда diff добавляет новые `[SerializeField]` поля в существующий ScriptableObject-конфиг, проверяй, есть ли уже сохранённый `.asset` (`Assets/Development/Data/Configs/...`) и есть ли в его YAML новый ключ.

Нюанс (уточнено 2026-10-03): поле ВЕРХНЕГО уровня SO, отсутствующее в YAML, при десериализации сохраняет значение C#-инициализатора (Unity сначала конструирует объект, потом накладывает данные) — это Warning «проверить значение в инспекторе / re-serialize», не Critical. Опасный случай — `arraySize++` через SerializedProperty (новый элемент копирует предыдущий/зануляется, инициализаторы не применяются — урок аудио-слоя) и, вероятно, поля [Serializable]-STRUCT-элементов. Уточнено 2026-10-06 (ревью 11b): для [Serializable] CLASS-элементов уже сохранённого массива недостающее поле ПОЛУЧИЛО C#-инициализатор (`BuildingEntry.LaunchZoneSize = (6,3,6)` у entry 3–9, которых миграция не трогала) — Unity конструирует class-элемент через ctor.

**Why:** Editor-скрипты `CreateDefault*Editor` идемпотентны через "asset exists → skip" и не дописывают новые поля в существующий asset. 2026-06: `EnemyPersonalityConfig` asset не имел `_circleBreakTime`/`_graceTime` — sync не прогонялся.

**How to apply:** Для `EnemyPersonalityConfig` есть upsert `CreateDefaultEnemyPersonalityConfigEditor.SyncNewTuningFields` — новое tuning-поле добавить в `fieldNames` и прогнать меню. Для полей в элементах массивов (DamageStage и т.п.) — поднимать выше Warning, если значение по умолчанию ≠ 0 и критично для поведения.
