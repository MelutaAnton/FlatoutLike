using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FlatOutLike.Views.UI.Hud
{
    /// <summary>
    /// HUD Derby-матча (Шаг 8d MVP + Шаг 10b/10c): HP-бар игрока, nitro-бар, счётчик живых
    /// соперников, kill-feed лента и экран смерти игрока (Wrecked). Pause-оверлей — отдельный
    /// View (см. PauseOverlayView).
    /// Только Unity API + сигналы наружу — вся нормировка и источники данных в DerbyHudPresenter.
    /// </summary>
    public sealed class DerbyHudView : MonoBehaviour
    {
        [SerializeField] private Image _hpBar;
        [SerializeField] private Image _nitroBar;
        [SerializeField] private TMP_Text _aliveLabel;
        [SerializeField] private GameObject _root;

        // ── Шаг 10b: kill-feed ───────────────────────────────────────────────────
        [SerializeField] private RectTransform _killFeedContainer;
        [SerializeField] private KillFeedItemView[] _killFeedItems;

        // ── Шаг 10c: экран смерти игрока ────────────────────────────────────────
        [SerializeField] private GameObject _wreckedPanel;
        [SerializeField] private Button _wreckedWatchButton;
        [SerializeField] private Button _wreckedLeaveButton;

        /// <summary>Длительность показа одной записи kill-feed.</summary>
        private const float KillFeedEntryLifetimeSeconds = 4f;

        // Индекс следующего слота kill-feed, переиспользуемого по кругу.
        private int _nextKillFeedIndex;

        public event Action OnWreckedWatchClicked;
        public event Action OnWreckedLeaveClicked;

        /// <summary>
        /// Тик Update — Unity-lifecycle, View имеет на него право (см. CLAUDE.md).
        /// Presenter поллит continuous-значения (нитро дренится каждый кадр, событий нет).
        /// </summary>
        public event Action OnTick;

        private void Awake()
        {
            if (_wreckedPanel != null) _wreckedPanel.SetActive(false); // скрыт по умолчанию

            if (_wreckedWatchButton != null) _wreckedWatchButton.onClick.AddListener(HandleWreckedWatchClicked);
            if (_wreckedLeaveButton != null) _wreckedLeaveButton.onClick.AddListener(HandleWreckedLeaveClicked);
        }

        private void OnDestroy()
        {
            if (_wreckedWatchButton != null) _wreckedWatchButton.onClick.RemoveListener(HandleWreckedWatchClicked);
            if (_wreckedLeaveButton != null) _wreckedLeaveButton.onClick.RemoveListener(HandleWreckedLeaveClicked);
        }

        private void Update() => OnTick?.Invoke();

        /// <summary>Добавляет запись в kill-feed — занимает следующий слот по кругу.</summary>
        public void PushKillFeedEntry(string text)
        {
            if (_killFeedItems == null || _killFeedItems.Length == 0) return;

            var item = _killFeedItems[_nextKillFeedIndex];
            _nextKillFeedIndex = (_nextKillFeedIndex + 1) % _killFeedItems.Length;

            if (item == null) return;
            item.Show(text, KillFeedEntryLifetimeSeconds);
        }

        /// <summary>Показывает экран смерти игрока (Wrecked) с кнопками Watch/Leave.</summary>
        public void ShowWreckedPanel()
        {
            if (_wreckedPanel != null) _wreckedPanel.SetActive(true);
        }

        public void HideWreckedPanel()
        {
            if (_wreckedPanel != null) _wreckedPanel.SetActive(false);
        }

        private void HandleWreckedWatchClicked() => OnWreckedWatchClicked?.Invoke();
        private void HandleWreckedLeaveClicked() => OnWreckedLeaveClicked?.Invoke();

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
