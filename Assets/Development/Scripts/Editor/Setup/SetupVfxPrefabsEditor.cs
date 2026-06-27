#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using FlatOutLike.Controllers.Vfx;

namespace FlatOutLike.Editor.Setup
{
    /// <summary>
    /// Выделите один или несколько prefab'ов partice-эффектов в окне Project и запустите
    /// этот пункт меню. Для каждого префаба скрипт:
    ///   1. Установит ParticleSystem.main.stopAction = Callback (нужно для возврата в Pool).
    ///   2. Установит ParticleSystem.main.playOnAwake = false (эффекты управляются Pool'ом,
    ///      активируются через SetActive перед Launch — playOnAwake срабатывает на старой pooled-позиции).
    ///   3. Добавит компонент ParticleController, если его нет.
    ///   4. Привяжет ParticleController._particleSystem к ParticleSystem на корне.
    /// Операция идемпотентна — повторный запуск на уже настроенных префабах безопасен.
    /// </summary>
    public static class SetupVfxPrefabsEditor
    {
        [MenuItem("FlatOutLike/Setup/Setup VFX Prefabs")]
        public static void SetupVfxPrefabs()
        {
            Undo.SetCurrentGroupName("Setup VFX Prefabs");
            int undoGroup = Undo.GetCurrentGroup();

            var selected = Selection.gameObjects;
            if (selected == null || selected.Length == 0)
            {
                EditorUtility.DisplayDialog("Setup VFX Prefabs",
                    "No objects selected.\n\n" +
                    "Select one or more particle prefab assets in the Project window, " +
                    "then run FlatOutLike/Setup/Setup VFX Prefabs.",
                    "OK");
                return;
            }

            int processed = 0;

            foreach (var go in selected)
            {
                string assetPath = AssetDatabase.GetAssetPath(go);

                // ── Фильтр: только prefab-ассеты ──────────────────────────────
                if (string.IsNullOrEmpty(assetPath) ||
                    !assetPath.EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase))
                {
                    Debug.LogWarning("[Editor] Skipping '" + go.name + "' — not a prefab asset.");
                    continue;
                }

                if (go.GetComponent<ParticleSystem>() == null)
                {
                    Debug.LogWarning("[Editor] Skipping '" + go.name +
                                     "' — no ParticleSystem on prefab root.");
                    continue;
                }

                // ── Открываем содержимое префаба для редактирования ───────────
                var root = PrefabUtility.LoadPrefabContents(assetPath);
                bool changed = false;

                try
                {
                    var ps = root.GetComponent<ParticleSystem>();
                    if (ps == null)
                    {
                        Debug.LogWarning("[Editor] Skipping '" + go.name +
                                         "' — ParticleSystem not found after loading prefab contents.");
                        continue;
                    }

                    // ── 1. Настраиваем MainModule ─────────────────────────────
                    var main = ps.main;

                    // stopAction = Callback: нужно, чтобы ParticleController.OnParticleSystemStopped()
                    // срабатывал и Pool получал callback возврата.
                    if (main.stopAction != ParticleSystemStopAction.Callback)
                    {
                        main.stopAction = ParticleSystemStopAction.Callback;
                        changed = true;
                        Debug.Log("[Editor] '" + go.name + "' — set stopAction = Callback.");
                    }
                    else
                    {
                        Debug.Log("[Editor] '" + go.name + "' — stopAction already Callback, skipping.");
                    }

                    // playOnAwake = false: эффекты управляются Pool'ом и активируются через SetActive(true)
                    // перед Launch(). С включённым playOnAwake система запускается на старой pooled-позиции
                    // за кадр до перепозиционирования и может преждевременно дёрнуть stop-callback.
                    if (main.playOnAwake)
                    {
                        main.playOnAwake = false;
                        changed = true;
                        Debug.Log("[Editor] '" + go.name + "' — set playOnAwake = false.");
                    }
                    else
                    {
                        Debug.Log("[Editor] '" + go.name + "' — playOnAwake already false, skipping.");
                    }

                    // ── 2. Добавляем ParticleController, если его нет ─────────
                    var pc = root.GetComponent<ParticleController>();
                    if (pc == null)
                    {
                        pc = root.AddComponent<ParticleController>();
                        changed = true;
                        Debug.Log("[Editor] '" + go.name + "' — added ParticleController.");
                    }
                    else
                    {
                        Debug.Log("[Editor] '" + go.name + "' — ParticleController already present.");
                    }

                    // ── 3. Привязываем _particleSystem ────────────────────────
                    var sopc = new SerializedObject(pc);
                    var psProp = sopc.FindProperty("_particleSystem");
                    if (psProp.objectReferenceValue == null)
                    {
                        psProp.objectReferenceValue = ps;
                        sopc.ApplyModifiedProperties();
                        changed = true;
                        Debug.Log("[Editor] '" + go.name + "' — wired ParticleController._particleSystem.");
                    }
                    else
                    {
                        Debug.Log("[Editor] '" + go.name + "' — ParticleController._particleSystem already wired.");
                    }

                    // ── Сохраняем, если что-то изменилось ─────────────────────
                    if (changed)
                    {
                        PrefabUtility.SaveAsPrefabAsset(root, assetPath);
                        processed++;
                        Debug.Log("[Editor] Saved prefab '" + assetPath + "'.");
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Undo.CollapseUndoOperations(undoGroup);

            Debug.Log("[Editor] VFX prefab setup complete. Prefabs updated: " + processed + ".");
        }
    }
}
#endif
