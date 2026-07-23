#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using FlatOutLike.Controllers.Audio;
using FlatOutLike.Data.Configs.Audio;
using FlatOutLike.Installers;
using FlatOutLike.Models;
using FlatOutLike.Views.UI.Common;

namespace FlatOutLike.Editor.Setup
{
    /// <summary>
    /// Проводит аудио-слой (SfxId/MusicTrackId, AudioConfig, AudioPlayer prefab) во ВСЕ
    /// enabled-сцены из Build Settings. Написан ПАРАЛЛЕЛЬНО с рантайм-кодом — на момент
    /// написания AudioConfig/AudioPlayerController/UiClickSoundRelay/SfxId/MusicTrackId
    /// ещё могли не существовать. Компилируется только ПОСЛЕ появления рантайм-классов
    /// с контрактными именами (см. ниже) — запускать после их компиляции.
    ///
    /// Контракт имён (зафиксирован в задаче, НЕ менять без синхронизации с рантайм-агентом):
    ///   enum FlatOutLike.Models.SfxId — None, UiClick, CrashLight, CrashHeavy, CarWrecked,
    ///     NitroLoop, PickupRepair, PickupNitro, Eject, MidairBoost, Landing, NewRecord,
    ///     MatchWin, MatchLose, Purchase.
    ///   enum FlatOutLike.Models.MusicTrackId — None, Menu, Derby, HighJump.
    ///   SO FlatOutLike.Data.Configs.Audio.AudioConfig со списками:
    ///     SfxEntry { SfxId Id; AudioClip[] Clips; float Volume; float PitchJitter; float CooldownSeconds; }
    ///     MusicEntry { MusicTrackId Id; AudioClip Clip; float Volume; }
    ///     StateMusicEntry { GameState State; MusicTrackId Track; }
    ///   MonoBehaviour FlatOutLike.Controllers.Audio.AudioPlayerController,
    ///                 FlatOutLike.Views.UI.Common.UiClickSoundRelay.
    ///   GameLifetimeScope: _audioConfig, _audioPlayerController, _uiClickSoundRelay.
    ///
    /// Точные имена сериализованных СПИСКОВ-полей AudioConfig (SfxEntry[]/MusicEntry[]/
    /// StateMusicEntry[]) на момент написания скрипта неизвестны — заполнение идёт через
    /// SerializedObject по КАНДИДАТАМ имён (см. *EntriesCandidates), первое найденное
    /// используется. Если реальное имя отличается от всех кандидатов — Debug.LogWarning
    /// с точным списком опробованного; соответствующий список тогда не заполняется, но
    /// остальная проводка (scope/сцены/префаб) всё равно отрабатывает. Перед финальной
    /// сдачей сверься с реальным AudioConfig.cs и, если нужно, дополни кандидатов.
    ///
    /// Шаги:
    ///   1) EnsureAudioConfigAsset() — Assets/Development/Data/Configs/Audio/AudioConfig_Default.asset.
    ///      При СОЗДАНИИ — по одной SfxEntry на каждый SfxId (кроме None), MusicEntry на
    ///      Menu/Derby/HighJump, StateMusicEntry-маппинг GameState→трек. Существующий ассет —
    ///      миграционно дополняется отсутствующими entries, существующие не трогаются.
    ///   2) EnsureAudioPlayerPrefab() — Assets/Development/Prefabs/Systems/AudioPlayer.prefab
    ///      (AudioPlayerController + UiClickSoundRelay на корне).
    ///   3) Обход enabled-сцен (паттерн WireInputConfigEditor): найти/создать инстанс
    ///      AudioPlayer ПО КОМПОНЕНТУ (не по имени), прошить scope._audioConfig/
    ///      _audioPlayerController/_uiClickSoundRelay. Сцена сохраняется только при правке.
    /// </summary>
    public static class SetupAudioEditor
    {
        private const string ConfigAssetPath = "Assets/Development/Data/Configs/Audio/AudioConfig_Default.asset";

        private const string PrefabPath   = "Assets/Development/Prefabs/Systems/AudioPlayer.prefab";
        private const string PrefabGoName = "AudioPlayer";

