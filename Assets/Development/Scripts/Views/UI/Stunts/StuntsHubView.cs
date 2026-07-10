using System;
using System.Collections.Generic;
using FlatOutLike.Models;
using UnityEngine;
using UnityEngine.UI;

namespace FlatOutLike.Views.UI.Stunts
{
    /// <summary>
    /// View экрана Stunts Hub. Агрегирует клики карточек трюков в единое событие наружу —
    /// StuntsHubPresenter не подписывается на каждую карточку по отдельности.
    /// </summary>
    public sealed class StuntsHubView : MonoBehaviour
    {
        [SerializeField] private StuntsCardView[] _cards;
        [SerializeField] private Button _backButton;

        public event Action OnBackClicked;
        public event Action<StuntType> OnCardClicked;

        public IReadOnlyList<StuntsCardView> Cards => _cards;

        private void Awake()
        {
            _backButton.onClick.AddListener(HandleBackClicked);

            if (_cards != null)
                foreach (var card in _cards)
                    if (card != null)
                        card.OnClicked += HandleCardClicked;
        }

        private void OnDestroy()
        {
            _backButton.onClick.RemoveListener(HandleBackClicked);

            if (_cards != null)
                foreach (var card in _cards)
                    if (card != null)
                        card.OnClicked -= HandleCardClicked;
        }

        private void HandleBackClicked() => OnBackClicked?.Invoke();
        private void HandleCardClicked(StuntType type) => OnCardClicked?.Invoke(type);
    }
}
