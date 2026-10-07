using System;

namespace FlatOutLike.Services.Flow
{
    /// <summary>
    /// Отображаемый прогресс экрана загрузки (pure C#, тестируется без Unity). Реальный
    /// AsyncOperation.progress скачет и замирает на 0.9, поэтому полоса ведётся фейковой
    /// кривой: ease-out 0 → fakeTarget за fakeSeconds, затем, пока сцена не готова, медленный
    /// ease-out fakeTarget → creepTarget за creepSeconds (выше не поднимается). Когда сцена
    /// готова и выдержан минимум показа, полоса линейно доходит до 1 за finishSeconds.
    /// Отображаемое значение никогда не убывает.
    /// </summary>
    public sealed class LoadingProgressModel
    {
        private readonly float _fakeTarget;
        private readonly float _fakeSeconds;
        private readonly float _creepTarget;
        private readonly float _creepSeconds;
        private readonly float _finishSeconds;
        private readonly float _minSeconds;

        private float _elapsed;
        private float _finishElapsed;
        private float _finishFrom;

        public float Displayed { get; private set; }
        public bool IsFinishing { get; private set; }
        public bool IsComplete => Displayed >= 1f;

        public LoadingProgressModel(float fakeTarget, float fakeSeconds, float creepTarget,
            float creepSeconds, float finishSeconds, float minSeconds)
        {
            // Нормализация входа: цели в [0..1], creep не ниже fake; отрицательные длительности = 0.
            _fakeTarget = Clamp01(fakeTarget);
            _creepTarget = Math.Max(_fakeTarget, Clamp01(creepTarget));
            _fakeSeconds = Math.Max(0f, fakeSeconds);
            _creepSeconds = Math.Max(0f, creepSeconds);
            _finishSeconds = Math.Max(0f, finishSeconds);
            _minSeconds = Math.Max(0f, minSeconds);
        }

        /// <summary>
        /// Продвинуть прогресс на deltaTime. sceneReady — сцена загружена и ждёт только активации.
        /// </summary>
        public void Advance(float deltaTime, bool sceneReady)
        {
            float dt = Math.Max(0f, deltaTime);
            _elapsed += dt;

            if (!IsFinishing)
            {
                Displayed = Math.Max(Displayed, FakeValueAt(_elapsed));

                if (!sceneReady || _elapsed < _minSeconds)
                    return;

                // Рывок к 100% стартует с текущего значения; dt этого тика уже ушёл на фейк-кривую.
                IsFinishing = true;
                _finishFrom = Displayed;
                _finishElapsed = 0f;
            }
            else
            {
                _finishElapsed += dt;
            }

            float fraction = _finishSeconds <= 0f ? 1f : Clamp01(_finishElapsed / _finishSeconds);

            // Конец рывка — ровно 1: a + (1 - a) во float может дать 0.99999994, и IsComplete
            // никогда не наступил бы.
            float finishValue = fraction >= 1f
                ? 1f
                : _finishFrom + (1f - _finishFrom) * fraction;

            Displayed = Math.Max(Displayed, Clamp01(finishValue));
        }

        private float FakeValueAt(float time)
        {
            if (_fakeSeconds > 0f && time < _fakeSeconds)
                return _fakeTarget * EaseOut(time / _fakeSeconds);

            if (_creepSeconds <= 0f)
                return _creepTarget;

            float creepTime = time - _fakeSeconds;
            return _fakeTarget + (_creepTarget - _fakeTarget) * EaseOut(Clamp01(creepTime / _creepSeconds));
        }

        // Квадратичный ease-out: быстрый старт, плавное подползание к цели.
        private static float EaseOut(float t)
        {
            float inv = 1f - Clamp01(t);
            return 1f - inv * inv;
        }

        private static float Clamp01(float value) =>
            value < 0f ? 0f : (value > 1f ? 1f : value);
    }
}
