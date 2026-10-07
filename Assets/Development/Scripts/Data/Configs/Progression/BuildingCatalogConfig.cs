using System;
using System.Collections.Generic;
using FlatOutLike.Models;
using UnityEngine;

namespace FlatOutLike.Data.Configs.Progression
{
    /// <summary>
    /// Каталог построек BaseHub (game-design §6): где стоит каждая постройка, доступна ли она
    /// и каким префабом рисуется, плюс точка спавна персонажа и зоны запуска режимов (11b:
    /// HubLaunchService + editor-сборка). Этапы стройки/цены/доход — шаги 12/13.
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

        public IReadOnlyList<BuildingEntry> Entries =>
            _entries != null ? _entries : Array.Empty<BuildingEntry>();

        public Vector3 CharacterSpawnPosition => _characterSpawnPosition;
        public float CharacterSpawnYRotation => _characterSpawnYRotation;
        public string ComingSoonLabel => _comingSoonLabel;
        public float LaunchConfirmSeconds => _launchConfirmSeconds;
        public float LaunchIndicatorHeight => _launchIndicatorHeight;

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

        /// <summary>Одна постройка базы: id, размещение, доступность, визуальный префаб и зона запуска режима.</summary>
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
                     "ВАЖНО: смещение зоны запуска (LaunchZoneOffset) считается от центра здания и при " +
                     "изменении габарита его нужно поправить вручную (ориентир: footprint/2 + " +
                     "LaunchZoneSize.z/2 + 0.5).")]
            public float VisualFootprint;

            [Tooltip("Есть ли у постройки зона запуска режима (§6 «Запуск режимов»).")]
            public bool HasLaunchZone;

            [Tooltip("Состояние GameFlow, в которое переводит зона после таймера подтверждения.")]
            public GameState LaunchState;

            [Tooltip("Центр зоны локально относительно корня постройки (с учётом её YRotation), м. " +
                     "Зоны соседних зданий не должны перекрываться по XZ (editor-сборка предупреждает).")]
            public Vector3 LaunchZoneOffset;

            [Tooltip("Размер триггер-зоны (BoxCollider), м.")]
            public Vector3 LaunchZoneSize = new Vector3(6f, 3f, 6f);

            /// <summary>
            /// Мировой центр зоны запуска — единственная формула для runtime (круг подтверждения)
            /// и editor-сборки (расстановка триггера и маркера).
            /// </summary>
            public Vector3 GetLaunchZoneWorldCenter() =>
                Position + Quaternion.Euler(0f, YRotation, 0f) * LaunchZoneOffset;
        }
    }
}
