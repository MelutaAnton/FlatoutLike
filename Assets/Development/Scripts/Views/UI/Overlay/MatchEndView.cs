using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FlatOutLike.Views.UI.Overlay
{
    /// <summary>
    /// Оверлей результата Derby-матча. Скрыт по умолчанию — MatchEndPresenter вызывает Show()
    /// после DerbyMatchEndedEvent/RewardEarnedEvent и Hide() при переходе в Garage.
    /// </summary>
    public sealed class MatchEndView : MonoBehaviour
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private TMP_Text _resultLabel;
        [SerializeField] private TMP_Text _rewardLabel;
        [SerializeField] private Button _continueButton;

        public event Action OnContinueClicked;

        private void Awake()
        {
            if (_panel != null) _panel.SetActive(false); // скрыт по умолчанию
            _continueButton.onClick.AddListener(HandleContinueClicked);
        }

        private void OnDestroy()
        {
            _continueButton.onClick.RemoveListener(HandleContinueClicked);
        }

        /// <summary>
        /// Показывает панель результата. isVictory — победил ли игрок; playerKills — число фрагов;
        /// rewardCoins — начисленная сумма монет (см. MatchEndPresenter про порядок событий).
        /// </summary>
        public void Show(bool isVictory, int playerKills, int rewardCoins)
        {
            if (_panel != null) _panel.SetActive(true);
            if (_resultLabel != null) _resultLabel.text = isVictory ? "VICTORY" : "WRECKED";
            if (_rewardLabel != null) _rewardLabel.text = $"Kills: {playerKills}   +{rewardCoins} coins";
        }

        public void Hide()
        {
            if (_panel != null) _panel.SetActive(false);
        }

        private void HandleContinueClicked() => OnContinueClicked?.Invoke();
    }
}
