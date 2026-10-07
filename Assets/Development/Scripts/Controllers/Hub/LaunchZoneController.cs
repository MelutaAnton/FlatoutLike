using FlatOutLike.Models;
using FlatOutLike.Services.Hub;
using UnityEngine;
using VContainer;

namespace FlatOutLike.Controllers.Hub
{
    /// <summary>
    /// Триггер зоны запуска режима в BaseHub (game-design §6). Только Unity-физика: сообщает
    /// IHubLaunchService о входе/выходе персонажа хаба; таймер подтверждения и решение о запуске —
    /// в сервисе. Коллайдер (isTrigger) и kinematic Rigidbody ставит editor-сборка сцены.
    /// Сценный объект — инъекция через InjectGameObject в build callback scope.
    /// Ограничение: учёт занятых зон в сервисе рассчитан на ОДИН коллайдер персонажа хаба
    /// (CharacterController). Дочерние коллайдеры на HubCharacter дали бы Enter/Exit на каждый —
    /// первый же Exit убрал бы зону, хотя персонаж ещё внутри.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class LaunchZoneController : MonoBehaviour
    {
        [SerializeField] private BuildingId _buildingId;

        private IHubLaunchService _launch;
        private bool _initialized;

        [Inject]
        public void Construct(IHubLaunchService launch)
        {
            _launch = launch;
            _initialized = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!_initialized || !IsHubCharacter(other)) return;

            _launch.EnterZone(_buildingId);
        }

        private void OnTriggerExit(Collider other)
        {
            if (!_initialized || !IsHubCharacter(other)) return;

            _launch.ExitZone(_buildingId);
        }

        private static bool IsHubCharacter(Collider other)
        {
            if (other == null) return false;

            return other.GetComponentInParent<HubCharacterController>() != null;
        }
    }
}
