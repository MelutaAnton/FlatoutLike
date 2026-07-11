using UnityEngine;
using UnityEngine.EventSystems;

namespace FlatOutLike.Controllers.Mobile
{
    /// <summary>
    /// Собственный виртуальный джойстик для мобильного HUD (Шаг 9). ThirdParty Joystick Pack
    /// использовать нельзя (см. CLAUDE.md — его классы недоступны из FlatOutLike.asmdef,
    /// а сам пак не редактируется), поэтому реализуем классическую схему сами: смещение
    /// пальца относительно центра _background, нормированное на радиус фона.
    /// Только Unity API (EventSystems + RectTransform) — DI не нужен, бизнес-логики нет.
    /// </summary>
    public sealed class VirtualJoystickView : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [SerializeField] private RectTransform _background;
        [SerializeField] private RectTransform _handle;

        private Canvas _canvas;

        /// <summary>Нормированный ввод -1..1 по каждой оси, magnitude клампится до 1.</summary>
        public Vector2 Direction { get; private set; }

        private void Awake()
        {
            _canvas = GetComponentInParent<Canvas>();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            OnDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_background == null || _handle == null) return;

            // UnityEngine.Camera полностью: из FlatOutLike.Controllers.* голое имя Camera
            // резолвится в соседний неймспейс Controllers.Camera (CS0118).
            UnityEngine.Camera cam = ResolveEventCamera();

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _background, eventData.position, cam, out Vector2 localPoint))
                return;

            // rect.width — фактическая ширина при любых анкорах (sizeDelta при stretch-анкорах
            // перестаёт быть размером). Предполагается pivot фона (0.5, 0.5) — центр стика.
            float radius = _background.rect.width * 0.5f;
            if (radius <= 0f) return;

            Vector2 clamped = Vector2.ClampMagnitude(localPoint, radius);
            Direction = clamped / radius;

            _handle.anchoredPosition = clamped;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            ResetStick();
        }

        private void OnDisable()
        {
            // Группа может скрыться (смена контекста Driving↔UI/Flight) во время удержания
            // пальца — OnPointerUp тогда не придёт, и стик «залипнет» на последнем значении
            // (фантомное движение после повторного показа). Симметрично HoldButtonView.
            ResetStick();
        }

        private void ResetStick()
        {
            Direction = Vector2.zero;

            if (_handle != null)
                _handle.anchoredPosition = Vector2.zero;
        }

        private UnityEngine.Camera ResolveEventCamera()
        {
            // Overlay-канвас работает без камеры (worldCamera == null — это валидное значение
            // для ScreenPointToLocalPointInRectangle), ScreenSpaceCamera/WorldSpace — с ней.
            if (_canvas == null) return null;
            return _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
        }
    }
}
