using TMPro;
using UnityEngine;

namespace FlatOutLike.Views.UI.Hud
{
    /// <summary>
    /// Один слот kill-feed ленты (Шаг 10b). Никакого DI и бизнес-логики — только показ текста
    /// и собственный таймер жизни. DerbyHudView переиспользует фиксированный набор слотов
    /// по кругу (см. DerbyHudView.PushKillFeedEntry), сам слот о переиспользовании не знает.
    /// </summary>
    public sealed class KillFeedItemView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _label;

        private float _remainingSeconds;
        private bool _active;

        private void Awake()
        {
            gameObject.SetActive(false); // скрыт по умолчанию
        }

        /// <summary>Показывает запись на lifetimeSeconds секунд, после чего слот сам скрывается.</summary>
        public void Show(string text, float lifetimeSeconds)
        {
            if (_label != null) _label.text = text;

            _remainingSeconds = lifetimeSeconds;
            _active = true;
            gameObject.SetActive(true);
        }

        private void Update()
        {
            if (!_active) return;

            // Осознанно scaled deltaTime: на паузе (timeScale=0) запись «замирает» вместе
            // со всем геймплеем и доживает свой срок после резюма.
            _remainingSeconds -= Time.deltaTime;
            if (_remainingSeconds <= 0f)
            {
                _active = false;
                gameObject.SetActive(false);
            }
        }
    }
}
