using System;
using System.Collections.Generic;
using FlatOutLike.Models;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FlatOutLike.Views.UI.Garage
{
    /// <summary>
    /// View экрана Garage. Агрегирует клики дочерних карточек машин и строк апгрейдов в единые
    /// события наружу — GaragePresenter не подписывается на каждую карточку/строку отдельно.
    /// </summary>
    public sealed class GarageView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _coinsLabel;
        [SerializeField] private CarCardView[] _carCards;
        [SerializeField] private UpgradeRowView[] _upgradeRows;
        [SerializeField] private Button _backButton;

        public event Action OnBackClicked;
        public event Action<string> OnCarCardClicked;
        public event Action<UpgradeCategory> OnUpgradeBuyClicked;

        public IReadOnlyList<CarCardView> CarCards => _carCards;
        public IReadOnlyList<UpgradeRowView> UpgradeRows => _upgradeRows;

        private void Awake()
        {
            _backButton.onClick.AddListener(HandleBackClicked);

            if (_carCards != null)
                foreach (var card in _carCards)
                    if (card != null)
                        card.OnClicked += HandleCarCardClicked;

            if (_upgradeRows != null)
                foreach (var row in _upgradeRows)
                    if (row != null)
                        row.OnBuyClicked += HandleUpgradeBuyClicked;
        }

        private void OnDestroy()
        {
            _backButton.onClick.RemoveListener(HandleBackClicked);

            if (_carCards != null)
                foreach (var card in _carCards)
                    if (card != null)
                        card.OnClicked -= HandleCarCardClicked;

            if (_upgradeRows != null)
                foreach (var row in _upgradeRows)
                    if (row != null)
                        row.OnBuyClicked -= HandleUpgradeBuyClicked;
        }

        public void SetCoins(int coins)
        {
            if (_coinsLabel != null) _coinsLabel.text = coins.ToString();
        }

        private void HandleBackClicked() => OnBackClicked?.Invoke();
        private void HandleCarCardClicked(string carId) => OnCarCardClicked?.Invoke(carId);
        private void HandleUpgradeBuyClicked(UpgradeCategory category) => OnUpgradeBuyClicked?.Invoke(category);
    }
}
