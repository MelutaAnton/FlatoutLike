using UnityEngine;
using UnityEngine.SceneManagement;

namespace FlatOutLike.Services.Flow
{
    /// <summary>
    /// Тонкая обёртка над SceneManager. Единственное место в проекте, где вызывается
    /// SceneManager.LoadScene — межэкранные переходы идут только через ISceneLoaderService.
    /// </summary>
    public sealed class SceneLoaderService : ISceneLoaderService
    {
        public string ActiveSceneName => SceneManager.GetActiveScene().name;

        public void Load(string sceneKey)
        {
            if (ActiveSceneName == sceneKey)
            {
                // Сцена уже активна (например, GameLifetimeScope стартовал прямо в ней) — повторная
                // загрузка пересоздала бы весь Composition Root и сбросила рантайм-состояние.
                Debug.Log($"[SceneLoader] Сцена '{sceneKey}' уже активна — загрузка пропущена.");
                return;
            }

            Debug.Log($"[SceneLoader] Загрузка сцены '{sceneKey}'.");
            SceneManager.LoadScene(sceneKey, LoadSceneMode.Single);
        }
    }
}