        // Кандидаты имён сериализованных списков AudioConfig — первое найденное используется.
        private static readonly string[] SfxEntriesCandidates        = { "_sfxEntries", "_sfx", "_sfxList", "sfxEntries" };
        private static readonly string[] MusicEntriesCandidates      = { "_musicEntries", "_music", "_musicList", "musicEntries" };
        private static readonly string[] StateMusicEntriesCandidates = { "_stateMusicEntries", "_stateMusic", "_stateMusicMap", "stateMusicEntries" };

        // GameState → MusicTrackId (см. задачу). Значения, отсутствующие в текущем GameState.cs,
        // пропускаются с Debug.Log — enum мог измениться независимо от этого скрипта.
        private static readonly (string state, MusicTrackId track)[] StateMusicMap =
        {
            ("MainMenu", MusicTrackId.Menu),
            ("GarageMeta", MusicTrackId.Menu),
            ("StuntsHub", MusicTrackId.Menu),
            ("DerbyMatch", MusicTrackId.Derby),
            ("MatchEnd", MusicTrackId.Derby),
            ("StuntsHighJumpDriving", MusicTrackId.HighJump),
            ("StuntsHighJumpFlight", MusicTrackId.HighJump),
            ("StuntsHighJumpScoring", MusicTrackId.HighJump),
        };

        [MenuItem("FlatOutLike/Setup/Setup Audio (All Scenes)")]
        public static void SetupAudio()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log("[Editor] Setup Audio: отменено пользователем.");
                return;
            }

            Undo.SetCurrentGroupName("Setup Audio");

            var config = EnsureAudioConfigAsset();
            var prefab = EnsureAudioPlayerPrefab();

            WireAllScenes(config, prefab);

