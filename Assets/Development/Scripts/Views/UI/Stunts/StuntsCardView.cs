using System;
using FlatOutLike.Models;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FlatOutLike.Views.UI.Stunts
{
    /// <summary>
    /// Карточка трюка в Stunts Hub. Disabled-карточки показывают бейдж "Coming soon" и
    /// не кликабельны — это решает StuntsHubPresenter через флаг isEnabled в SetData.
    /// </summary>
    public sealed class StuntsCardView : MonoBehaviour
    {
        [SerializeField] private Image _preview;
        [SerializeField] private TMP_Text _titleLabel;
        [SerializeField] private GameObject _comingSoonBadge;
        [SerializeField] private Button _button;

        private StuntType _type;

        public event Action<StuntType> OnClicked;

        private void Awake()
        {
            _button.onClick.AddListener(HandleClicked);
        }

        private void OnDestroy()
        {
            _button.onClick.RemoveListener(HandleClicked);
        }

        /// <summary>Заполняет карточку данными одной entry каталога трюков.</summary>
        public void SetData(StuntType type, string title, bool isEnabled, Sprite preview)
        {
            _type = type;
            if (_titleLabel != null) _titleLabel.text = title;

            if (_preview != null)
            {
                _preview.sprite = preview;
                _preview.enabled = preview != null;
            }

            if (_comingSoonBadge != null) _comingSoonBadge.SetActive(!isEnabled);
            _button.interactable = isEnabled;
        }

        private void HandleClicked() => OnClicked?.Invoke(_type);
    }
}
