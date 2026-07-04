---
path: "**/Editor/**"
---

## Известные нюансы Editor-скриптов

### Требования к Editor-скриптам
- Каждый скрипт доступен через меню: `[MenuItem("Flatuot2_Like/...")]` (категории и naming — в skill `add-editor-script`)
- Поддержка Undo: `Undo.RegisterCreatedObjectUndo()` для всех созданных объектов
- Логирование: `Debug.Log("[Editor] Created ...")` для каждого действия
- Идемпотентность: повторный запуск не дублирует объекты (проверять существующие)
- Все editor-скрипты обёрнуты в `#if UNITY_EDITOR` или лежат в папке Editor/

### Идемпотентность: проверять компонент, а не только GameObject
При создании объектов на сцене стандартный паттерн `if (GameObject.Find("X") != null) return`
не работает если в сцене уже есть объект с таким именем (например, дефолтная `Main Camera`
от Unity), но без нужного компонента. Всегда проверять наличие именно компонента:
```csharp
// НЕВЕРНО — выйдет раньше, не добавив компонент к существующему объекту:
if (FindExisting("Main Camera") != null) return;

// ВЕРНО — используем существующий объект, добавляем компонент если его нет:
var camGo = FindExisting("Main Camera") ?? new GameObject("Main Camera");
if (camGo.GetComponent<CameraController>() == null)
    camGo.AddComponent<CameraController>();
```

### Программное создание тегов
Теги в Unity должны существовать в TagManager до их назначения объекту.
Если префаб создаётся Editor-скриптом с кастомным тегом — добавить тег программно:
```csharp
var tagManager = new SerializedObject(
    AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
var tagsProp = tagManager.FindProperty("tags");
// проверить наличие и добавить при необходимости
tagManager.ApplyModifiedProperties();
```

### Автоматическое заполнение ссылок на Composition Root
LifetimeScope (Composition Root) содержит SerializeField-ссылки на конфиги и сцено-объекты.
Заполнять их вручную через Inspector ненадёжно и медленно. Автоматизировать через
`SerializedObject` + `FindProperty` + `FindAnyObjectByType` / `AssetDatabase.LoadAssetAtPath`:
```csharp
var so = new SerializedObject(scope);
so.FindProperty("_playerController").objectReferenceValue =
    Object.FindAnyObjectByType<PlayerController>();
so.FindProperty("_bulletConfig").objectReferenceValue =
    AssetDatabase.LoadAssetAtPath<BulletConfig>("Assets/Development/Data/Configs/BulletConfig.asset");
so.ApplyModifiedProperties();
```
Этот шаг должен быть последним в Wizard'е — после создания всех объектов и ассетов.

Два обязательных правила прошивки:
- **Вариантные конфиги выбирать по контексту, а не хардкодом.** Если у конфига есть
  варианты под сцену/режим/платформу (`Config_Default`, `Config_<Variant>`), скрипт
  обязан определить актуальный вариант (обычно по `SceneManager.GetActiveScene().name`
  против констант имён сцен проекта) и прошить именно его. Жёсткая прошивка
  `_Default`-ассета молча затирает вариантные настройки при прогоне меню в другой
  сцене — регрессия проявляется не сразу и далеко от места правки.
- **Не присваивать null**: если ассет/объект не найден — `Debug.LogWarning` и оставить
  существующее значение поля, иначе повторный прогон ломает рабочую проводку сцены.

### Пример структуры Editor-скрипта
```csharp
using UnityEditor;
using UnityEngine;

namespace Flatuot2_Like.Editor.Setup
{
    public static class SceneSetupEditor
    {
        [MenuItem("Flatuot2_Like/Setup/Build Complete Scene")]
        public static void BuildScene()
        {
            if (FindExisting("Player"))
            {
                if (!EditorUtility.DisplayDialog("Scene Setup",
                    "Scene already contains objects. Rebuild?", "Yes", "Cancel"))
                    return;
            }

            Undo.SetCurrentGroupName("Build Complete Scene");

            CreateArena();
            PlaceTower();
            CreatePlayer();
            CreateEnemy();
            SetupCamera();
            CreateGameLifetimeScope();

            Debug.Log("[Editor] Scene setup complete.");
        }

        private static GameObject FindExisting(string name) =>
            GameObject.Find(name);
    }
}
```
