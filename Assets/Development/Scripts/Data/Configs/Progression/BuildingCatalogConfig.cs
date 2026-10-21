using System;
using System.Collections.Generic;
using FlatOutLike.Models;
using UnityEngine;
using UnityEngine.Serialization;

namespace FlatOutLike.Data.Configs.Progression
{
    /// <summary>
    /// Каталог построек BaseHub (game-design §6): где стоит каждая постройка, доступна ли она
    /// и каким префабом рисуется, плюс точка спавна персонажа и зоны запуска режимов (11b:
    /// HubLaunchService + editor-сборка), этапы стройки с ценами и доходом (12a: BuildProgressService;
    /// начисление дохода — шаг 13).
    /// </summary>
    [CreateAssetMenu(fileName = "BuildingCatalogConfig", menuName = "FlatOutLike/Progression/BuildingCatalogConfig")]
    public sealed class BuildingCatalogConfig : ScriptableObject
    {
        [SerializeField] private BuildingEntry[] _entries;

        [Tooltip("Мировая позиция спавна персонажа в BaseHub.")]
        [SerializeField] private Vector3 _characterSpawnPosition;

        [Tooltip("Поворот персонажа при спавне вокруг Y, град.")]
        [SerializeField] private float _characterSpawnYRotation;

        [Tooltip("Подпись на заглушке недоступной постройки.")]
        [SerializeField] private string _comingSoonLabel = "COMING SOON";

        [Tooltip("Сколько секунд персонаж стоит в зоне запуска до старта режима.")]
        [SerializeField] private float _launchConfirmSeconds = 1f;

        [Tooltip("Высота круга подтверждения над центром зоны запуска, м.")]
        [SerializeField] private float _launchIndicatorHeight = 3f;

        // Служебный флаг editor-миграции: дефолты подписей/габаритов (DisplayName, VisualFootprint)
        // заливаются в каталог ОДИН раз — дальше пустая подпись и нулевой габарит означают
        // осознанное «выключено» и миграцией не перезаписываются. Пишет только editor через SerializedObject.
        [SerializeField, HideInInspector] private bool _presentationDefaultsApplied;

        [Tooltip("Визуал пустыря — стадия «0 этапов завершено» у недостроенного здания. " +
                 "null — editor-сборка ставит примитив-заглушку.")]
        [SerializeField] private GameObject _vacantStagePrefab;

        // Служебный флаг editor-миграции: стартовые цены/доход этапов (Stages) заливаются в каталог
        // ОДИН раз — дальше ручной тюнинг миграцией не перезаписывается. Пишет только editor.
        [SerializeField, HideInInspector] private bool _stagesDefaultsApplied;

        [Header("Эффекты стройки (12b)")]
        [Tooltip("Длительность «попа» появления нового визуала стадии, секунды (игровое время). " +
                 "0 и меньше — поп отключён, визуал сразу в базовом масштабе.")]
        [SerializeField] private float _stagePopDuration = 0.35f;

        [Tooltip("Стартовый множитель масштаба нового визуала стадии в начале попа.")]
        [SerializeField] private float _stagePopStartScale = 0.6f;

        [Tooltip("Пиковый множитель масштаба попа перед возвратом к 1.")]
        [SerializeField] private float _stagePopOvershoot = 1.08f;

        [Tooltip("Высота над площадкой, на которой играет VFX завершения здания, м.")]
        [SerializeField] private float _completedVfxHeight = 4f;

        public IReadOnlyList<BuildingEntry> Entries =>
            _entries != null ? _entries : Array.Empty<BuildingEntry>();

        public GameObject VacantStagePrefab => _vacantStagePrefab;

        public Vector3 CharacterSpawnPosition => _characterSpawnPosition;
        public float CharacterSpawnYRotation => _characterSpawnYRotation;
        public string ComingSoonLabel => _comingSoonLabel;
        public float LaunchConfirmSeconds => _launchConfirmSeconds;
        public float LaunchIndicatorHeight => _launchIndicatorHeight;

