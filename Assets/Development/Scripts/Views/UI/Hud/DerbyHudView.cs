using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FlatOutLike.Views.UI.Hud
{
    /// <summary>
    /// HUD Derby-матча (Шаг 8d, MVP): HP-бар игрока, nitro-бар, счётчик живых соперников.
    /// Kill-feed и Pause-кнопка — вне этого среза (см. game-design.md §10).
    /// Только Unity API + сигналы наружу — вся нормировка и источники данных в DerbyHudPresenter.
    /// </summary>
    public sealed class DerbyHudView : MonoBehaviour
    {
        [SerializeField] private Image _hpBar;
        [SerializeField] private Image _nitroBar;
        [SerializeField] private TMP_Text _aliveLabel;
        [SerializeField] private GameObject _root;

        /// <summary>
        /// Тик Update — Unity-lifecycle, View имеет на него право (см. CLAUDE.md).
        /// Presenter поллит continuous-значения (нитро дренится каждый кадр, событий нет).
        /// </summary>
        public event Action OnTick;

        private void Update() => OnTick?.Invoke();

        /// <summary>Заполнение HP-бара, 0..1.</summary>
        public void SetHp(float normalized)
        {
            if (_hpBar != null) _hpBar.fillAmount = Mathf.Clamp01(normalized);
        }

        /// <summary>Заполнение nitro-бара, 0..1.</summary>
        public void SetNitro(float normalized)
        {
            if (_nitroBar != null) _nitroBar.fillAmount = Mathf.Clamp01(normalized);
        }

        public void SetAliveCount(int count)
        {
            if (_aliveLabel != null) _aliveLabel.text = count.ToString();
        }

        /// <summary>Скрывает весь HUD целиком — после смерти игрока или конца матча.</summary>
        public void SetVisible(bool visible)
        {
            if (_root != null) _root.SetActive(visible);
        }
    }
}
