using TMPro;
using UnityEngine;

namespace FlatOutLike.Views.UI.Common
{
    /// <summary>
    /// Ненавязчиво привлекает внимание к TMP-лейблу (хинту) линейной ping-pong анимацией
    /// размера шрифта. Никакого DI и бизнес-логики — только Unity API.
    /// </summary>
    public sealed class PulsingLabelView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _label;
        [SerializeField] private float _minFontSize = 30f;
        [SerializeField] private float _maxFontSize = 34f;
        [SerializeField] private float _cycleDuration = 1.2f; // полный цикл туда-обратно, сек

        private void Awake()
        {
            if (_label == null)
            {
                Debug.LogWarning($"[PulsingLabelView] '{name}': _label не прошит — пульс не будет работать.", this);
                return;
            }

            // enableAutoSizing игнорирует ручные правки fontSize — анимация молча не сработает.
            if (_label.enableAutoSizing)
                Debug.LogWarning($"[PulsingLabelView] '{name}': у TMP_Text включён enableAutoSizing — fontSize из Update() будет игнорироваться.", this);
        }

        private void Update()
        {
            if (_label == null) return;

            if (_cycleDuration <= 0f)
            {
                _label.fontSize = _minFontSize;
                return;
            }

            // Линейный ping-pong без easing — по запросу, эффект должен быть ненавязчивым.
            float t = Mathf.PingPong(Time.time * 2f / _cycleDuration, 1f);
            _label.fontSize = Mathf.Lerp(_minFontSize, _maxFontSize, t);
        }
    }
}
