using System;
using UnityEngine;
using UnityEngine.EventSystems;

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
        public bool IsHeld { get; private set; }

        public event Action OnPressed;

        public void OnPointerDown(PointerEventData eventData)
        {
            IsHeld = true;
            OnPressed?.Invoke();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            IsHeld = false;
        }

        private void OnDisable()
        {
            // Объект может быть скрыт (смена контекста Driving/Flight) прямо во время
            // удержания пальца — без сброса IsHeld «залипнет» в true до следующего тача.
            IsHeld = false;
        }
    }
}
