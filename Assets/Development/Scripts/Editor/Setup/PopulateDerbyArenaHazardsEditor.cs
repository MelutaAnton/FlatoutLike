#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FlatOutLike.Editor.Setup
{
    /// <summary>
    /// Расставляет hazard-объекты в Derby_Arena_01.unity под родительским объектом "Hazards".
    ///
    /// Схема расстановки (арена 60×60, spawn-кольцо на radius=18 — hazard'ы ≤ radius=13):
    ///
    ///   Ядро (5 объектов, radius 4-6):
    ///     2× Hazard_Barrel         — по бокам от центра
    ///     1× Hazard_ConcreteBlock  — центр арены, повёрнут 45°
    ///     2× Hazard_Fence          — позади центра, перпендикулярно машинам
    ///
    ///   TireStack (3 шт., radius=8, шаг 120°):
    ///     3× Hazard_TireStack
    ///
    ///   Шины/колёса (8 шт., radius=10, шаг 45°):
    ///     4× Hazard_Tire + 4× Hazard_Wheel, чередование
    ///
    ///   Кактусы (8 шт., radius=13, шаг 45°, сдвиг 22.5° от шинного кольца):
    ///     Hazard_Cactus_01/02/03, по кругу
    ///
    /// Итого: 24 инстанса, детерминированные позиции — повторный запуск пересоздаёт
    /// расстановку (с диалогом подтверждения если "Hazards" уже есть).
    ///
    /// Запускать ПОСЛЕ:
    ///   1. FlatOutLike/Setup/Create Physics Layers
    ///   2. FlatOutLike/Scenes/Build Derby Arena
    ///   3. FlatOutLike/Setup/Build Hazard Prefabs
    ///   4. FlatOutLike/Configs/Create Default DestructibleConfig
    /// </summary>
    public static class PopulateDerbyArenaHazardsEditor
    {
        // Путь к сцене арены
        private const string ScenePath = "Assets/Development/Scenes/Derby_Arena_01.unity";

        // Имя родительского объекта в иерархии
        private const string HazardsParentName = "Hazards";

        // Пути наших hazard-префабов
        private const string EnvPath   = "Assets/Development/Prefabs/Environment/";
        private const string BarrelPath        = EnvPath + "Hazard_Barrel.prefab";
        private const string ConcreteBlockPath = EnvPath + "Hazard_ConcreteBlock.prefab";
        private const string FencePath         = EnvPath + "Hazard_Fence.prefab";
        private const string TirePath          = EnvPath + "Hazard_Tire.prefab";
        private const string WheelPath         = EnvPath + "Hazard_Wheel.prefab";
        private const string TireStackPath     = EnvPath + "Hazard_TireStack.prefab";
        private const string Cactus01Path      = EnvPath + "Hazard_Cactus_01.prefab";
        private const string Cactus02Path      = EnvPath + "Hazard_Cactus_02.prefab";
        private const string Cactus03Path      = EnvPath + "Hazard_Cactus_03.prefab";

        [MenuItem("FlatOutLike/Setup/Populate Derby Arena Hazards")]
        public static void PopulateHazards()
        {
            // ── 1. Убеждаемся что открыта Derby_Arena_01 ─────────────────────
            if (!EnsureDerbyArenaOpen()) return;

            Undo.SetCurrentGroupName("Populate Derby Arena Hazards");
            int undoGroup = Undo.GetCurrentGroup();

            // ── 2. Создаём или пересоздаём родительский объект Hazards ────────
            var parent = GetOrRebuildHazardsParent();
            if (parent == null) return; // пользователь отменил

            // ── 3. Расставляем hazard'ы ────────────────────────────────────────
            int placed = 0;

            // — Ядро: 2 бочки + 1 бетонный блок + 2 забора (radius 4-6)
            placed += PlaceHazard(parent, BarrelPath,
                new Vector3(5f, 0f, 0f),  Quaternion.identity);
            placed += PlaceHazard(parent, BarrelPath,
                new Vector3(-5f, 0f, 0f), Quaternion.identity);
            placed += PlaceHazard(parent, ConcreteBlockPath,
                new Vector3(0f, 0f, 5f),  Quaternion.Euler(0f, 45f, 0f));
            placed += PlaceHazard(parent, FencePath,
                new Vector3(4f, 0f, -5f), Quaternion.Euler(0f, 90f, 0f));
            placed += PlaceHazard(parent, FencePath,
                new Vector3(-4f, 0f, -5f), Quaternion.Euler(0f, 90f, 0f));

            // — TireStack: 3 штуки по кольцу radius=8, шаг 120°
            for (int i = 0; i < 3; i++)
            {
                float angle = i * 120f * Mathf.Deg2Rad;
                var   pos   = new Vector3(Mathf.Sin(angle) * 8f, 0f, Mathf.Cos(angle) * 8f);
                placed += PlaceHazard(parent, TireStackPath,
                    pos, Quaternion.Euler(0f, i * 60f, 0f));
            }

            // — Шины и колёса: 8 штук по кольцу radius=10, чередование Tire/Wheel
            string[] tireMix =
            {
                TirePath, WheelPath, TirePath, WheelPath,
                TirePath, WheelPath, TirePath, WheelPath,
            };
            for (int i = 0; i < tireMix.Length; i++)
            {
                float angle = i * (360f / tireMix.Length) * Mathf.Deg2Rad;
                var   pos   = new Vector3(Mathf.Sin(angle) * 10f, 0f, Mathf.Cos(angle) * 10f);
                placed += PlaceHazard(parent, tireMix[i],
                    pos, Quaternion.Euler(0f, i * 45f, 0f));
            }

            // — Кактусы: 8 штук по кольцу radius=13 (22.5° сдвиг от шинного кольца)
            string[] cactusMix =
            {
                Cactus01Path, Cactus02Path, Cactus03Path, Cactus01Path,
                Cactus02Path, Cactus03Path, Cactus01Path, Cactus02Path,
            };
            for (int i = 0; i < cactusMix.Length; i++)
            {
                float angle = (i * (360f / cactusMix.Length) + 22.5f) * Mathf.Deg2Rad;
                var   pos   = new Vector3(Mathf.Sin(angle) * 13f, 0f, Mathf.Cos(angle) * 13f);
                placed += PlaceHazard(parent, cactusMix[i],
                    pos, Quaternion.Euler(0f, i * 45f, 0f));
            }

            // ── 4. Сохраняем сцену ─────────────────────────────────────────────
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());

            Undo.CollapseUndoOperations(undoGroup);

            Debug.Log("[Editor] Populate Derby Arena Hazards завершён: " + placed +
                      " объектов расставлено под 'Hazards' (радиусы 4-13, spawn-ring=18).");
        }

        // ─────────────────────────────────────────────────────────────────────
        // Вспомогательные методы
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Проверяет что активная сцена — Derby_Arena_01.
        /// Если нет — предлагает открыть (сохраняя текущую если надо).
        /// Возвращает false если не удалось открыть.
        /// </summary>
        private static bool EnsureDerbyArenaOpen()
        {
            var active = SceneManager.GetActiveScene();
            if (active.path == ScenePath)
                return true;

            bool save = EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
            if (!save)
            {
                Debug.LogWarning("[Editor] Populate Derby Arena Hazards: операция отменена пользователем.");
                return false;
            }

            bool exists = System.IO.File.Exists(
                System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), ScenePath));
            if (!exists)
            {
                Debug.LogWarning("[Editor] Derby_Arena_01.unity не найдена по пути: " + ScenePath +
                                 ". Запусти 'FlatOutLike/Scenes/Build Derby Arena' сначала.");
                return false;
            }

            EditorSceneManager.OpenScene(ScenePath);
            Debug.Log("[Editor] Открыта сцена: " + ScenePath);
            return true;
        }

        /// <summary>
        /// Находит существующий объект "Hazards" в сцене.
        /// Если он уже содержит дочерние объекты — спрашивает разрешения на пересоздание:
        ///   «Да» — удаляет всех детей и возвращает очищенный объект.
        ///   «Отмена» — возвращает null (вызывающий должен прекратить работу).
        /// Если объект отсутствует — создаёт новый.
        /// </summary>
        private static GameObject GetOrRebuildHazardsParent()
        {
            var existing = GameObject.Find(HazardsParentName);

            if (existing != null)
            {
                if (existing.transform.childCount > 0)
                {
                    bool rebuild = EditorUtility.DisplayDialog(
                        "Populate Derby Arena Hazards",
                        "Объект '" + HazardsParentName + "' уже содержит " +
                        existing.transform.childCount + " дочерних объектов. Пересоздать расстановку?",
                        "Пересоздать", "Отмена");

                    if (!rebuild)
                    {
                        Debug.Log("[Editor] Populate Derby Arena Hazards: отменено пользователем.");
                        return null;
                    }

                    // Удаляем всех детей через Undo
                    while (existing.transform.childCount > 0)
                        Undo.DestroyObjectImmediate(existing.transform.GetChild(0).gameObject);

                    Debug.Log("[Editor] Дочерние объекты '" + HazardsParentName + "' удалены — пересоздаём.");
                }
                else
                {
                    Debug.Log("[Editor] Объект '" + HazardsParentName + "' найден (пустой) — используем.");
                }

                return existing;
            }

            // Создаём новый родительский объект
            var parent = new GameObject(HazardsParentName);
            Undo.RegisterCreatedObjectUndo(parent, "Create Hazards Parent");
            parent.transform.position = Vector3.zero;
            Debug.Log("[Editor] Создан объект '" + HazardsParentName + "'.");
            return parent;
        }

        /// <summary>
        /// Загружает prefab по пути и инстанциирует его под parent с указанными pos/rot.
        /// Логирует предупреждение если prefab не найден (не блокирует остальные объекты).
        /// Возвращает 1 если объект размещён, 0 — если prefab не найден.
        /// </summary>
        private static int PlaceHazard(GameObject parent, string prefabPath, Vector3 pos, Quaternion rot)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogWarning("[Editor] Hazard-префаб не найден: " + prefabPath +
                                 " — пропускаем. Запусти 'FlatOutLike/Setup/Build Hazard Prefabs' сначала.");
                return 0;
            }

            // InstantiatePrefab сохраняет prefab-link (nested prefab в иерархии)
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.transform);
            Undo.RegisterCreatedObjectUndo(instance, "Place " + prefab.name);

            instance.transform.localPosition = pos;
            instance.transform.localRotation = rot;

            Debug.Log("[Editor] Размещён " + prefab.name + " pos=" + pos);
            return 1;
        }
    }
}
#endif
