using FlatOutLike.Core.Events;
using FlatOutLike.Services.Driver;
using FlatOutLike.Services.Meta;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VContainer;

namespace FlatOutLike.Controllers.Stunts
{
    /// <summary>
    /// ВРЕМЕННЫЙ debug-оверлей «High Jump» — заглушка до полноценного MVP-HUD (Шаг 8).
    /// Показывает высоту/время полёта в реальном времени и итоговый счёт после приземления.
    /// Только Unity API + чтение готовых значений сервисов — никакой бизнес-логики подсчёта.
    /// </summary>
    public sealed class HighJumpDebugOverlayController : MonoBehaviour
    {
        [SerializeField] private Text _text;

        private IDriverFlightService _flight;
        private IMetaProgressService _meta;
        private IEventBus _eventBus;
        private bool _initialized;

        // Кэш последних значений из событий — обновляется подписками, читается в Update.
        private int _lastFlipCount;
        private bool _scored;
        private int _lastScore;
        private bool _lastIsNewRecord;

        [Inject]
        public void Construct(IDriverFlightService flight, IMetaProgressService meta, IEventBus eventBus)
        {
            _flight = flight;
            _meta = meta;
            _eventBus = eventBus;
            _initialized = true;

            _eventBus.Subscribe<DriverLandedEvent>(OnDriverLanded);
            _eventBus.Subscribe<StuntsScoredEvent>(OnStuntsScored);

            // Оформление кодом, чтобы не пересобирать сцену; временный оверлей, не конфигурируем через SO.
            if (_text != null)
            {
                _text.color = Color.black;
                _text.fontSize = 32;
                // Overflow — чтобы крупный шрифт не обрезался фиксированным rect'ом текста.
                _text.horizontalOverflow = HorizontalWrapMode.Overflow;
                _text.verticalOverflow = VerticalWrapMode.Overflow;
            }
        }

        private void OnDriverLanded(DriverLandedEvent e)
        {
            _lastFlipCount = e.FlipCount;
        }

        private void OnStuntsScored(StuntsScoredEvent e)
        {
            _scored = true;
            _lastScore = e.Score;
            _lastIsNewRecord = e.IsNewRecord;
        }

        private void Update()
        {
            if (!_initialized) return;
            if (_text == null) return;

            if (_scored)
            {
                string recordSuffix = _lastIsNewRecord ? " [NEW RECORD]" : string.Empty;
                _text.text = $"Счёт: {_lastScore} | Рекорд высоты: {_meta.Profile.BestHighJumpHeight:F1} м{recordSuffix} — R: заново";

                if (UnityEngine.Input.GetKeyDown(KeyCode.R))
                    SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);

                return;
            }

            _text.text = $"Высота (пик): {_flight.PeakHeight:F1} м | Время полёта: {_flight.AirTime:F1} с | Флипы: {_lastFlipCount}";
        }

        private void OnDestroy()
        {
            if (_eventBus == null) return;

            _eventBus.Unsubscribe<DriverLandedEvent>(OnDriverLanded);
            _eventBus.Unsubscribe<StuntsScoredEvent>(OnStuntsScored);
        }
    }
}
