using System;
using System.Collections.Generic;
using FlatOutLike.Data.Configs.Audio;
using FlatOutLike.Models;
using UnityEditor;
using UnityEngine;

namespace FlatOutLike.Editor.Setup
{
    /// <summary>
    /// Прошивает дефолтные клипы Universal Sound FX (+ музыку из Development/Audio/Music)
    /// в AudioConfig_Default.asset. Подбор клипов — по каталогу пака (см. маппинг ниже),
    /// тюнинг = замена клипов в инспекторе конфига.
    /// Идемпотентность: слот, где клипы уже прошиты (вручную или прошлым прогоном),
    /// НЕ перезаписывается — скрипт заполняет только пустые слоты.
    /// Прогонять ПОСЛЕ «Setup Audio (All Scenes)» (создаёт сам конфиг).
    /// </summary>
    public static class AssignDefaultAudioClipsEditor
    {
        private const string ConfigAssetPath = "Assets/Development/Data/Configs/Audio/AudioConfig_Default.asset";
        private const string UsfxRoot  = "Assets/ThirdParty/Universal Sound FX/";
        private const string MusicRoot = "Assets/Development/Audio/Music/";

        // ── Маппинг SfxId → клипы пака (несколько = случайные варианты в рантайме) ──
        private static readonly (SfxId id, string[] paths)[] SfxClipMap =
        {
            (SfxId.UiClick, new[]
            {
                UsfxRoot + "BUTTONS/BUTTON_Clean_Tap_mono.wav",
                UsfxRoot + "BUTTONS/BUTTON_Click_01_mono.wav",
            }),
            (SfxId.CrashLight, new[]
            {
                UsfxRoot + "IMPACTS/Metal/IMPACT_Metal_Hit_Sheet_Metal_RR1_mono.wav",
                UsfxRoot + "IMPACTS/Metal/IMPACT_Metal_Hit_Sheet_Metal_RR2_mono.wav",
                UsfxRoot + "IMPACTS/Metal/IMPACT_Metal_Hard_01_mono.wav",
                UsfxRoot + "IMPACTS/Metal/IMPACT_Metal_Hard_02_mono.wav",
            }),
            (SfxId.CrashHeavy, new[]
            {
                UsfxRoot + "IMPACTS/Metal/IMPACT_Metal_Hit_Large_Metal_01_mono.wav",
                UsfxRoot + "IMPACTS/Metal/IMPACT_Metal_Hit_Large_Metal_02_mono.wav",
                UsfxRoot + "IMPACTS/Metal/IMPACT_Metal_Large_Deep_Metallic_Bang_Rattle_stereo.wav",
                UsfxRoot + "IMPACTS/Metal/IMPACT_Metal_Large_Rattle_stereo.wav",
            }),
            (SfxId.CarWrecked, new[]
            {
                UsfxRoot + "EXPLOSIONS/Medium/EXPLOSION_Medium_Metallic_Burst_Smooth_Tail_mono.wav",
                UsfxRoot + "EXPLOSIONS/Medium/EXPLOSION_Medium_Blast_Scatter_Debris_mono.wav",
                UsfxRoot + "EXPLOSIONS/Medium/EXPLOSION_Medium_Flame_Burst_Burning_Tail_mono.wav",
            }),
            // Луп — рантайм берёт Clips[0], один клип достаточно.
            (SfxId.NitroLoop, new[]
            {
                UsfxRoot + "THRUSTERS_FLAMES/THRUSTER_Deep_Powerful_Burning_Air_Big_Flames_loop_mono.wav",
            }),
            // «Comperssed» — опечатка в реальных именах файлов пака, не исправлять.
            (SfxId.PickupRepair, new[]
            {
                UsfxRoot + "TOOLS/Impact_Wrench/TOOL_Impact_Wrench_Comperssed_Air_Short_Burst_01_mono.wav",
            }),
            (SfxId.PickupNitro, new[]
            {
                UsfxRoot + "CHARGE_UPS_DOWNS/CHARGE_Complex_Wet_12_Semi_Up_500ms_mono.wav",
            }),
            (SfxId.Eject, new[]
            {
                UsfxRoot + "WHOOSHES/Air/WHOOSH_Air_Very_Fast_RR1_mono.wav",
                UsfxRoot + "WHOOSHES/Air/WHOOSH_Air_Very_Fast_RR2_mono.wav",
                UsfxRoot + "WHOOSHES/Air/WHOOSH_Air_Very_Fast_RR3_mono.wav",
            }),
            (SfxId.MidairBoost, new[]
            {
                UsfxRoot + "WHOOSHES/Air/WHOOSH_Air_Super_Fast_RR1_mono.wav",
                UsfxRoot + "WHOOSHES/Air/WHOOSH_Air_Super_Fast_RR2_mono.wav",
            }),
            (SfxId.Landing, new[]
            {
                UsfxRoot + "THUDS_THUMPS/THUD_Deep_Noisy_01_mono.wav",
                UsfxRoot + "THUDS_THUMPS/THUD_Dark_01_mono.wav",
                UsfxRoot + "THUDS_THUMPS/THUD_Dark_02_mono.wav",
            }),
            (SfxId.NewRecord, new[]
            {
                UsfxRoot + "MONEY_CASH_CURRENCY/SLOT_MACHINE_Win_Fanfare_01_mono.wav",
            }),
            (SfxId.MatchWin, new[]
            {
                UsfxRoot + "MUSIC_EFFECTS/MUSIC_EFFECT_Dubstep_Positive_stereo.wav",
            }),
            (SfxId.MatchLose, new[]
            {
                UsfxRoot + "MUSIC_EFFECTS/MUSIC_EFFECT_Dubstep_Negative_stereo.wav",
            }),
            (SfxId.Purchase, new[]
            {
                UsfxRoot + "MONEY_CASH_CURRENCY/CASH_REGISTER_Cha-ching_01_mono.wav",
                UsfxRoot + "MONEY_CASH_CURRENCY/CASH_REGISTER_Cha-ching_02_mono.wav",
            }),
        };