            Debug.Log("[Editor] Setup Audio (All Scenes) завершён.");
        }

        // ── Шаг 1: AudioConfig ──────────────────────────────────────────────────

        private static AudioConfig EnsureAudioConfigAsset()
        {
            EnsureFolder("Assets/Development/Data", "Configs");
            EnsureFolder("Assets/Development/Data/Configs", "Audio");

            var config = AssetDatabase.LoadAssetAtPath<AudioConfig>(ConfigAssetPath);
            bool isNew = config == null;

            if (isNew)
            {
                config = ScriptableObject.CreateInstance<AudioConfig>();
                AssetDatabase.CreateAsset(config, ConfigAssetPath);
                // Без Undo.RegisterCreatedObjectUndo: на ассетах Undo некорректен — при
                // OpenScene(Single) сброс undo-стека уничтожал managed-ссылку (fake-null),
                // и проводка _audioConfig по сценам молча пропускалась («не присваивать null»).
                Debug.Log("[Editor] Создан AudioConfig.asset: " + ConfigAssetPath);
            }
            else
            {
                Debug.Log("[Editor] AudioConfig.asset уже существует: " + ConfigAssetPath + " — дополняем отсутствующие entries.");
            }

            var so = new SerializedObject(config);

            FillSfxEntries(so);
            FillMusicEntries(so);
            FillStateMusicEntries(so);

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();

            return config;
        }

        private static void FillSfxEntries(SerializedObject so)
        {
            var entries = FindPropertyAny(so, SfxEntriesCandidates, "SfxEntry[]");
            if (entries == null) return;

            var values = (SfxId[])Enum.GetValues(typeof(SfxId));
            int added = 0;

            for (int i = 0; i < values.Length; i++)
            {
                var id = values[i];
                if (id == SfxId.None) continue;

                if (FindEnumEntryIndex(entries, "Id", i) >= 0)
                    continue; // уже есть entry на этот SfxId — не трогаем (может быть донастроен вручную)

                int idx = entries.arraySize;
                entries.arraySize = idx + 1;
                var e = entries.GetArrayElementAtIndex(idx);
                var idProp = e.FindPropertyRelative("Id");
                if (idProp != null) idProp.enumValueIndex = i;
                var clipsProp = e.FindPropertyRelative("Clips");
                if (clipsProp != null) clipsProp.arraySize = 0;
                SetFloatIfExists(e, "Volume", 1f);
                SetFloatIfExists(e, "PitchJitter", 0.05f);
                SetFloatIfExists(e, "CooldownSeconds", 0.08f);
                added++;
            }

            Debug.Log("[Editor] AudioConfig SfxEntry: добавлено " + added + " из " + (values.Length - 1) + " (SfxId кроме None).");
        }

        private static void FillMusicEntries(SerializedObject so)
        {
            var entries = FindPropertyAny(so, MusicEntriesCandidates, "MusicEntry[]");
            if (entries == null) return;

            MusicTrackId[] tracks = { MusicTrackId.Menu, MusicTrackId.Derby, MusicTrackId.HighJump };
            var allValues = (MusicTrackId[])Enum.GetValues(typeof(MusicTrackId));
            int added = 0;

            foreach (var track in tracks)
            {
                int enumIndex = Array.IndexOf(allValues, track);
                if (enumIndex < 0) continue;

                if (FindEnumEntryIndex(entries, "Id", enumIndex) >= 0)
                    continue;

                int idx = entries.arraySize;
                entries.arraySize = idx + 1;
                var e = entries.GetArrayElementAtIndex(idx);
                var idProp = e.FindPropertyRelative("Id");
                if (idProp != null) idProp.enumValueIndex = enumIndex;
                var clipProp = e.FindPropertyRelative("Clip");
                if (clipProp != null) clipProp.objectReferenceValue = null;
                // Элементы, созданные через arraySize++, инициализируются НУЛЯМИ — C#
                // field-инициализаторы (Volume = 0.5f) для них не выполняются. Без явной
                // записи музыка осталась бы немой даже после прошивки клипов.
                SetFloatIfExists(e, "Volume", 0.5f);
                added++;
            }

            Debug.Log("[Editor] AudioConfig MusicEntry: добавлено " + added + " из " + tracks.Length + " (Menu/Derby/HighJump).");
        }

        private static void FillStateMusicEntries(SerializedObject so)
        {
            var entries = FindPropertyAny(so, StateMusicEntriesCandidates, "StateMusicEntry[]");
            if (entries == null) return;

            var gameStateValues = (GameState[])Enum.GetValues(typeof(GameState));
            var trackValues = (MusicTrackId[])Enum.GetValues(typeof(MusicTrackId));
            int added = 0;
            int skipped = 0;

            foreach (var (stateName, track) in StateMusicMap)
            {
                int stateIndex = Array.FindIndex(gameStateValues, s => s.ToString() == stateName);
                if (stateIndex < 0)
                {
                    Debug.Log("[Editor] AudioConfig StateMusicEntry: GameState." + stateName +
                              " не найден в текущем enum — пропуск.");
                    skipped++;
                    continue;
                }

                int trackIndex = Array.IndexOf(trackValues, track);

                if (FindEnumEntryIndex(entries, "State", stateIndex) >= 0)
                    continue;

                int idx = entries.arraySize;
                entries.arraySize = idx + 1;
                var e = entries.GetArrayElementAtIndex(idx);
                var stateProp = e.FindPropertyRelative("State");
                if (stateProp != null) stateProp.enumValueIndex = stateIndex;
                var trackProp = e.FindPropertyRelative("Track");
                if (trackProp != null) trackProp.enumValueIndex = trackIndex;
                added++;
            }

            Debug.Log("[Editor] AudioConfig StateMusicEntry: добавлено " + added + ", пропущено " + skipped + ".");
        }

        // ── Шаг 2: AudioPlayer prefab ───────────────────────────────────────────

        private static GameObject EnsureAudioPlayerPrefab()
        {
            EnsureFolder("Assets/Development/Prefabs", "Systems");

            bool exists = File.Exists(Path.Combine(Directory.GetCurrentDirectory(), PrefabPath));

            if (exists)
            {
                var root = PrefabUtility.LoadPrefabContents(PrefabPath);
                try
                {
                    bool changed = ConfigureAudioPlayerRoot(root);
                    if (changed)
                    {
                        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                        Debug.Log("[Editor] Обновлён префаб: " + PrefabPath);
                    }
                    else
                    {
                        Debug.Log("[Editor] Префаб уже актуален: " + PrefabPath);
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }

                return AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            }

            var tempGo = new GameObject(PrefabGoName);
            try
            {
                ConfigureAudioPlayerRoot(tempGo);
                var savedPrefab = PrefabUtility.SaveAsPrefabAsset(tempGo, PrefabPath);
                Debug.Log("[Editor] Создан префаб: " + PrefabPath);
                return savedPrefab;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tempGo);
            }
        }

        private static bool ConfigureAudioPlayerRoot(GameObject go)
        {
            bool changed = false;

            if (go.GetComponent<AudioPlayerController>() == null)
            {
                go.AddComponent<AudioPlayerController>();
                Debug.Log("[Editor] Добавлен компонент AudioPlayerController на " + go.name + ".");
                changed = true;
            }

            if (go.GetComponent<UiClickSoundRelay>() == null)
            {
                go.AddComponent<UiClickSoundRelay>();
                Debug.Log("[Editor] Добавлен компонент UiClickSoundRelay на " + go.name + ".");
                changed = true;
            }

            return changed;
        }

        // ── Шаг 3: проводка сцен ────────────────────────────────────────────────

        private static void WireAllScenes(AudioConfig config, GameObject prefab)
        {
            AssetDatabase.SaveAssets();

            string originalScenePath = SceneManager.GetActiveScene().path;

            var scenes = EditorBuildSettings.scenes;
            int wired = 0;
            int skipped = 0;

            foreach (var buildScene in scenes)
            {
                if (!buildScene.enabled)
                    continue;

                if (string.IsNullOrEmpty(buildScene.path) || !File.Exists(buildScene.path))
                {
                    Debug.LogWarning("[Editor] Setup Audio: файл сцены не найден — " + buildScene.path + ", пропуск.");
                    skipped++;
                    continue;
                }

                var scene = EditorSceneManager.OpenScene(buildScene.path, OpenSceneMode.Single);

                var scope = UnityEngine.Object.FindAnyObjectByType<GameLifetimeScope>();
                if (scope == null)
                {
                    Debug.Log("[Editor] " + buildScene.path + ": GameLifetimeScope не найден, пропуск.");
                    skipped++;
                    continue;
                }

                // ПЕРЕЧИТЫВАЕМ ассеты ПОСЛЕ OpenScene: смена сцены (Single) выгружает
                // неиспользуемые ассеты, managed-ссылка НЕ удерживает нативный объект —
                // ссылки, полученные до OpenScene, становятся fake-null, и прошивка
                // _audioConfig молча пропускалась во всех сценах (инцидент 2026-07-23).
                var liveConfig = AssetDatabase.LoadAssetAtPath<AudioConfig>(ConfigAssetPath);
                var livePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);

                bool sceneChanged = WireScene(scene, scope, liveConfig, livePrefab);

                if (sceneChanged)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    Debug.Log("[Editor] " + buildScene.path + ": аудио-слой прошит.");
                }
                else
                {
                    Debug.Log("[Editor] " + buildScene.path + ": аудио-слой уже прошит, без изменений.");
                }

                wired++;
            }

            RestoreOriginalScene(originalScenePath);

            Debug.Log("[Editor] Setup Audio: обработано " + wired + ", пропущено " + skipped + " из " + scenes.Length + " enabled-сцен.");
        }

        private static bool WireScene(Scene scene, GameLifetimeScope scope, AudioConfig config, GameObject prefab)
        {
            bool changed = false;

            // Идемпотентность: ищем инстанс ПО КОМПОНЕНТУ, не по имени GameObject — включая неактивные.
            var audioPlayerComponent = FindComponentInSceneIncludingInactive<AudioPlayerController>(scene);
            GameObject audioPlayerGo = audioPlayerComponent != null ? audioPlayerComponent.gameObject : null;

            if (audioPlayerGo == null)
            {
                if (prefab == null)
                {
                    Debug.LogWarning("[Editor] " + scene.path + ": префаб AudioPlayer недоступен — инстанс не создан.");
                }
                else
                {
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                    Undo.RegisterCreatedObjectUndo(instance, "Create AudioPlayer instance");
                    audioPlayerGo = instance;
                    Debug.Log("[Editor] " + scene.path + ": создан инстанс AudioPlayer.");
                    changed = true;
                }
            }

            changed |= EnsureAudioListener(scene, audioPlayerGo);

            var so = new SerializedObject(scope);

            changed |= SetObjectRefIfMissing(so, "_audioConfig", config, "AudioConfig");

            if (audioPlayerGo != null)
            {
                var apc = audioPlayerGo.GetComponent<AudioPlayerController>();
                var relay = audioPlayerGo.GetComponent<UiClickSoundRelay>();

                changed |= SetObjectRefIfMissing(so, "_audioPlayerController", apc, "AudioPlayerController");
                changed |= SetObjectRefIfMissing(so, "_uiClickSoundRelay", relay, "UiClickSoundRelay");
            }

            so.ApplyModifiedProperties();

            return changed;
        }

        /// <summary>
        /// Гарантирует ровно один AudioListener в сцене: без него Unity не воспроизводит
        /// НИЧЕГО («There are no audio listeners in the scene» — мета-сцены с UI-камерами
        /// без слушателя были немыми). Если слушателей нет — вешаем на камеру сцены
        /// (правильная позиция для будущих 3D-звуков), без камеры — на AudioPlayer
        /// (для 2D-звуков позиция слушателя не важна). Существующие слушатели не трогаем.
        /// </summary>
        private static bool EnsureAudioListener(Scene scene, GameObject fallbackHost)
        {
            if (FindComponentInSceneIncludingInactive<AudioListener>(scene) != null)
                return false;

            var camera = UnityEngine.Object.FindAnyObjectByType<UnityEngine.Camera>();
            GameObject host = camera != null ? camera.gameObject : fallbackHost;
            if (host == null)
            {
                Debug.LogWarning("[Editor] " + scene.path + ": ни камеры, ни AudioPlayer — AudioListener не добавлен.");
                return false;
            }

            Undo.AddComponent<AudioListener>(host);
            Debug.Log("[Editor] " + scene.path + ": добавлен AudioListener на \"" + host.name + "\".");
            return true;
        }

        private static T FindComponentInSceneIncludingInactive<T>(Scene scene) where T : Component
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var found = root.GetComponentInChildren<T>(true);
                if (found != null)
                    return found;
            }
            return null;
        }

        /// <summary>
        /// Прошивает поле scope, только если оно ещё не указывает на нужный объект.
        /// Правило "не присваивать null": если value == null (объект не найден) — LogWarning
        /// и существующее значение поля НЕ трогается.
        /// </summary>
        private static bool SetObjectRefIfMissing(SerializedObject so, string fieldName, UnityEngine.Object value, string humanName)
        {
            var prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                Debug.LogWarning("[Editor] Поле GameLifetimeScope." + fieldName + " не найдено (рантайм ещё не скомпилирован?).");
                return false;
            }

            if (value == null)
            {
                Debug.LogWarning("[Editor] " + humanName + " не найден — поле " + fieldName + " не тронуто.");
                return false;
            }

            if (prop.objectReferenceValue == value)
                return false;

            prop.objectReferenceValue = value;
            return true;
        }

        // ── Утилиты ─────────────────────────────────────────────────────────────

        private static SerializedProperty FindPropertyAny(SerializedObject so, string[] candidates, string humanName)
        {
            foreach (var name in candidates)
            {
                var prop = so.FindProperty(name);
                if (prop != null)
                    return prop;
            }

            Debug.LogWarning("[Editor] AudioConfig: не найдено поле-список " + humanName +
                              " ни по одному из имён-кандидатов (" + string.Join(", ", candidates) +
                              "). Список не заполнен — сверься с реальными именами полей в AudioConfig.cs " +
                              "и при необходимости добавь их в кандидаты SetupAudioEditor.");
            return null;
        }

        private static int FindEnumEntryIndex(SerializedProperty arrayProp, string enumFieldName, int enumValueIndex)
        {
            for (int i = 0; i < arrayProp.arraySize; i++)
            {
                var e = arrayProp.GetArrayElementAtIndex(i);
                var f = e.FindPropertyRelative(enumFieldName);
                if (f != null && f.enumValueIndex == enumValueIndex)
                    return i;
            }
            return -1;
        }

        private static void SetFloatIfExists(SerializedProperty entry, string fieldName, float value)
        {
            var prop = entry.FindPropertyRelative(fieldName);
            if (prop != null)
                prop.floatValue = value;
        }

        private static void RestoreOriginalScene(string originalScenePath)
        {
            if (string.IsNullOrEmpty(originalScenePath))
                return;

            if (SceneManager.GetActiveScene().path == originalScenePath)
                return;

            EditorSceneManager.OpenScene(originalScenePath, OpenSceneMode.Single);
            Debug.Log("[Editor] Возврат в исходную сцену: " + originalScenePath);
        }

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
