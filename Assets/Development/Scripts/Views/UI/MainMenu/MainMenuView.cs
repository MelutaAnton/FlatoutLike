using System;
using UnityEngine;
using UnityEngine.UI;

namespace FlatOutLike.Views.UI.MainMenu
{
    /// <summary>
    /// View главного меню. Строго MVP: только ссылки на UI-элементы и сигналы кликов наружу —
    /// никакой бизнес-логики и обращений к сервисам, переходы решает MainMenuPresenter.
    /// </summary>
    public sealed class MainMenuView : MonoBehaviour
    {
        [SerializeField] private Button _derbyButton;
        [SerializeField] private Button _stuntsButton;
        [SerializeField] private Button _garageButton;

        public event Action OnDerbyClicked;
        public event Action OnStuntsClicked;
        public event Action OnGarageClicked;

        // Подписка на клики — чистое UI-связывание (Button → C#-событие), не бизнес-логика,
        // поэтому уместна в Awake (правило "Launch() вместо OnEnable/Start" касается запуска
        // игрового поведения, а не проводки Unity UI-компонентов).
        private void Awake()
        {
            _derbyButton.onClick.AddListener(HandleDerbyClicked);
            _stuntsButton.onClick.AddListener(HandleStuntsClicked);
            _garageButton.onClick.AddListener(HandleGarageClicked);
        }

        private void OnDestroy()
        {
            _derbyButton.onClick.RemoveListener(HandleDerbyClicked);
            _stuntsButton.onClick.RemoveListener(HandleStuntsClicked);
            _garageButton.onClick.RemoveListener(HandleGarageClicked);
        }

        private void HandleDerbyClicked() => OnDerbyClicked?.Invoke();
        private void HandleStuntsClicked() => OnStuntsClicked?.Invoke();
        private void HandleGarageClicked() => OnGarageClicked?.Invoke();
    }
}
