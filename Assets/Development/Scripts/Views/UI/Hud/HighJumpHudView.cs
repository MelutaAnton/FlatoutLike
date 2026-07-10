using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FlatOutLike.Views.UI.Hud
{
    /// <summary>
    /// HUD трюка «Stunts: High Jump» (Шаг 8d, MVP). Три фазы — Driving/Flight/Scoring —
    /// переключаются presenter'ом целыми группами объектов, без анимаций и переходов.
    /// Только Unity API + сигналы наружу — источники данных (скорость, высота, буст,
    /// рекорд, счёт) и переключение фаз — в HighJumpHudPresenter.
    /// </summary>
    public sealed class HighJumpHudView : MonoBehaviour
    {
        [SerializeField] private GameObject _drivingGroup;
        [SerializeField] private TMP_Text _speedLabel;

        [SerializeField] private GameObject _flightGroup;
        [SerializeField] private TMP_Text _heightLabel;
        [SerializeField] private TMP_Text _bestLabel;
        [SerializeField] private TMP_Text _boostLabel;

        [SerializeField] private GameObject _scorePanel;
        /// <summary>Результат попытки = пиковая ВЫСОТА в метрах (единственная метрика трюка).</summary>
        [SerializeField] private TMP_Text _scoreLabel;
        /// <summary>Строка под результатом: личный рекорд («BEST: 63.0 M»).</summary>
        [SerializeField] private TMP_Text _breakdownLabel;
        [SerializeField] private GameObject _newRecordLabel;
        [SerializeField] private Button _continueButton;

        /// <summary>Тик Update — Unity-lifecycle, View имеет на него право (см. CLAUDE.md).</summary>
        public event Action OnTick;
        public event Action OnContinueClicked;

        private void Awake()
        {
            // Скрыты по умолчанию — фазу задаёт presenter сразу после Construct
            // (стартовое GameStateChangedEvent свежий scope не публикует, см. IGameFlowService.Current).
            if (_scorePanel != null) _scorePanel.SetActive(false);
            if (_newRecordLabel != null) _newRecordLabel.SetActive(false);

            if (_continueButton != null)
                _continueButton.onClick.AddListener(HandleContinueClicked);
        }

        private void OnDestroy()
        {
            if (_continueButton != null)
                _continueButton.onClick.RemoveListener(HandleContinueClicked);
        }

        private void Update() => OnTick?.Invoke();

        public void ShowDrivingPhase()
        {
            SetActive(_drivingGroup, true);
            SetActive(_flightGroup, false);
            SetActive(_scorePanel, false);
        }

        public void ShowFlightPhase()
        {
            SetActive(_drivingGroup, false);
            SetActive(_flightGroup, true);
            SetActive(_scorePanel, false);
        }

        public void ShowScoringPhase(float peakHeight, float bestHeight, bool isNewRecord)
        {
            SetActive(_drivingGroup, false);
            SetActive(_flightGroup, false);
            SetActive(_scorePanel, true);

            if (_scoreLabel != null) _scoreLabel.text = $"{peakHeight:F1} M";
            if (_breakdownLabel != null) _breakdownLabel.text = $"BEST: {bestHeight:F1} M";
            SetActive(_newRecordLabel, isNewRecord);
        }

        public void SetSpeed(float kmh)
        {
            if (_speedLabel != null) _speedLabel.text = $"{kmh:F0} km/h";
        }

        public void SetHeight(float current, float best)
        {
            if (_heightLabel != null) _heightLabel.text = $"{current:F1} m";
            if (_bestLabel != null) _bestLabel.text = $"BEST: {best:F1} m";
        }

        public void SetBoostAvailable(bool available)
        {
            if (_boostLabel != null) _boostLabel.text = available ? "BOOST READY" : "BOOST USED";
        }

        private static void SetActive(GameObject go, bool active)
        {
            if (go != null) go.SetActive(active);
        }

        private void HandleContinueClicked() => OnContinueClicked?.Invoke();
    }
}
