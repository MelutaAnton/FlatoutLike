using System;
using FlatOutLike.Models;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FlatOutLike.Views.UI.Garage
{
    /// <summary>
    /// Строка апгрейда в Garage (одна категория). Только отображение уровня/цены —
    /// доступность покупки решает GaragePresenter и передаёт готовым флагом в SetData.
    /// </summary>
    public sealed class UpgradeRowView : MonoBehaviour
    {
        [SerializeField] private Image _icon;
        [SerializeField] private TMP_Text _titleLabel;
        [SerializeField] private TMP_Text _levelLabel;
        [SerializeField] private TMP_Text _priceLabel;
        [SerializeField] private Button _buyButton;

        private UpgradeCategory _category;

        public event Action<UpgradeCategory> OnBuyClicked;

        private void Awake()
        {
            _buyButton.onClick.AddListener(HandleBuyClicked);
        }

        private void OnDestroy()
        {
            _buyButton.onClick.RemoveListener(HandleBuyClicked);
        }

        /// <summary>
        /// Заполняет строку текущим состоянием категории апгрейда.
        /// level/maxLevel — текущий/максимальный уровень, nextPrice — цена следующего уровня
        /// (игнорируется, если maxed), canAfford — хватает ли монет, maxed — уровень уже потолок.
        /// </summary>
        public void SetData(UpgradeCategory category, int level, int maxLevel, int nextPrice, bool canAfford, bool maxed)
        {
            _category = category;
            if (_titleLabel != null) _titleLabel.text = category.ToString();
            if (_levelLabel != null) _levelLabel.text = $"{level}/{maxLevel}";
            if (_priceLabel != null) _priceLabel.text = maxed ? "MAX" : nextPrice.ToString();

            _buyButton.interactable = !maxed && canAfford;
        }

        private void HandleBuyClicked() => OnBuyClicked?.Invoke(_category);
    }
}