        // ── Маппинг MusicTrackId → трек (Pixabay, Development/Audio/Music) ──────────
        private static readonly (MusicTrackId id, string path)[] MusicClipMap =
        {
            (MusicTrackId.Menu,     MusicRoot + "nastelbom-background-music-463062.mp3"),
            (MusicTrackId.Derby,    MusicRoot + "alexgrohl-got-that-heat-this-action-punk-rock-454153.mp3"),
            (MusicTrackId.HighJump, MusicRoot + "magpiemusic-pop-punk-rock-179615.mp3"),
        };

        [MenuItem("FlatOutLike/Setup/Assign Default Audio Clips")]
        public static void AssignDefaultClips()
        {
            var config = AssetDatabase.LoadAssetAtPath<AudioConfig>(ConfigAssetPath);
            if (config == null)
            {
                Debug.LogWarning("[Editor] Assign Default Audio Clips: не найден " + ConfigAssetPath +
                                 " — сначала прогони FlatOutLike/Setup/Setup Audio (All Scenes).");
                return;
            }

            var so = new SerializedObject(config);

            int sfxFilled   = FillSfxClips(so);
            int musicFilled = FillMusicClips(so);

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();

            Debug.Log("[Editor] Assign Default Audio Clips: прошито SFX-слотов " + sfxFilled +
                      ", музыкальных треков " + musicFilled + ". Уже заполненные слоты не тронуты.");
        }

        private static int FillSfxClips(SerializedObject so)
        {
            var entries = so.FindProperty("_sfxEntries");
            if (entries == null)
            {
                Debug.LogWarning("[Editor] Не найдено поле _sfxEntries в AudioConfig — маппинг SFX пропущен.");
                return 0;
            }

            var enumValues = (SfxId[])Enum.GetValues(typeof(SfxId));
            int filled = 0;

            foreach (var (id, paths) in SfxClipMap)
            {
                int enumIndex = Array.IndexOf(enumValues, id);
                var entry = FindEntryByEnumIndex(entries, "Id", enumIndex);
                if (entry == null)
                {
                    Debug.LogWarning("[Editor] В конфиге нет SfxEntry для " + id + " — пропуск (перепрогони Setup Audio).");
                    continue;
                }

                var clipsProp = entry.FindPropertyRelative("Clips");
                if (clipsProp == null) continue;
                if (clipsProp.arraySize > 0) continue; // слот уже заполнен — не затираем ручной тюнинг

                var clips = LoadClips(paths, id.ToString());
                if (clips.Count == 0) continue;

                clipsProp.arraySize = clips.Count;
                for (int i = 0; i < clips.Count; i++)
                    clipsProp.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];

                Debug.Log("[Editor] " + id + " ← " + clips.Count + " клип(ов).");
                filled++;
            }

            return filled;
        }

        private static int FillMusicClips(SerializedObject so)
        {
            var entries = so.FindProperty("_musicEntries");
            if (entries == null)
            {
                Debug.LogWarning("[Editor] Не найдено поле _musicEntries в AudioConfig — маппинг музыки пропущен.");
                return 0;
            }

            var enumValues = (MusicTrackId[])Enum.GetValues(typeof(MusicTrackId));
            int filled = 0;

            foreach (var (id, path) in MusicClipMap)
            {
                int enumIndex = Array.IndexOf(enumValues, id);
                var entry = FindEntryByEnumIndex(entries, "Id", enumIndex);
                if (entry == null)
                {
                    Debug.LogWarning("[Editor] В конфиге нет MusicEntry для " + id + " — пропуск (перепрогони Setup Audio).");
                    continue;
                }

                var clipProp = entry.FindPropertyRelative("Clip");
                if (clipProp == null) continue;
                if (clipProp.objectReferenceValue != null) continue; // трек уже прошит — не затираем

                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip == null)
                {
                    Debug.LogWarning("[Editor] Не найден трек " + path + " для " + id + " — слот оставлен пустым.");
                    continue;
                }

                clipProp.objectReferenceValue = clip;
                Debug.Log("[Editor] Музыка " + id + " ← " + System.IO.Path.GetFileName(path));
                filled++;
            }

            return filled;
        }

        /// <summary>Элемент массива, чьё enum-поле <paramref name="enumField"/> равно enumIndex; null если нет.</summary>
        private static SerializedProperty FindEntryByEnumIndex(SerializedProperty array, string enumField, int enumIndex)
        {
            for (int i = 0; i < array.arraySize; i++)
            {
                var element = array.GetArrayElementAtIndex(i);
                var idProp = element.FindPropertyRelative(enumField);
                if (idProp != null && idProp.enumValueIndex == enumIndex)
                    return element;
            }

            return null;
        }

        /// <summary>Загружает клипы по путям; отсутствующие файлы пропускаются с Warning.</summary>
        private static List<AudioClip> LoadClips(string[] paths, string label)
        {
            var clips = new List<AudioClip>(paths.Length);
            foreach (var path in paths)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip == null)
                {
                    Debug.LogWarning("[Editor] Не найден клип " + path + " (слот " + label + ").");
                    continue;
                }

                clips.Add(clip);
            }

            return clips;
        }
    }
}
