namespace FlatOutLike.Services.Dev
{
    /// <summary>
    /// Счётчик комбинации «N одинаковых нажатий подряд» для dev-читов. Pure C#, без UnityEngine:
    /// время нажатия передаёт вызывающий, поэтому логика тестируется без Unity.
    /// Нажатие, пришедшее позже <c>maxIntervalSeconds</c> после предыдущего, начинает серию заново
    /// (считается первым). После срабатывания счёт начинается с нуля.
    /// </summary>
    public sealed class CheatSequence
    {
        private readonly int _requiredPresses;
        private readonly float _maxIntervalSeconds;

        private int _count;
        private float _lastPressTime;

        public CheatSequence(int requiredPresses, float maxIntervalSeconds)
        {
            _requiredPresses = requiredPresses < 1 ? 1 : requiredPresses;
            _maxIntervalSeconds = maxIntervalSeconds < 0f ? 0f : maxIntervalSeconds;
        }

        /// <summary>
        /// Регистрирует нажатие в момент <paramref name="time"/> (секунды).
        /// Возвращает true ровно на N-м нажатии серии; после этого серия сбрасывается.
        /// </summary>
        public bool RegisterPress(float time)
        {
            // Слишком долгая пауза — текущее нажатие открывает новую серию.
            if (_count > 0 && time - _lastPressTime > _maxIntervalSeconds)
                _count = 0;

            _count++;
            _lastPressTime = time;

            if (_count < _requiredPresses)
                return false;

            _count = 0;
            return true;
        }

        /// <summary>Сбрасывает серию (например, нажата посторонняя клавиша).</summary>
        public void Reset()
        {
            _count = 0;
        }
    }
}
