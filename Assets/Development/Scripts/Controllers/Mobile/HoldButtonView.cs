using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FlatOutLike.Controllers.Mobile
{
    /// <summary>
    /// Кнопка мобильного HUD с двумя режимами использования: непрерывное удержание
    /// (<see cref="IsHeld"/> — ручник, нитро) и одноразовое нажатие (<see cref="OnPressed"/> —
    /// eject, буст, пауза). Вешается на объект с Graphic (raycastTarget=true), обычно Image.
    /// Только Unity API (EventSystems) — DI не нужен, бизнес-логики нет.
    /// </summary>
    public sealed class HoldButtonView : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] private Button _button;

        // Button.interactable сам по себе НЕ блокирует IPointerDown/Up — это независимые
        // обработчики, поэтому нужен свой флаг вдобавок к визуальному отключению кнопки.
        private bool _interactable = true;

        public bool IsHeld { get; private set; }

        public event Action OnPressed;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!_interactable) return;

            IsHeld = true;
            OnPressed?.Invoke();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!_interactable) return;

            IsHeld = false;
        }

        /// <summary>Включает/выключает кнопку (например, буст без зарядов). При выключении сбрасывает удержание.</summary>
        public void SetInteractable(bool value)
        {
            _interactable = value;
            if (_button != null) _button.interactable = value;

            if (!value)
                IsHeld = false; // тот же сброс, что и в OnDisable — палец мог удерживать кнопку в момент выключения
        }

        private void OnDisable()
        {
            // Объект может быть скрыт (смена контекста Driving/Flight) прямо во время
            // удержания пальца — без сброса IsHeld «залипнет» в true до следующего тача.
            IsHeld = false;
        }
    }
}