        public float StagePopDuration => _stagePopDuration;
        public float StagePopStartScale => _stagePopStartScale;
        public float StagePopOvershoot => _stagePopOvershoot;
        public float CompletedVfxHeight => _completedVfxHeight;

        /// <summary>Ищет постройку по id. Каждый BuildingId встречается в каталоге не более раза.</summary>
        public bool TryGetEntry(BuildingId id, out BuildingEntry entry)
        {
            if (_entries != null)
            {
                for (int i = 0; i < _entries.Length; i++)
                {
                    if (_entries[i] != null && _entries[i].Id == id)
                    {
                        entry = _entries[i];
                        return true;
                    }
                }
            }

            entry = null;
            return false;
        }

        /// <summary>
        /// Одна постройка базы: id, размещение, доступность, визуальный префаб, зона здания и этапы стройки.
        /// </summary>
        [Serializable]
        public sealed class BuildingEntry
        {
            public BuildingId Id;
            public Vector3 Position;
            public float YRotation;
            public bool Enabled = true;
            public GameObject VisualPrefab;

            [Tooltip("Подпись площадки на карте (world-space надпись). Пусто — без подписи.")]
            public string DisplayName;

            [Tooltip("Целевой габарит визуала по большей горизонтальной стороне, м. Editor-сборка " +
                     "подбирает масштаб префаба под это значение. 0 — масштаб префаба не трогается. " +
                     "ВАЖНО: смещение зоны (ZoneOffset) считается от центра здания и при " +
                     "изменении габарита его нужно поправить вручную (ориентир: footprint/2 + " +
                     "ZoneSize.z/2 + 0.5).")]
            public float VisualFootprint;

            [Tooltip("Есть ли у постройки запуск режима (§6 «Запуск режимов») — после завершения стройки.")]
            public bool HasLaunchZone;

            [Tooltip("Состояние GameFlow, в которое переводит зона после таймера подтверждения.")]
            public GameState LaunchState;

            [Tooltip("Центр зоны здания локально относительно корня постройки (с учётом её YRotation), м. " +
                     "Зона одна на здание, роль — по состоянию: недостроено — вложение в стройку, " +
                     "построено — запуск режима (если HasLaunchZone), шаг 13 — сбор дохода. " +
                     "Зоны соседних зданий не должны перекрываться по XZ (editor-сборка предупреждает).")]
            [FormerlySerializedAs("LaunchZoneOffset")]
            public Vector3 ZoneOffset;

            [Tooltip("Размер триггер-зоны здания (BoxCollider), м. Зона одна на здание: вложение / запуск / сбор дохода.")]
            [FormerlySerializedAs("LaunchZoneSize")]
            public Vector3 ZoneSize = new Vector3(6f, 3f, 6f);

            [Tooltip("Этапы стройки по порядку (game-design §6). Пусто — здание построено со старта " +
                     "(Garage, DerbyArena).")]
            public BuildStageEntry[] Stages;

            /// <summary>Число этапов стройки (0, если этапов нет).</summary>
            public int StageCount => Stages != null ? Stages.Length : 0;

            /// <summary>Здание без этапов — построено со старта, вложений не принимает.</summary>
            public bool IsBuiltFromStart => StageCount == 0;

            /// <summary>
            /// Мировой центр зоны здания — единственная формула для runtime (круг подтверждения)
            /// и editor-сборки (расстановка триггера и маркера).
            /// </summary>
            public Vector3 GetZoneWorldCenter() =>
                Position + Quaternion.Euler(0f, YRotation, 0f) * ZoneOffset;
        }

        /// <summary>Один этап стройки: цена, визуал после завершения и доход после него.</summary>
        [Serializable]
        public sealed class BuildStageEntry
        {
            [Tooltip("Сколько монет вложить в этап (потоком, стоя в зоне здания).")]
            public int Cost;

            [Tooltip("Визуал здания ПОСЛЕ завершения этапа. null — заглушка editor-сборки; " +
                     "для последнего этапа — VisualPrefab постройки.")]
            public GameObject StagePrefab;

            [Tooltip("Доход здания, монет/мин, после завершения этого этапа. Начисление — шаг 13.")]
            public int IncomePerMinute;
        }
    }
}
