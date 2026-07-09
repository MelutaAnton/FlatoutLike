#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using FlatOutLike.Controllers.Car;

namespace FlatOutLike.Editor.Tools
{
    /// <summary>
    /// Общий helper для editor-скриптов, обрабатывающих ВСЕ префабы машин проекта
    /// (AttachDriverToCarPrefabsEditor, SetupWindshieldShatterEditor и т.п.).
    ///
    /// Заменяет захардкоженные списки путей (Car_Sport_09, Car_Enemy, ...) динамическим
    /// обнаружением: любой .prefab в Assets/Development/Prefabs/Vehicles/ с CarController
    /// НА КОРНЕ считается машиной. Новые машины (Car_Tank_17, Car_Sport_05 и т.д.) подхватываются
    /// автоматически без правки кода — единственный источник правды о списке машин.
    ///
    /// Не содержит [MenuItem] — это переиспользуемая утилита, а не самостоятельное действие.
    /// </summary>
    public static class CarPrefabDiscovery
    {
        public const string VehiclesRoot = "Assets/Development/Prefabs/Vehicles";

        /// <summary>
        /// Возвращает пути ко всем prefab-ассетам в <see cref="VehiclesRoot"/>, у которых
        /// CarController навешан на корневой GameObject. Результат отсортирован по пути
        /// для детерминированного порядка обработки.
        /// </summary>
        public static string[] DiscoverCarPrefabPaths()
        {
            var result = new List<string>();

            if (!AssetDatabase.IsValidFolder(VehiclesRoot))
            {
                Debug.LogWarning("[Editor] CarPrefabDiscovery: папка " + VehiclesRoot + " не найдена.");
                return Array.Empty<string>();
            }

            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { VehiclesRoot });
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null) continue;

                // Именно на корне — вложенные машины (если такие появятся) не подходят под контракт.
                if (go.GetComponent<CarController>() == null) continue;

                result.Add(path);
            }

            result.Sort(StringComparer.Ordinal);
            return result.ToArray();
        }
    }
}
#endif
