#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using FlatOutLike.Bridge;
using FlatOutLike.Controllers.Car;
using FlatOutLike.Data.Configs.Audio;

namespace FlatOutLike.Bridge.Editor
{
    /// <summary>
    /// ВАЖНО: этот файл лежит в Assets/Development/BridgeScripts/Editor/, а НЕ в
    /// обычной Assets/Development/Scripts/Editor/. Причина — граница asmdef.
    /// Скрипт работает с типом RealisticEngineSound (ThirdParty-пак "RealisticEngineSound",
    /// поставляется БЕЗ asmdef → компилируется в Assembly-CSharp) и с мостом
    /// CarEngineSoundBridge (тоже в Assembly-CSharp по той же причине — см.
    /// Assets/Development/BridgeScripts/CarEngineSoundBridge.cs). Обычный
    /// Assets/Development/Scripts/Editor/ лежит под FlatOutLike.Editor.asmdef, который
    /// НЕ видит Assembly-CSharp-типы. Папка Editor/ без .asmdef компилируется в
    /// Assembly-CSharp-Editor, который видит и пак, и мост, и все FlatOutLike-типы
    /// (FlatOutLike.asmdef подключается автоматически) — единственное место, откуда
    /// этот скрипт вообще может скомпилироваться.
    ///
    /// Встраивает звук двигателя (RealisticEngineSound) в 4 префаба машин:
    /// создаёт дочерний анкор "EngineSound" под корнем префаба, вкладывает туда
    /// nested-инстанс соответствующего RES-префаба двигателя, переключает
    /// gasPedalValueSetting на NotSimulated (рекомендация пака для джойстик/стик-
    /// управления, как у нас) и добавляет на корень CarEngineSoundBridge с прошитыми
    /// ссылками на CarController / RealisticEngineSound / EngineSoundConfig.
    ///
    /// Идемпотентность: проверяется наличие CarEngineSoundBridge с уже прошитым
    /// _engineSound — если есть, машина пропускается целиком.
    /// </summary>
    public static class SetupEngineSoundsEditor
    {
        private const string ConfigFolderData = "Assets/Development/Data";
        private const string ConfigFolderConfigsRoot = "Assets/Development/Data/Configs";
        private const string ConfigFolderAudio = "Assets/Development/Data/Configs/Audio";
        private const string ConfigAssetPath = "Assets/Development/Data/Configs/Audio/EngineSoundConfig_Default.asset";

        private const string AnchorName = "EngineSound";
        private static readonly Vector3 AnchorLocalPosition = new Vector3(0f, 0.5f, 0.8f);

        // Дословный маппинг: префаб машины → RES-префаб двигателя.
        private struct CarEngineMapping
        {
            public readonly string CarPrefabPath;
            public readonly string EnginePrefabPath;

            public CarEngineMapping(string carPrefabPath, string enginePrefabPath)
            {
                CarPrefabPath = carPrefabPath;
                EnginePrefabPath = enginePrefabPath;
            }
        }

        private static readonly CarEngineMapping[] Mappings =
        {
            new CarEngineMapping(
                "Assets/Development/Prefabs/Vehicles/Car_Sport_05.prefab",
                "Assets/ThirdParty/RealisticEngineSound/Assets/Prefabs/Engine_Prefabs/v8_italian.prefab"),
            new CarEngineMapping(
                "Assets/Development/Prefabs/Vehicles/Car_Sport_09.prefab",
                "Assets/ThirdParty/RealisticEngineSound/Assets/Prefabs/Engine_Prefabs/V8_american_modern.prefab"),
            new CarEngineMapping(
                "Assets/Development/Prefabs/Vehicles/Car_MT_02.prefab",
                "Assets/ThirdParty/RealisticEngineSound/Assets/Prefabs/Engine_Prefabs/Truck_old.prefab"),
            new CarEngineMapping(
                "Assets/Development/Prefabs/Vehicles/Car_Enemy.prefab",
                "Assets/ThirdParty/RealisticEngineSound/Assets/Prefabs/Engine_Prefabs/i6_german.prefab"),
        };

