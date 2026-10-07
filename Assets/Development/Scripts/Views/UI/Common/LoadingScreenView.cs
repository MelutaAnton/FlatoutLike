using System;
using FlatOutLike.Data.Configs.Flow;
using FlatOutLike.Services.Flow;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace FlatOutLike.Views.UI.Common
{
    /// <summary>
    /// Экран загрузки межсценового перехода (префаб Prefabs/UI/LoadingScreenCanvas, Шаг 11d):
    /// заголовок места назначения, подсказка, полоса и процент прогресса. Только Unity API:
    /// alpha корня и подсказки двигаются в Update по Time.unscaledDeltaTime, поэтому экран
    /// анимируется и при Time.timeScale = 0 (переход из паузы, заморозка старой сцены).
    /// Начальное состояние (alpha = 0) задаётся сериализованным префабом, а не Awake/OnEnable:
    /// новая сцена закрывается явно через Cover() из конструктора SceneLoaderService.
    /// Регистрируется как ILoadingScreen через RegisterComponent — Construct получает длительности.
    /// </summary>
    public sealed class LoadingScreenView : MonoBehaviour, ILoadingScreen
    {
        private const float OpaqueAlpha = 1f;
        private const float TransparentAlpha = 0f;
        private const float PercentScale = 100f;
        private const int NoPercent = -1;

        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private TMP_Text _hintText;
        [SerializeField] private CanvasGroup _hintGroup;
        [SerializeField] private Image _progressFill;
        [SerializeField] private TMP_Text _percentText;

        // Длительности приходят только из LoadingScreenConfig (единственный источник дефолтов).
        // Если Construct не вызван или конфиг null — остаются 0: все анимации мгновенные
        // (Update сразу ставит целевую alpha), по смыслу то же, что ветка «Update не будет».
        private float _showSeconds;
        private float _hideSeconds;
        private float _hintFadeSeconds;

        private float _targetAlpha;
        private bool _animating;
        private Action _onShown;

        private float _hintTargetAlpha;
        private bool _hintAnimating;

        // Последний выведенный целый процент — текст перестраивается только при его смене.
        private int _shownPercent = NoPercent;

        [Inject]
        public void Construct(LoadingScreenConfig config)
        {
            if (config == null)
                return;

            _showSeconds = config.ShowSeconds;
            _hideSeconds = config.HideSeconds;
            _hintFadeSeconds = config.HintFadeSeconds;
        }

        public void Cover(string title)
        {
            SetTitle(title);
            SetProgress(1f);
            SetHintAlphaImmediate(TransparentAlpha);

            if (_canvasGroup == null)
                return;

            _canvasGroup.alpha = OpaqueAlpha;
            _canvasGroup.blocksRaycasts = true;
            _targetAlpha = OpaqueAlpha;
            // Незавершённый Show (теоретически) завершится на ближайшем Update — onShown не теряется.
            _animating = _onShown != null;
        }

        public void Show(string title, string hint, Action onShown)
        {
            // Без экрана или с выключенным объектом Update не дойдёт до конца — ведём себя как
            // NullLoadingScreen (мгновенный переход), иначе загрузка сцены зависла бы навсегда.
            if (!CanAnimate(nameof(Show)))
            {
                if (onShown != null)
                    onShown();
                return;
            }

            if (_onShown != null)
                Debug.LogWarning($"[LoadingScreenView] '{name}': Show во время незавершённого Show — прежний onShown заменён.", this);

            SetTitle(title);
            if (_hintText != null)
                _hintText.text = hint ?? string.Empty;
            SetHintAlphaImmediate(OpaqueAlpha);
            SetProgress(0f);

            // Блокируем ввод сразу — двойной тап по UI во время проявления не должен пройти.
            _canvasGroup.blocksRaycasts = true;
            _onShown = onShown;
            StartFade(OpaqueAlpha);
            // Если alpha уже 1 — onShown вызовется на ближайшем Update.
        }

        public void SetProgress(float progress01)
        {
            float progress = Mathf.Clamp01(progress01);

            if (_progressFill != null)
                _progressFill.fillAmount = progress;

            if (_percentText == null)
                return;

            int percent = Mathf.FloorToInt(progress * PercentScale);
            if (percent == _shownPercent)
                return;

            _shownPercent = percent;
            // SetText с форматом не аллоцирует строку (в отличие от интерполяции в .text).
            _percentText.SetText("{0:0}%", percent);
        }

        public void HideHint()
        {
            if (_hintGroup == null)
                return;

            if (!WillUpdate() || _hintFadeSeconds <= 0f)
            {
                SetHintAlphaImmediate(TransparentAlpha);
                return;
            }

            _hintTargetAlpha = TransparentAlpha;
            _hintAnimating = true;
        }

        public void Hide()
        {
            if (_canvasGroup == null)
                return;

            if (_onShown != null)
            {
                // Экран проявляется под переход — открывать уходящую сцену нельзя,
                // иначе обещанный onShown (старт загрузки) никогда не будет вызван.
                Debug.LogWarning($"[LoadingScreenView] '{name}': Hide во время незавершённого Show — проигнорирован.", this);
                return;
            }

            _canvasGroup.blocksRaycasts = false;

            if (!WillUpdate())
            {
                // Update не будет — убираем экран мгновенно, чтобы он не застрял поверх сцены.
                _canvasGroup.alpha = TransparentAlpha;
                _animating = false;
                return;
            }

            StartFade(TransparentAlpha);
        }

        private void Update()
        {
            UpdateRootFade();
            UpdateHintFade();
        }

        private void UpdateRootFade()
        {
            if (!_animating || _canvasGroup == null)
                return;

            bool showing = _targetAlpha >= OpaqueAlpha;
            float duration = showing ? _showSeconds : _hideSeconds;

            _canvasGroup.alpha = duration <= 0f
                ? _targetAlpha
                : Mathf.MoveTowards(_canvasGroup.alpha, _targetAlpha, Time.unscaledDeltaTime / duration);

            if (!Mathf.Approximately(_canvasGroup.alpha, _targetAlpha))
                return;

            _canvasGroup.alpha = _targetAlpha;
            _animating = false;

            if (showing)
                CompleteShow();
        }

        private void UpdateHintFade()
        {
            if (!_hintAnimating || _hintGroup == null)
                return;

            _hintGroup.alpha = _hintFadeSeconds <= 0f
                ? _hintTargetAlpha
                : Mathf.MoveTowards(_hintGroup.alpha, _hintTargetAlpha, Time.unscaledDeltaTime / _hintFadeSeconds);

            if (!Mathf.Approximately(_hintGroup.alpha, _hintTargetAlpha))
                return;

            _hintGroup.alpha = _hintTargetAlpha;
            _hintAnimating = false;
        }

        private void StartFade(float targetAlpha)
        {
            _targetAlpha = targetAlpha;
            _animating = true;
        }

        private void CompleteShow()
        {
            // Обнуляем до вызова: колбэк вызывается ровно один раз, даже если внутри него
            // кто-то снова дёрнет Show/Hide (например, Hide при неудачном старте загрузки).
            var callback = _onShown;
            _onShown = null;
            if (callback != null)
                callback();
        }

        private void SetTitle(string title)
        {
            if (_titleText != null)
                _titleText.text = title ?? string.Empty;
        }

        private void SetHintAlphaImmediate(float alpha)
        {
            _hintAnimating = false;
            if (_hintGroup != null)
                _hintGroup.alpha = alpha;
        }

        /// <summary>
        /// Будет ли вызываться Update. Не isActiveAndEnabled: тот возвращает false, пока у компонента
        /// не отработал OnEnable, — а Show/Cover вызываются при сборке контейнера (Awake scope),
        /// когда OnEnable экрана мог ещё не случиться (Boot). Включённый компонент на активном
        /// объекте гарантированно получит Update.
        /// </summary>
        private bool WillUpdate() => enabled && gameObject.activeInHierarchy;

        private bool CanAnimate(string caller)
        {
            if (_canvasGroup == null)
            {
                Debug.LogWarning($"[LoadingScreenView] '{name}': _canvasGroup не прошит — {caller} выполняется мгновенно без экрана.", this);
                return false;
            }

            if (!WillUpdate())
            {
                Debug.LogWarning($"[LoadingScreenView] '{name}': компонент выключен — {caller} выполняется мгновенно без экрана.", this);
                return false;
            }

            return true;
        }
    }
}
