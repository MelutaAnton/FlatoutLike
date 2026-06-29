#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace FlatOutLike.Editor.Setup
{
    /// <summary>
    /// Утилита: для каждого выделенного GameObject с Collider(isTrigger=true)
    /// добавляет кинематический Rigidbody (isKinematic=true, useGravity=false),
    /// если его ещё нет.
    ///
    /// ЗАЧЕМ: OnTriggerEnter / OnTriggerExit НЕ СРАБАТЫВАЮТ без Rigidbody ни
    /// на одном из участников коллизии (CLAUDE.md "Триггеры требуют Rigidbody — КРИТИЧНО").
    /// Кинематический Rigidbody не участвует в физическом расчёте (isKinematic=true),
    /// но активирует trigger-события — именно то, что нужно для зон пикапов и любых
    /// стационарных триггеров в проекте.
    ///
    /// Применение:
    ///   1. Выдели один или несколько GameObject с trigger-коллайдерами в Hierarchy.
    ///   2. Запусти FlatOutLike/Setup/Add Kinematic Rigidbody To Selected Triggers.
    ///   3. Готово — Rigidbody добавлен, Undo работает.
    ///
    /// Идемпотентность: если Rigidbody уже есть — объект пропускается без изменений.
    /// Если у объекта нет trigger-коллайдера — пропускается с LogWarning.
    /// </summary>
    public static class AddKinematicRigidbodyToTriggersEditor
    {
        [MenuItem("FlatOutLike/Setup/Add Kinematic Rigidbody To Selected Triggers")]
        public static void AddKinematicRigidbodyToTriggers()
        {
            var selected = Selection.gameObjects;
            if (selected == null || selected.Length == 0)
            {
                Debug.LogWarning("[Editor] Нет выделенных объектов. " +
                                 "Выдели GameObject(s) с trigger-коллайдерами в Hierarchy и повтори.");
                return;
            }

            Undo.SetCurrentGroupName("Add Kinematic Rigidbody To Triggers");
            int undoGroup = Undo.GetCurrentGroup();

            int added     = 0;
            int alreadyHas = 0;
            int noTrigger = 0;

            foreach (var go in selected)
            {
                if (go == null) continue;

                // ── Проверяем: есть ли на объекте trigger-коллайдер? ─────────
                bool hasTrigger = false;
                foreach (var col in go.GetComponents<Collider>())
                {
                    if (col.isTrigger)
                    {
                        hasTrigger = true;
                        break;
                    }
                }

                if (!hasTrigger)
                {
                    Debug.LogWarning("[Editor] '" + go.name + "' не содержит trigger-коллайдер — пропускаем. " +
                                     "Убедись, что у коллайдера стоит галка 'Is Trigger'.");
                    noTrigger++;
                    continue;
                }

                // ── Идемпотентность: Rigidbody уже есть? ─────────────────────
                var rb = go.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    Debug.Log("[Editor] '" + go.name + "' уже имеет Rigidbody " +
                              "(isKinematic=" + rb.isKinematic + ") — пропускаем.");
                    alreadyHas++;
                    continue;
                }

                // ── Добавляем кинематический Rigidbody через Undo.AddComponent ─
                rb = Undo.AddComponent<Rigidbody>(go);
                rb.isKinematic = true;
                rb.useGravity  = false;

                Debug.Log("[Editor] Добавлен Rigidbody(isKinematic=true, useGravity=false) на '" + go.name + "'.");
                added++;
            }

            Undo.CollapseUndoOperations(undoGroup);

            Debug.Log("[Editor] Add Kinematic Rigidbody To Selected Triggers завершён: " +
                      "добавлено=" + added +
                      ", уже было=" + alreadyHas +
                      ", без триггера=" + noTrigger + ".");
        }

        /// <summary>
        /// Валидатор MenuItem: пункт меню активен только при наличии выделения.
        /// </summary>
        [MenuItem("FlatOutLike/Setup/Add Kinematic Rigidbody To Selected Triggers", true)]
        private static bool ValidateSelection()
        {
            return Selection.gameObjects != null && Selection.gameObjects.Length > 0;
        }
    }
}
#endif