        [MenuItem("FlatOutLike/Setup/Setup Engine Sounds")]
        public static void SetupEngineSounds()
        {
            var config = EnsureEngineSoundConfig();

            int processed = 0;
            int changedCount = 0;
            int skipped = 0;

            foreach (var mapping in Mappings)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(mapping.CarPrefabPath) == null)
                {
                    Debug.LogWarning("[Editor] Setup Engine Sounds: не найден префаб машины " +
                                      mapping.CarPrefabPath + " — пропускаю.");
                    continue;
                }

                processed++;
                bool changed = ProcessCarPrefab(mapping, config, out bool wasSkipped);

                if (wasSkipped)
                {
                    skipped++;
                    continue;
                }

                if (changed)
                {
                    changedCount++;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[Editor] Setup Engine Sounds завершён. Обработано машин: " + processed +
                       ", изменено: " + changedCount +
                       ", пропущено (уже настроены): " + skipped + ".");
        }

        /// <summary>Ensure-ассет EngineSoundConfig_Default (дефолты — в инициализаторах класса).</summary>
        private static EngineSoundConfig EnsureEngineSoundConfig()
        {
            var existing = AssetDatabase.LoadAssetAtPath<EngineSoundConfig>(ConfigAssetPath);
            if (existing != null)
            {
                Debug.Log("[Editor] EngineSoundConfig_Default уже существует по " + ConfigAssetPath +
                          " — пропускаем создание, чтобы не затереть тюнинг.");
                return existing;
            }

            EnsureFolder("Assets/Development", "Data");
            EnsureFolder(ConfigFolderData, "Configs");
            EnsureFolder(ConfigFolderConfigsRoot, "Audio");

            var config = ScriptableObject.CreateInstance<EngineSoundConfig>();
            AssetDatabase.CreateAsset(config, ConfigAssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[Editor] Created EngineSoundConfig at " + ConfigAssetPath + ".");
            return config;
        }

        /// <summary>Донастраивает один префаб машины. wasSkipped=true, если уже полностью настроен.</summary>
        private static bool ProcessCarPrefab(CarEngineMapping mapping, EngineSoundConfig config, out bool wasSkipped)
        {
            wasSkipped = false;
            bool changed = false;

            var enginePrefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(mapping.EnginePrefabPath);
            if (enginePrefabAsset == null)
            {
                Debug.LogError("[Editor] Setup Engine Sounds: не найден RES-префаб двигателя " +
                                mapping.EnginePrefabPath + " для " + mapping.CarPrefabPath + " — пропускаю.");
                wasSkipped = true;
                return false;
            }

            var root = PrefabUtility.LoadPrefabContents(mapping.CarPrefabPath);
            try
            {
                var car = root.GetComponent<CarController>();
                if (car == null)
                {
                    Debug.LogWarning("[Editor] Setup Engine Sounds: на корне " + mapping.CarPrefabPath +
                                      " нет CarController — пропускаю.");
                    wasSkipped = true;
                    return false;
                }

                // Идемпотентность по компоненту: уже настроенный бридж с прошитым _engineSound — пропуск.
                var existingBridge = root.GetComponent<CarEngineSoundBridge>();
                if (existingBridge != null)
                {
                    var existingSo = new SerializedObject(existingBridge);
                    var existingEngineProp = existingSo.FindProperty("_engineSound");
                    if (existingEngineProp != null && existingEngineProp.objectReferenceValue != null)
                    {
                        Debug.Log("[Editor] Setup Engine Sounds: " + mapping.CarPrefabPath +
                                  " уже настроен (CarEngineSoundBridge._engineSound прошит) — пропускаю.");
                        wasSkipped = true;
                        return false;
                    }
                }

                // ── 1. Анкор "EngineSound" под корнем ───────────────────────────
                var anchorTransform = root.transform.Find(AnchorName);
                GameObject anchor;
                if (anchorTransform == null)
                {
                    anchor = new GameObject(AnchorName);
                    anchor.transform.SetParent(root.transform, false);
                    anchor.transform.localPosition = AnchorLocalPosition;
                    changed = true;
                    Debug.Log("[Editor] Setup Engine Sounds: создан анкор '" + AnchorName + "' на " +
                              mapping.CarPrefabPath + ".");
                }
                else
                {
                    anchor = anchorTransform.gameObject;
                }

                // ── 2. Nested-инстанс RES-префаба двигателя ─────────────────────
                var engineSound = anchor.GetComponentInChildren<RealisticEngineSound>(true);
                if (engineSound == null)
                {
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(enginePrefabAsset, anchor.transform);
                    instance.transform.localPosition = Vector3.zero;
                    instance.transform.localRotation = Quaternion.identity;
                    engineSound = instance.GetComponentInChildren<RealisticEngineSound>(true);
                    changed = true;
                    Debug.Log("[Editor] Setup Engine Sounds: вложен nested-инстанс " +
                              mapping.EnginePrefabPath + " под '" + AnchorName + "' на " +
                              mapping.CarPrefabPath + ".");
                }

                if (engineSound == null)
                {
                    Debug.LogError("[Editor] Setup Engine Sounds: в " + mapping.EnginePrefabPath +
                                    " не найден компонент RealisticEngineSound — прошивка бриджа невозможна.");
                    // false: префаб не сохраняется (полуфабрикат отбрасывается UnloadPrefabContents),
                    // счётчик изменённых машин не инкрементируем.
                    return false;
                }

                // ── 3. Рекомендованный режим для стик-управления ────────────────
                if (engineSound.gasPedalValueSetting != RealisticEngineSound.GasPedalValue.NotSimulated)
                {
                    engineSound.gasPedalValueSetting = RealisticEngineSound.GasPedalValue.NotSimulated;
                    EditorUtility.SetDirty(engineSound);
                    changed = true;
                    Debug.Log("[Editor] Setup Engine Sounds: gasPedalValueSetting=NotSimulated на " +
                              mapping.CarPrefabPath + ".");
                }

                // ── 4. CarEngineSoundBridge на корне ─────────────────────────────
                var bridge = root.GetComponent<CarEngineSoundBridge>();
                if (bridge == null)
                {
                    bridge = root.AddComponent<CarEngineSoundBridge>();
                    changed = true;
                    Debug.Log("[Editor] Setup Engine Sounds: добавлен CarEngineSoundBridge на " +
                              mapping.CarPrefabPath + ".");
                }

                var so = new SerializedObject(bridge);

                var carProp = so.FindProperty("_car");
                if (carProp != null)
                {
                    if (carProp.objectReferenceValue != car)
                    {
                        carProp.objectReferenceValue = car;
                        changed = true;
                    }
                }
                else
                {
                    Debug.LogWarning("[Editor] Setup Engine Sounds: поле _car не найдено на CarEngineSoundBridge, пропускаю.");
                }

                var engineProp = so.FindProperty("_engineSound");
                if (engineProp != null)
                {
                    if (engineProp.objectReferenceValue != engineSound)
                    {
                        engineProp.objectReferenceValue = engineSound;
                        changed = true;
                    }
                }
                else
                {
                    Debug.LogWarning("[Editor] Setup Engine Sounds: поле _engineSound не найдено на CarEngineSoundBridge, пропускаю.");
                }

                var configProp = so.FindProperty("_config");
                if (configProp != null)
                {
                    // Правило "не присваивать null": если конфиг не найден — не трогаем поле.
                    if (config != null && configProp.objectReferenceValue != config)
                    {
                        configProp.objectReferenceValue = config;
                        changed = true;
                    }
                    else if (config == null)
                    {
                        Debug.LogWarning("[Editor] Setup Engine Sounds: EngineSoundConfig не найден — " +
                                          "поле _config на " + mapping.CarPrefabPath + " не тронуто.");
                    }
                }
                else
                {
                    Debug.LogWarning("[Editor] Setup Engine Sounds: поле _config не найдено на CarEngineSoundBridge, пропускаю.");
                }

                so.ApplyModifiedProperties();

                if (changed)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, mapping.CarPrefabPath);
                    Debug.Log("[Editor] Setup Engine Sounds: сохранён префаб " + mapping.CarPrefabPath + ".");
                }
                else
                {
                    Debug.Log("[Editor] Setup Engine Sounds: без изменений — " + mapping.CarPrefabPath + ".");
                }

                return changed;
            }
            finally
            {
                // Undo внутри LoadPrefabContents-потока — no-op, поэтому здесь не используется
                // (см. .claude/rules/editor-files.md / образец SetupNitroTrailsEditor.cs).
                PrefabUtility.UnloadPrefabContents(root);
            }
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
