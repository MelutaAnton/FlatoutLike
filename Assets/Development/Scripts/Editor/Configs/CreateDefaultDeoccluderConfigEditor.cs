#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using FlatOutLike.Data.Configs.Camera;
using FlatOutLike.Models;

namespace FlatOutLike.Editor.Configs
{
    /// <summary>
    /// Создаёт дефолтный ассет DeoccluderConfig (SO-конфиг для CinemachineDeoccluder на
    /// chase/orbit vcam). Значения полей повторяют запечённые дефолты, которые раньше
    /// выставлял EnsureDeoccluder() напрямую в SetupCinemachineCameraRigEditor — теперь
    /// они читаются рантайм-адаптерами из конфига, а не хардкодятся в editor-скрипте.
    ///
    /// Идемпотентность: ассет уже существует → лог и выход, существующий тюнинг
    /// не перезатирается (в отличие от AISensorConfig-скрипта, здесь НЕТ upsert-синхронизации,
    /// т.к. предполагается ручная донастройка деоклюдера дизайнером после первого запуска).
    /// </summary>
    public static class CreateDefaultDeoccluderConfigEditor
    {
        private const string AssetPath  = "Assets/Development/Data/Configs/Camera/DeoccluderConfig_Default.asset";
        private const string FolderData = "Assets/Development/Data";
        private const string FolderConfigsRoot = "Assets/Development/Data/Configs";
        private const string FolderConfigsCamera = "Assets/Development/Data/Configs/Camera";

        [MenuItem("FlatOutLike/Configs/Create Default DeoccluderConfig")]
        public static void CreateDefaultDeoccluderConfig()
        {
            var existing = AssetDatabase.LoadAssetAtPath<DeoccluderConfig>(AssetPath);
            if (existing != null)
            {
                Debug.Log("[Editor] DeoccluderConfig_Default уже существует по " + AssetPath +
                          " — пропускаем создание, чтобы не затереть тюнинг.");
                Selection.activeObject = existing;
                EditorGUIUtility.PingObject(existing);
                return;
            }

            EnsureFolder("Assets/Development", "Data");
            EnsureFolder(FolderData, "Configs");
            EnsureFolder(FolderConfigsRoot, "Camera");

            var config = ScriptableObject.CreateInstance<DeoccluderConfig>();
            var so = new SerializedObject(config);

            // CollideAgainst: Default всегда есть, Wall/Hazard — только если Physics Layers уже созданы.
            var collideLayerNames = new List<string> { "Default" };
            if (LayerMask.NameToLayer(Layers.Wall) >= 0)
                collideLayerNames.Add(Layers.Wall);
            else
                Debug.LogWarning("[Editor] Слой '" + Layers.Wall + "' не найден — CollideAgainst без него. " +
                                  "Запусти 'FlatOutLike/Setup/Create Physics Layers' и перезапусти этот скрипт.");
            if (LayerMask.NameToLayer(Layers.Hazard) >= 0)
                collideLayerNames.Add(Layers.Hazard);
            else
                Debug.LogWarning("[Editor] Слой '" + Layers.Hazard + "' не найден — CollideAgainst без него. " +
                                  "Запусти 'FlatOutLike/Setup/Create Physics Layers' и перезапусти этот скрипт.");
            SetLayerMask(so, "_collideAgainst", LayerMask.GetMask(collideLayerNames.ToArray()));

            // TransparentLayers: машины и пикапы не должны толкать камеру.
            var transparentLayerNames = new List<string>();
            if (LayerMask.NameToLayer(Layers.Vehicle) >= 0)
                transparentLayerNames.Add(Layers.Vehicle);
            else
                Debug.LogWarning("[Editor] Слой '" + Layers.Vehicle + "' не найден — TransparentLayers без него.");
            if (LayerMask.NameToLayer(Layers.Pickup) >= 0)
                transparentLayerNames.Add(Layers.Pickup);
            else
                Debug.LogWarning("[Editor] Слой '" + Layers.Pickup + "' не найден — TransparentLayers без него.");
            // GetMask от пустого массива возвращает 0 — отдельная ветка не нужна.
            SetLayerMask(so, "_transparentLayers", LayerMask.GetMask(transparentLayerNames.ToArray()));

            SetFloat(so, "_minimumDistanceFromTarget", 1f);
            SetFloat(so, "_cameraRadius", 0.5f);
            SetFloat(so, "_damping", 0.4f);
            SetFloat(so, "_dampingWhenOccluded", 0.2f);
            SetFloat(so, "_smoothingTime", 0f);

            so.ApplyModifiedProperties();

            AssetDatabase.CreateAsset(config, AssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = config;
            EditorGUIUtility.PingObject(config);

            Debug.Log("[Editor] Created DeoccluderConfig at " + AssetPath +
                      " (CollideAgainst=[" + string.Join(",", collideLayerNames) + "], " +
                      "TransparentLayers=[" + string.Join(",", transparentLayerNames) + "]).");
        }

        // ─────────────────────────────────────────────────────────────────────

        private static void SetFloat(SerializedObject so, string fieldName, float value)
        {
            var prop = so.FindProperty(fieldName);
            if (prop != null)
                prop.floatValue = value;
            else
                Debug.LogWarning($"[Editor] Field {fieldName} not found on DeoccluderConfig, skipping");
        }

        private static void SetLayerMask(SerializedObject so, string fieldName, int maskValue)
        {
            var prop = so.FindProperty(fieldName);
            if (prop != null)
                prop.intValue = maskValue;
            else
                Debug.LogWarning($"[Editor] Field {fieldName} not found on DeoccluderConfig, skipping");
        }

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
