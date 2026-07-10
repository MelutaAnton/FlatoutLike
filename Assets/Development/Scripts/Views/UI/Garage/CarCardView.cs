using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FlatOutLike.Views.UI.Garage
{
    /// <summary>
    /// Карточка машины в Garage. Только отображение: состояние (owned/selected/canAfford)
    /// приходит из GaragePresenter через SetState — никаких обращений к IEconomyService отсюда.
    /// </summary>
    public sealed class CarCardView : MonoBehaviour
    {
        [SerializeField] private Image _icon;
        [SerializeField] private TMP_Text _title;
        [SerializeField] private TMP_Text _priceLabel;
        [SerializeField] private GameObject _lockIcon;
        [SerializeField] private GameObject _selectedMarker;
        [SerializeField] private Button _actionButton;

        // Id хранится после SetData — HandleClicked передаёт его наружу без круговой ссылки
        // на каталог (карточка не знает про CarCatalogConfig).
        private string _carId = string.Empty;

        public event Action<string> OnClicked;

        private void Awake()
        {
            _actionButton.onClick.AddListener(HandleClicked);
        }

        private void OnDestroy()
        {
            _actionButton.onClick.RemoveListener(HandleClicked);
        }

        /// <summary>Заполняет статичные данные карточки (вызывается один раз при построении списка).</summary>
        public void SetData(string carId, string title, int price, Sprite icon)
        {
            _carId = carId;
            if (_title != null) _title.text = title;
            if (_priceLabel != null) _priceLabel.text = price > 0 ? price.ToString() : "FREE";

            if (_icon != null)
            {
                _icon.sprite = icon;
                _icon.enabled = icon != null; // entry без иконки — просто пустая картинка, не падаем
            }
        }

        /// <summary>Обновляет визуальное состояние карточки по данным экономики.</summary>
        public void SetState(bool owned, bool selected, bool canAfford)
        {
            if (_lockIcon != null) _lockIcon.SetActive(!owned);
            if (_selectedMarker != null) _selectedMarker.SetActive(selected);

            // Владеемую машину всегда можно выбрать; невладеемую — только если хватает монет.
            _actionButton.interactable = owned || canAfford;
        }

        private void HandleClicked() => OnClicked?.Invoke(_carId);
    }
}
