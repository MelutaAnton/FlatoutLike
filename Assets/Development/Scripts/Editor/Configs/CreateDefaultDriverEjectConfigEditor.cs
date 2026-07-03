#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using FlatOutLike.Data.Configs.Driver;

namespace FlatOutLike.Editor.Configs
{
    /// <summary>
    /// Создаёт (или обновляет) per-режимные ассеты DriverEjectConfig в
    /// Assets/Development/Data/Configs/Driver/:
    ///   Default  (Derby):     _forwardImpulse=8,  _upImpulse=6,  _carVelocityInheritance=0.6
    ///   HighJump (трамплин):  _forwardImpulse=10, _upImpulse=10, _carVelocityInheritance=0.9
    /// Режимы разделены на уровне данных: GameLifetimeScope._driverEjectConfig — per-scene
    /// поле, каждая сцена ссылается на свой ассет; код общий.
    ///
    /// Идемпотентность: если ассет уже существует — поля перезаписываются, GUID сохраняется.
    /// </summary>
    public static class CreateDefaultDriverEjectConfigEditor
    {
        private const string DefaultAssetPath  = "Assets/Development/Data/Configs/Driver/DriverEjectConfig_Default.asset";
        private const string HighJumpAssetPath = "Assets/Development/Data/Configs/Driver/DriverEjectConfig_HighJump.asset";
        private const string FolderData  = "Assets/Development/Data";
        private const string FolderCfg   = "Assets/Development/Data/Configs";

        [MenuItem("FlatOutLike/Configs/Create Default DriverEjectConfig")]
        public static void CreateDefaultDriverEjectConfig()
            => CreateOrUpdate(DefaultAssetPath, forward: 8f, up: 6f, inheritance: 0.6f);

        [MenuItem("FlatOutLike/Configs/Create HighJump DriverEjectConfig")]
        public static void CreateHighJumpDriverEjectConfig()
            => CreateOrUpdate(HighJumpAssetPath, forward: 10f, up: 10f, inheritance: 0.9f);

        private static void CreateOrUpdate(string assetPath, float forward, float up, float inheritance)
        {
            Undo.SetCurrentGroupName("Create DriverEjectConfig");

            // ── 1. Папки ─────────────────────────────────────────────────────
            EnsureFolder("Assets/Development", "Data");
            EnsureFolder(FolderData, "Configs");
            EnsureFolder(FolderCfg,  "Driver");

            // ── 2. Создаём или загружаем ассет (идемпотентно по пути) ─────────
            var config = AssetDatabase.LoadAssetAtPath<DriverEjectConfig>(assetPath);
            bool isNew = config == null;

            if (isNew)
            {
                config = ScriptableObject.CreateInstance<DriverEjectConfig>();
                AssetDatabase.CreateAsset(config, assetPath);
                AssetDatabase.SaveAssets();
                Undo.RegisterCreatedObjectUndo(config, "Create DriverEjectConfig");
                Debug.Log("[Editor] Создан " + assetPath);
            }
            else
            {
                Debug.Log("[Editor] " + assetPath + " уже существует — обновляем поля.");
            }

            // ── 3. Заполняем поля через SerializedObject ──────────────────────
            var so = new SerializedObject(config);
            so.FindProperty("_forwardImpulse").floatValue          = forward;
            so.FindProperty("_upImpulse").floatValue               = up;
            so.FindProperty("_carVelocityInheritance").floatValue  = inheritance;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();

            Selection.activeObject = config;
            EditorGUIUtility.PingObject(config);

            Debug.Log($"[Editor] DriverEjectConfig заполнен: _forwardImpulse={forward}, _upImpulse={up}, _carVelocityInheritance={inheritance}.");
        }

        // ─────────────────────────────────────────────────────────────────────
        private static void EnsureFolder(string parent, string folderName)
        {
            string full = parent + "/" + folderName;
            if (!AssetDatabase.IsValidFolder(full))
            {
                AssetDatabase.CreateFolder(parent, folderName);
                Debug.Log("[Editor] Создана папка: " + full);
            }
        }
    }
}
#endif
