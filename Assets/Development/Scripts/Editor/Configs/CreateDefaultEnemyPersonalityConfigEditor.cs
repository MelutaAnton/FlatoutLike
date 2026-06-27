#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using FlatOutLike.Data.Configs.Enemies;

namespace FlatOutLike.Editor.Configs
{
    /// <summary>
    /// Creates the default Aggressor EnemyPersonalityConfig asset, or synchronises
    /// newly-added tuning fields if the asset already exists (preserving its GUID).
    ///
    /// Behaviour:
    ///   • Asset absent  → create fresh instance; C# defaults cover all fields.
    ///   • Asset present → re-assert defaults ONLY for the three newest tuning fields
    ///     (_stuckTime, _circleBreakTime, _graceTime). All other fields
    ///     (RamAggression, distances, etc.) are intentionally left untouched so that
    ///     any manual tuning done in Inspector is preserved.
    ///
    /// Idempotent: repeating this menu action is safe — same values are written each time,
    /// no duplicates, no GUID change.
    ///
    /// Null-safe: if a field is not found on either the asset or the defaults instance
    /// (e.g. after a schema change), a warning is logged and the field is skipped — no NRE.
    /// </summary>
    public static class CreateDefaultEnemyPersonalityConfigEditor
    {
        private const string AssetPath     = "Assets/Development/Data/Configs/Enemies/EnemyPersonalityConfig_Aggressor.asset";
        private const string FolderData    = "Assets/Development/Data";
        private const string FolderConfigs = "Assets/Development/Data/Configs";
        private const string FolderEnemies = "Assets/Development/Data/Configs/Enemies";

        [MenuItem("FlatOutLike/Configs/Create Default EnemyPersonalityConfig")]
        public static void CreateDefaultEnemyPersonalityConfig()
        {
            var existing = AssetDatabase.LoadAssetAtPath<EnemyPersonalityConfig>(AssetPath);

            if (existing != null)
            {
                SyncNewTuningFields(existing);
                return;
            }

            // ── Ensure folder hierarchy ───────────────────────────────────────
            EnsureFolder("Assets/Development", "Data");
            EnsureFolder(FolderData,           "Configs");
            EnsureFolder(FolderConfigs,        "Enemies");

            // ── Create asset ──────────────────────────────────────────────────
            // Class defaults already define the Aggressor profile; no extra setup required.
            var config = ScriptableObject.CreateInstance<EnemyPersonalityConfig>();
            AssetDatabase.CreateAsset(config, AssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Undo.RegisterCreatedObjectUndo(config, "Create EnemyPersonalityConfig");

            Selection.activeObject = config;
            EditorGUIUtility.PingObject(config);

            Debug.Log("[Editor] Created EnemyPersonalityConfig (Aggressor defaults) at " + AssetPath);
        }

        /// <summary>
        /// Writes C# class defaults into the tuning fields that were added after the
        /// asset was first created. Values come from a fresh temporary instance so that
        /// magic numbers are not duplicated here — the class is the single source of truth.
        /// Each field is guarded: if FindProperty returns null (schema change), the field
        /// is skipped with a warning instead of throwing NullReferenceException.
        /// </summary>
        private static void SyncNewTuningFields(EnemyPersonalityConfig asset)
        {
            // Fields to sync — update this list when new tuning fields are added to the class.
            var fieldNames = new[] { "_stuckTime", "_circleBreakTime", "_graceTime" };

            // Spin up a temporary instance to read the C# constructor defaults.
            var defaults   = ScriptableObject.CreateInstance<EnemyPersonalityConfig>();
            var soDefaults = new SerializedObject(defaults);

            // Read default values with null-guard on the defaults side.
            var defaultValues = new System.Collections.Generic.Dictionary<string, float>();
            foreach (var fieldName in fieldNames)
            {
                var propDefault = soDefaults.FindProperty(fieldName);
                if (propDefault == null)
                {
                    Debug.LogWarning($"[Editor] Field {fieldName} not found on defaults instance, skipping");
                    continue;
                }
                defaultValues[fieldName] = propDefault.floatValue;
            }

            Object.DestroyImmediate(defaults);

            // Apply to the existing asset — GUID is never touched.
            var soExisting = new SerializedObject(asset);
            foreach (var kvp in defaultValues)
            {
                var propExisting = soExisting.FindProperty(kvp.Key);
                if (propExisting == null)
                {
                    Debug.LogWarning($"[Editor] Field {kvp.Key} not found on existing asset, skipping");
                    continue;
                }
                propExisting.floatValue = kvp.Value;
            }
            soExisting.ApplyModifiedProperties();

            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();

            float stuckTime       = defaultValues.TryGetValue("_stuckTime",       out var s) ? s : float.NaN;
            float circleBreakTime = defaultValues.TryGetValue("_circleBreakTime", out var c) ? c : float.NaN;
            float graceTime       = defaultValues.TryGetValue("_graceTime",       out var g) ? g : float.NaN;

            Debug.Log($"[Editor] Synced tuning fields on existing EnemyPersonalityConfig_Aggressor: " +
                      $"_stuckTime={stuckTime}, " +
                      $"_circleBreakTime={circleBreakTime}, " +
                      $"_graceTime={graceTime}");
        }

        // ─────────────────────────────────────────────────────────────────────
        private static void EnsureFolder(string parent, string folderName)
        {
            string full = parent + "/" + folderName;
            if (!AssetDatabase.IsValidFolder(full))
            {
                AssetDatabase.CreateFolder(parent, folderName);
                Debug.Log("[Editor] Created folder " + full);
            }
        }
    }
}
#endif
