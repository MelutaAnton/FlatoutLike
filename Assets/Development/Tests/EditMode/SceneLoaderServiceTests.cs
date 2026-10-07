using System;
using FlatOutLike.Core.Events;
using FlatOutLike.Data.Configs.Flow;
using FlatOutLike.Services.Flow;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FlatOutLike.Tests
{
    /// <summary>
    /// Юнит-тесты подшага 11d: SceneLoaderService закрывает новую сцену экраном загрузки уже
    /// в конструкторе (Cover), запускает загрузку только через Show, игнорирует повторные
    /// запросы во время перехода и не открывает сцену в Start(), если переход уже начался при
    /// сборке контейнера (Boot). Фейк-экран НЕ вызывает onShown — иначе в EditMode дёрнулся бы
    /// реальный SceneManager.LoadSceneAsync.
    /// </summary>
    [TestFixture]
    public class SceneLoaderServiceTests
    {
        // Ключ, заведомо не совпадающий с активной сценой EditMode-раннера.
        private const string OtherSceneKey = "__SceneLoaderServiceTests_OtherScene__";

        private FakeLoadingScreen _screen;
        private LoadingScreenConfig _config;
        private SceneLoaderService _loader;

        [SetUp]
        public void SetUp()
        {
            _screen = new FakeLoadingScreen();
            _config = ScriptableObject.CreateInstance<LoadingScreenConfig>();
            _loader = new SceneLoaderService(_screen, _config, new EventBus());
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_config);
        }

        [Test]
        public void Constructor_CoversSceneOnce()
        {
            Assert.AreEqual(1, _screen.CoverCalls);
        }

        [Test]
        public void Load_ActiveScene_DoesNotShow()
        {
            _loader.Load(SceneManager.GetActiveScene().name);

            Assert.AreEqual(0, _screen.ShowCalls);
            Assert.IsFalse(_loader.IsLoading);
        }

        [Test]
        public void Load_OtherScene_ShowsOnceAndSetsIsLoading()
        {
            _loader.Load(OtherSceneKey);

            Assert.AreEqual(1, _screen.ShowCalls);
            Assert.IsTrue(_loader.IsLoading);
        }

        [Test]
        public void Load_WhileLoadingInProgress_IsIgnored()
        {
            _loader.Load(OtherSceneKey);
            _loader.Load(OtherSceneKey + "_2");

            Assert.AreEqual(1, _screen.ShowCalls);
        }

        [Test]
        public void Start_WithoutLoad_HidesOnce()
        {
            _loader.Start();

            Assert.AreEqual(1, _screen.HideCalls);
        }

        [Test]
        public void Start_AfterLoad_DoesNotHide()
        {
            _loader.Load(OtherSceneKey);
            _loader.Start();

            Assert.AreEqual(0, _screen.HideCalls);
        }

        [Test]
        public void Tick_WithoutLoad_DoesNotTouchScreen()
        {
            _loader.Tick();

            Assert.AreEqual(0, _screen.SetProgressCalls);
        }

        [Test]
        public void NullLoadingScreen_Show_InvokesCallbackImmediately()
        {
            var screen = new NullLoadingScreen();
            int calls = 0;

            screen.Show("Title", "Hint", () => calls++);

            Assert.AreEqual(1, calls);
        }

        [Test]
        public void NullLoadingScreen_Show_NullCallback_DoesNotThrow()
        {
            var screen = new NullLoadingScreen();

            Assert.DoesNotThrow(() => screen.Show("Title", "Hint", null));
        }

        /// <summary>
        /// Считает вызовы и никогда не завершает проявление (onShown не вызывается).
        /// </summary>
        private sealed class FakeLoadingScreen : ILoadingScreen
        {
            public int CoverCalls { get; private set; }
            public int ShowCalls { get; private set; }
            public int SetProgressCalls { get; private set; }
            public int HideHintCalls { get; private set; }
            public int HideCalls { get; private set; }

            public void Cover(string title) => CoverCalls++;

            public void Show(string title, string hint, Action onShown) => ShowCalls++;

            public void SetProgress(float progress01) => SetProgressCalls++;

            public void HideHint() => HideHintCalls++;

            public void Hide() => HideCalls++;
        }
    }
}
