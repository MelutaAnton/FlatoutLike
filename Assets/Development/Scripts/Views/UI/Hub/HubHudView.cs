using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FlatOutLike.Views.UI.Hub
{
    /// <summary>
    /// HUD хаба (Шаг 11b, game-design §10 BaseHub): баланс монет и временная кнопка PLAY.
    /// Только UI и сигналы наружу — источник баланса и переход в режим — в HubPresenter.
    /// </summary>
    public sealed class HubHudView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _coinsLabel;
        [SerializeField] private Button _playButton;

        public event Action OnPlayClicked;

        private void Awake()
        {
            if (_playButton != null) _playButton.onClick.AddListener(HandlePlayClicked);
        }

        private void OnDestroy()
        {
            if (_playButton != null) _playButton.onClick.RemoveListener(HandlePlayClicked);
        }

        public void SetCoins(int coins)
        {
            if (_coinsLabel != null) _coinsLabel.text = coins.ToString();
        }

        private void HandlePlayClicked() => OnPlayClicked?.Invoke();
    }
}
