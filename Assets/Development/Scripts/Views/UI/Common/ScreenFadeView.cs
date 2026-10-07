using System;
using FlatOutLike.Services.Flow;
using UnityEngine;

namespace FlatOutLike.Views.UI.Common
{
    /// <summary>
    /// Полноэкранная чёрная шторка межсценового перехода (префаб Prefabs/UI/ScreenFadeCanvas).
    /// Только Unity API: двигает CanvasGroup.alpha к целевому значению по Time.unscaledDeltaTime,
    /// поэтому работает и при Time.timeScale = 0 (переход из паузы). Начальное состояние
    /// (alpha = 1, blocksRaycasts = true) задаётся сериализованным префабом, а не Awake/OnEnable:
    /// новая сцена стартует чёрной, проявление запускается явно через FadeIn().
    /// Инъекция не нужна — регистрируется как IScreenFader через RegisterComponent.
    /// </summary>
    public sealed class ScreenFadeView : MonoBehaviour, IScreenFader
    {
        private const float OpaqueAlpha = 1f;
        private const float TransparentAlpha = 0f;

        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField, Min(0f)] private float _fadeOutSeconds = 0.25f;
        [SerializeField, Min(0f)] private float _fadeInSeconds = 0.35f;

        private float _targetAlpha;
        private bool _animating;
        private Action _onFadeOutComplete;

        public void FadeOut(Action onComplete)
        {
            // Без шторки или с выключенным объектом Update не дойдёт до конца — ведём себя как
            // NullScreenFader (мгновенный переход), иначе загрузка сцены зависла бы навсегда.
            if (!CanAnimate(nameof(FadeOut)))
            {
                if (onComplete != null)
                    onComplete();
                return;
            }

            // Блокируем ввод сразу — двойной тап по UI во время затемнения не должен пройти.
            _canvasGroup.blocksRaycasts = true;
            _onFadeOutComplete = onComplete;
            StartFade(OpaqueAlpha);
            // Если alpha уже 1 — завершение произойдёт на ближайшем Update.
        }

        public void FadeIn()
        {
            if (!CanAnimate(nameof(FadeIn)))
                return;

            if (_onFadeOutComplete != null)
            {
                // Затемнение под переход уже идёт — проявлять уходящую сцену нельзя,
                // иначе обещанный onComplete (старт загрузки) никогда не будет вызван.
                Debug.LogWarning($"[ScreenFadeView] '{name}': FadeIn во время незавершённого FadeOut — проигнорирован.", this);
                return;
            }

            _canvasGroup.blocksRaycasts = false;
            StartFade(TransparentAlpha);
        }

        private void Update()
        {
            if (!_animating || _canvasGroup == null)
                return;

            bool fadingOut = _targetAlpha >= OpaqueAlpha;
            float duration = fadingOut ? _fadeOutSeconds : _fadeInSeconds;

            _canvasGroup.alpha = duration <= 0f
                ? _targetAlpha
                : Mathf.MoveTowards(_canvasGroup.alpha, _targetAlpha, Time.unscaledDeltaTime / duration);

            if (!Mathf.Approximately(_canvasGroup.alpha, _targetAlpha))
                return;

            _canvasGroup.alpha = _targetAlpha;
            _animating = false;

            if (fadingOut)
                CompleteFadeOut();
        }

        private void StartFade(float targetAlpha)
        {
            _targetAlpha = targetAlpha;
            _animating = true;
        }

        private void CompleteFadeOut()
        {
            // Обнуляем до вызова: колбэк вызывается ровно один раз, даже если внутри него
            // кто-то снова дёрнет FadeOut/FadeIn.
            var callback = _onFadeOutComplete;
            _onFadeOutComplete = null;
            if (callback != null)
                callback();
        }

        private bool CanAnimate(string caller)
        {
            if (_canvasGroup == null)
            {
                Debug.LogWarning($"[ScreenFadeView] '{name}': _canvasGroup не прошит — {caller} выполняется мгновенно без шторки.", this);
                return false;
            }

            if (!isActiveAndEnabled)
            {
                Debug.LogWarning($"[ScreenFadeView] '{name}': компонент выключен — {caller} выполняется мгновенно без шторки.", this);
                return false;
            }

            return true;
        }
    }
}
