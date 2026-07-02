#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using FlatOutLike.Controllers.Car;
using FlatOutLike.Controllers.Driver;

namespace FlatOutLike.Editor.Setup
{
    /// <summary>
    /// Добавляет ragdoll-водителя (Driver_Default.prefab) в Car_Sport_09.prefab и
    /// Car_Enemy.prefab: создаёт (или переиспользует) child "DriverSeat", вкладывает
    /// туда prefab-instance водителя и заполняет CarController._driver.
    ///
    /// Позиция DriverSeat — заглушка (см. DriverSeatLocalPos), точную посадку нужно
    /// подогнать вручную под конкретную модель машины.
    ///
    /// Идемпотентность: повторный запуск не дублирует ни DriverSeat, ни водителя —
    /// переиспользует существующие объекты и только обновляет ссылку _driver.
    ///
    /// Запускать ПОСЛЕ:
    ///   FlatOutLike/Setup/Build Driver Ragdoll Prefab
    /// </summary>
    public static class AttachDriverToCarPrefabsEditor
    {
        private const string DriverSeatName   = "DriverSeat";
        private static readonly Vector3 DriverSeatLocalPos = new Vector3(0f, 0.6f, 0.1f);

        private const string DriverPrefabPath = "Assets/Development/Prefabs/Characters/Driver_Default.prefab";

        private static readonly string[] CarPrefabPaths =
        {
            "Assets/Development/Prefabs/Vehicles/Car_Sport_09.prefab",
            "Assets/Development/Prefabs/Vehicles/Car_Enemy.prefab",
        };

        [MenuItem("FlatOutLike/Setup/Attach Driver To Car Prefabs")]
        public static void AttachDriverToCarPrefabs()
        {
            Undo.SetCurrentGroupName("Attach Driver To Car Prefabs");

            var driverPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DriverPrefabPath);
            if (driverPrefab == null)
            {
                Debug.LogWarning("[Editor] Driver_Default.prefab не найден по " + DriverPrefabPath +
                                  ". Запусти 'FlatOutLike/Setup/Build Driver Ragdoll Prefab' сначала.");
                return;
            }

            int processed = 0;
            foreach (var carPath in CarPrefabPaths)
            {
                if (AttachToCarPrefab(carPath, driverPrefab))
                    processed++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[Editor] Attach Driver To Car Prefabs завершён. Обработано префабов: " + processed + ".");
        }

        // ─────────────────────────────────────────────────────────────────────
        private static bool AttachToCarPrefab(string carPrefabPath, GameObject driverPrefab)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(carPrefabPath) == null)
            {
                Debug.LogWarning("[Editor] Пропускаем: префаб не найден по " + carPrefabPath);
                return false;
            }

            var root = PrefabUtility.LoadPrefabContents(carPrefabPath);
            if (root == null)
            {
                Debug.LogError("[Editor] Не удалось загрузить содержимое " + carPrefabPath);
                return false;
            }

            try
            {
                // ── 1. DriverSeat: находим по имени, создаём при отсутствии ────
                var seatTransform = FindChildByName(root.transform, DriverSeatName);
                GameObject seatGo;
                if (seatTransform == null)
                {
                    seatGo = new GameObject(DriverSeatName);
                    seatGo.transform.SetParent(root.transform, false);
                    seatGo.transform.localPosition = DriverSeatLocalPos;
                    seatGo.transform.localRotation = Quaternion.identity;
                    Debug.Log("[Editor] '" + root.name + "' — создан DriverSeat в " + DriverSeatLocalPos + ".");
                }
                else
                {
                    seatGo = seatTransform.gameObject;
                    Debug.Log("[Editor] '" + root.name + "' — DriverSeat уже существует, переиспользуем.");
                }

                // ── 2. Вкладываем prefab-instance водителя, если его ещё нет ───
                DriverController driverController = seatGo.GetComponentInChildren<DriverController>(true);
                if (driverController == null)
                {
                    var driverInstance = (GameObject)PrefabUtility.InstantiatePrefab(driverPrefab);
                    driverInstance.transform.SetParent(seatGo.transform, false);
                    driverInstance.transform.localPosition = Vector3.zero;
                    driverInstance.transform.localRotation = Quaternion.identity;
                    driverController = driverInstance.GetComponent<DriverController>();
                    Debug.Log("[Editor] '" + root.name + "' — вложен Driver_Default под DriverSeat.");
                }
                else
                {
                    Debug.Log("[Editor] '" + root.name + "' — водитель уже вложен под DriverSeat, обновляем ссылку.");
                }

                // ── 3. Заполняем CarController._driver ─────────────────────────
                var carController = root.GetComponent<CarController>();
                if (carController == null)
                {
                    Debug.LogWarning("[Editor] '" + root.name + "' — CarController не найден на корне. " +
                                     "Ссылка _driver не заполнена.");
                }
                else
                {
                    var so = new SerializedObject(carController);
                    var driverProp = so.FindProperty("_driver");
                    if (driverProp == null)
                    {
                        Debug.LogWarning("[Editor] '" + root.name + "' — поле CarController._driver не найдено. " +
                                         "Возможно, рантайм Step 6 ещё не скомпилировался — привязка пропущена.");
                    }
                    else
                    {
                        driverProp.objectReferenceValue = driverController;
                        so.ApplyModifiedProperties();
                        Debug.Log("[Editor] '" + root.name + "' — CarController._driver = " +
                                  (driverController != null ? driverController.name : "NULL") + ".");
                    }
                }

                PrefabUtility.SaveAsPrefabAsset(root, carPrefabPath);
                Debug.Log("[Editor] Сохранён prefab: " + carPrefabPath);
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static Transform FindChildByName(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t != root && t.name == name)
                    return t;
            return null;
        }
    }
}
#endif
