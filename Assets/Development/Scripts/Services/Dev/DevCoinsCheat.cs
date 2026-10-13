#if UNITY_EDITOR
using FlatOutLike.Services.Meta;
using UnityEngine;
using VContainer.Unity;

namespace FlatOutLike.Services.Dev
{
    /// <summary>
    /// Dev-чит на монеты для плейтеста в редакторе: быстро набрать баланс для проверки
    /// покупок, прокачки и стройки хаба без прохождения матчей.
    /// <para>
    /// Как пользоваться: в Play Mode нажать «5» четыре раза подряд (верхний ряд <c>Alpha5</c>
    /// или нумпад <c>Keypad5</c>, можно вперемешку) с паузой между нажатиями не больше
    /// <see cref="MaxIntervalSeconds"/> — на баланс зачисляется <see cref="CoinsAmount"/> монет
    /// через <see cref="IEconomyService.Credit"/>. Любая другая клавиша или кнопка мыши сбрасывает серию.
    /// Работает во всех сценах, на паузе (время — <c>Time.unscaledTime</c>, а ITickable VContainer
    /// не зависит от timeScale) и при мобильном вводе (читает клавиатуру напрямую, мимо IInputProvider).
    /// </para>
    /// <para>
    /// Почему editor-only: весь файл и его регистрация в <c>GameLifetimeScope</c> под
    /// <c>#if UNITY_EDITOR</c> — в билд чит не попадает физически, игрок не накрутит монеты.
    /// </para>
    /// </summary>
    public sealed class DevCoinsCheat : ITickable
    {
        private const int RequiredPresses = 4;
        private const float MaxIntervalSeconds = 1.5f;
        private const int CoinsAmount = 1000;

        private readonly IEconomyService _economy;
        private readonly CheatSequence _sequence = new CheatSequence(RequiredPresses, MaxIntervalSeconds);

        public DevCoinsCheat(IEconomyService economy)
        {
            _economy = economy;
        }

        public void Tick()
        {
            bool fivePressed = UnityEngine.Input.GetKeyDown(KeyCode.Alpha5)
                               || UnityEngine.Input.GetKeyDown(KeyCode.Keypad5);

            if (fivePressed)
            {
                if (_sequence.RegisterPress(Time.unscaledTime))
                {
                    _economy.Credit(CoinsAmount);
                    Debug.Log($"[Cheat] +{CoinsAmount} coins");
                }
            }
            else if (UnityEngine.Input.anyKeyDown)
            {
                // Посторонняя клавиша или кнопка мыши прерывает серию.
                _sequence.Reset();
            }
        }
    }
}
#endif
