using System;
using UnityEngine;
using UnityEngine.UI;

namespace FlatOutLike.Views.UI.Overlay
{
    /// <summary>
    /// Оверлей паузы (Шаг 10a). Скрыт по умолчанию — PauseOverlayPresenter вызывает Show()/Hide()
    /// по GamePausedEvent/GameResumedEvent. Только UI, никакой логики GameFlow здесь.
    /// </summary>
    public sealed class PauseOverlayView : MonoBehaviour
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private Button _resumeButton;
        [SerializeField] private Button _leaveButton;

        public event Action OnResumeClicked;
        public event Action OnLeaveClicked;

        private void Awake()
        {
            if (_panel != null) _panel.SetActive(false); // скрыт по умолчанию

            if (_resumeButton != null) _resumeButton.onClick.AddListener(HandleResumeClicked);
            if (_leaveButton != null) _leaveButton.onClick.AddListener(HandleLeaveClicked);
        }

        private void OnDestroy()
        {
            if (_resumeButton != null) _resumeButton.onClick.RemoveListener(HandleResumeClicked);
            if (_leaveButton != null) _leaveButton.onClick.RemoveListener(HandleLeaveClicked);
        }

        public void Show()
        {
            if (_panel != null) _panel.SetActive(true);
        }

        public void Hide()
        {
            if (_panel != null) _panel.SetActive(false);
        }

        /// <summary>Показывает/скрывает кнопку Leave (в BaseHub уходить некуда — кнопка скрыта).</summary>
        public void SetLeaveVisible(bool visible)
        {
            if (_leaveButton != null) _leaveButton.gameObject.SetActive(visible);
        }

        private void HandleResumeClicked() => OnResumeClicked?.Invoke();
        private void HandleLeaveClicked() => OnLeaveClicked?.Invoke();
    }
}
