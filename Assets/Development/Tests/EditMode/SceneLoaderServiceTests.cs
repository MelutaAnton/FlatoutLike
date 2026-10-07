using System;
using FlatOutLike.Services.Flow;
using NUnit.Framework;
using UnityEngine.SceneManagement;

namespace FlatOutLike.Tests
{
    /// <summary>
    /// Юнит-тесты подшага 11c: SceneLoaderService запускает загрузку только через фейд,
    /// игнорирует повторные запросы во время перехода и не проявляет сцену в Start(),
    /// если затемнение уже началось при сборке контейнера (Boot). Фейк-фейдер НЕ вызывает
    /// onComplete — иначе в EditMode дёрнулся бы реальный SceneManager.LoadSceneAsync.
    /// </summary>
    [TestFixture]
    public class SceneLoaderServiceTests
    {
        // Ключ, заведомо не совпадающий с активной сценой EditMode-раннера.
        private const string OtherSceneKey = "__SceneLoaderServiceTests_OtherScene__";

        private FakeScreenFader _fader;
        private SceneLoaderService _loader;

        [SetUp]
        public void SetUp()
        {
            _fader = new FakeScreenFader();
            _loader = new SceneLoaderService(_fader);
        }

        [Test]
        public void Load_ActiveScene_DoesNotFadeOut()
        {
            _loader.Load(SceneManager.GetActiveScene().name);

            Assert.AreEqual(0, _fader.FadeOutCalls);
        }

        [Test]
        public void Load_OtherScene_FadesOutOnce()
        {
            _loader.Load(OtherSceneKey);

            Assert.AreEqual(1, _fader.FadeOutCalls);
        }

        [Test]
        public void Load_WhileFadeOutInProgress_IsIgnored()
        {
            _loader.Load(OtherSceneKey);
            _loader.Load(OtherSceneKey + "_2");

            Assert.AreEqual(1, _fader.FadeOutCalls);
        }

        [Test]
        public void Start_WithoutLoad_FadesInOnce()
        {
            _loader.Start();

            Assert.AreEqual(1, _fader.FadeInCalls);
        }

        [Test]
        public void Start_AfterLoad_DoesNotFadeIn()
        {
            _loader.Load(OtherSceneKey);
            _loader.Start();

            Assert.AreEqual(0, _fader.FadeInCalls);
        }

        [Test]
        public void NullScreenFader_FadeOut_InvokesCallbackImmediately()
        {
            var fader = new NullScreenFader();
            int calls = 0;

            fader.FadeOut(() => calls++);

            Assert.AreEqual(1, calls);
        }

        /// <summary>
        /// Считает вызовы и никогда не завершает затемнение (onComplete не вызывается).
        /// </summary>
        private sealed class FakeScreenFader : IScreenFader
        {
            public int FadeOutCalls { get; private set; }
            public int FadeInCalls { get; private set; }

            public void FadeOut(Action onComplete) => FadeOutCalls++;

            public void FadeIn() => FadeInCalls++;
        }
    }
}
