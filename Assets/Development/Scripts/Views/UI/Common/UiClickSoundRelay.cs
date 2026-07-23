using System.Collections.Generic;
using FlatOutLike.Models;
using FlatOutLike.Services.Audio;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace FlatOutLike.Views.UI.Common
{
    /// <summary>
    /// Вешает клик-звук на ВСЕ кнопки сцены (включая неактивные на момент старта — панели
    /// меню, скрытые оверлеи). Один экземпляр на сцену — не требует ручной проводки каждой кнопки.
    /// FindObjectsByType в рантайме — НАМЕРЕННОЕ исключение (одноразовый скан в Start,
    /// глобальный UI-хук вместо проводки N кнопок). Ограничение: кнопки, инстанцируемые
    /// ПОСЛЕ Start, звук не получат — сейчас таких нет (карточки Garage предсозданы в сцене).
    /// </summary>
    public sealed class UiClickSoundRelay : MonoBehaviour
    {
        private IAudioService _audioService;
        private bool _initialized;

        private readonly List<Button> _boundButtons = new List<Button>();

        [Inject]
        public void Construct(IAudioService audioService)
        {
            _audioService = audioService;
            _initialized = true;
        }

        private void Start()
        {
            // OnEnable/Start могут отработать до Construct — гвард обязателен (CLAUDE.md).
            if (!_initialized) return;

            var buttons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var button in buttons)
            {
                button.onClick.AddListener(OnAnyButtonClicked);
                _boundButtons.Add(button);
            }
        }

        private void OnAnyButtonClicked()
        {
            _audioService.PlaySfx(SfxId.UiClick);
        }

        private void OnDestroy()
        {
            foreach (var button in _boundButtons)
            {
                if (button != null)
                    button.onClick.RemoveListener(OnAnyButtonClicked);
            }

            _boundButtons.Clear();
        }
    }
}
