using FlatOutLike.Models;
using FlatOutLike.Services.Progression;
using UnityEngine;
using VContainer;

namespace FlatOutLike.Controllers.Hub
{
    /// <summary>
    /// Триггер вложения в стройку BaseHub (game-design §6, шаг 12a). Только Unity-физика:
    /// сообщает IBuildProgressService о входе/выходе персонажа хаба; поток монет, списание
    /// и решение «здание уже построено — игнор» — в сервисе. Висит на ТОМ ЖЕ объекте зоны,
    /// что LaunchZoneController (одна зона на здание, роль — по состоянию стройки).
    /// Коллайдер (isTrigger) и kinematic Rigidbody ставит editor-сборка сцены.
    /// Сценный объект — инъекция через InjectGameObject площадки (BuildSiteController) в build callback.
    /// Ограничение то же, что у LaunchZoneController: рассчитан на ОДИН коллайдер персонажа хаба.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class InvestZoneController : MonoBehaviour
    {
        [SerializeField] private BuildingId _buildingId;

        private IBuildProgressService _build;
        private bool _initialized;

        [Inject]
        public void Construct(IBuildProgressService build)
        {
            _build = build;
            _initialized = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!_initialized || !IsHubCharacter(other)) return;

            _build.EnterZone(_buildingId);
        }

        private void OnTriggerExit(Collider other)
        {
            if (!_initialized || !IsHubCharacter(other)) return;

            _build.ExitZone(_buildingId);
        }

        private static bool IsHubCharacter(Collider other)
        {
            if (other == null) return false;

            return other.GetComponentInParent<HubCharacterController>() != null;
        }
    }
}
