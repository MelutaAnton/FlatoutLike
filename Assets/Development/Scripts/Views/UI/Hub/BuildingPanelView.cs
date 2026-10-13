using FlatOutLike.Models;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FlatOutLike.Views.UI.Hub
{
    /// <summary>
    /// World-space ценник здания BaseHub (game-design §6, шаг 12a): прогресс текущего этапа
    /// стройки («X LEFT» + заливка) и доход здания («+N/min»). Только UI — что и когда показывать,
    /// решает BuildingPanelPresenter. Без инъекции: presenter получает все панели сцены списком.
    /// </summary>
    public sealed class BuildingPanelView : MonoBehaviour
    {
        [SerializeField] private BuildingId _buildingId;
        [SerializeField] private GameObject _progressGroup;
        [SerializeField] private Image _progressFill;
        [SerializeField] private TMP_Text _remainingText;
        [SerializeField] private GameObject _rateGroup;
        [SerializeField] private TMP_Text _rateText;

        public BuildingId BuildingId => _buildingId;

        /// <summary>Прогресс текущего этапа: заливка invested/cost и остаток «X LEFT».</summary>
        public void SetProgress(int invested, int cost)
        {
            if (_progressFill != null)
                _progressFill.fillAmount = cost > 0 ? Mathf.Clamp01((float)invested / cost) : 1f;

            // SetText с аргументом — без аллокаций строки на каждую пачку.
            if (_remainingText != null)
                _remainingText.SetText("{0:0} LEFT", Mathf.Max(0, cost - invested));
        }

        public void SetProgressVisible(bool visible)
        {
            if (_progressGroup != null && _progressGroup.activeSelf != visible)
                _progressGroup.SetActive(visible);
        }

        /// <summary>Доход здания, монет/мин; 0 — группа дохода скрыта.</summary>
        public void SetRate(int perMinute)
        {
            bool visible = perMinute > 0;
            if (_rateGroup != null && _rateGroup.activeSelf != visible)
                _rateGroup.SetActive(visible);

            if (visible && _rateText != null)
                _rateText.SetText("+{0:0}/min", perMinute);
        }
    }
}
