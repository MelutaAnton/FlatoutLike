using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer.Unity;

namespace FlatOutLike.Services.Flow
{
    /// <summary>
    /// Тонкая обёртка над SceneManager. Единственное место в проекте, где вызывается
    /// SceneManager.LoadScene* — межэкранные переходы идут только через ISceneLoaderService.
    /// Переход асинхронный и двухфазный (scope per-scene, без DontDestroyOnLoad): старая сцена
    /// затемняется через IScreenFader → LoadSceneAsync(Single); новая сцена стартует уже чёрной
    /// (префаб фейда сериализован с alpha=1) и проявляется в Start() своего SceneLoaderService.
    /// </summary>
    public sealed class SceneLoaderService : ISceneLoaderService, IStartable
    {
        private readonly IScreenFader _fader;

        // Загрузка начата (идёт затемнение или асинхронная загрузка) — повторные запросы
        // игнорируются, а Start() не проявляет экран поверх начатого затемнения.
        private bool _loading;

        public string ActiveSceneName => SceneManager.GetActiveScene().name;

        public SceneLoaderService(IScreenFader fader)
        {
            _fader = fader;
        }

        public void Start()
        {
            // КРИТИЧНО: в Boot GameFlowService уже при сборке контейнера вызвал Load
            // (SceneEntryState.Enter начального состояния) — затемнение началось ДО Start().
            // FadeIn здесь отменил бы его и показал бы Boot вместо чёрного перехода.
            if (!_loading)
                _fader.FadeIn();
        }

        public void Load(string sceneKey)
        {
            if (ActiveSceneName == sceneKey)
            {
                // Сцена уже активна (например, GameLifetimeScope стартовал прямо в ней) — повторная
                // загрузка пересоздала бы весь Composition Root и сбросила рантайм-состояние.
                Debug.Log($"[SceneLoader] Сцена '{sceneKey}' уже активна — загрузка пропущена.");
                return;
            }

            if (_loading)
            {
                // Двойной тап / повторный переход во время затемнения — первая загрузка выигрывает.
                Debug.Log($"[SceneLoader] Загрузка уже идёт — запрос '{sceneKey}' пропущен.");
                return;
            }

            _loading = true;
            Debug.Log($"[SceneLoader] Загрузка сцены '{sceneKey}' (через фейд).");
            _fader.FadeOut(() => BeginAsyncLoad(sceneKey));
        }

        private void BeginAsyncLoad(string sceneKey)
        {
            var operation = SceneManager.LoadSceneAsync(sceneKey, LoadSceneMode.Single);
            if (operation != null)
                return;

            // Сцена не найдена в Build Settings — SceneManager уже залогировал ошибку.
            // Снимаем блокировку и проявляем текущую сцену, чтобы игрок не застрял на чёрном экране.
            Debug.LogError($"[SceneLoader] Не удалось начать загрузку сцены '{sceneKey}' — возврат из чёрного.");
            _loading = false;
            _fader.FadeIn();
        }
    }
}
