using System;
using FlatOutLike.Models;
using FlatOutLike.Services.Flow;

namespace FlatOutLike.Views.UI.MainMenu
{
    /// <summary>
    /// Presenter главного меню (pure C#). Клики View транслирует в переходы GameFlowService —
    /// ChangeState синхронно грузит целевую сцену через SceneEntryState/ISceneLoaderService
    /// (см. GameStateSceneMap), отдельный вызов загрузки сцены отсюда не нужен.
    /// </summary>
    public sealed class MainMenuPresenter : IDisposable
    {
        private readonly MainMenuView _view;
        private readonly IGameFlowService _gameFlow;

        public MainMenuPresenter(MainMenuView view, IGameFlowService gameFlow)
        {
            _view = view;
            _gameFlow = gameFlow;

            _view.OnDerbyClicked += HandleDerbyClicked;
            _view.OnStuntsClicked += HandleStuntsClicked;
            _view.OnGarageClicked += HandleGarageClicked;
        }

        public void Dispose()
        {
            _view.OnDerbyClicked -= HandleDerbyClicked;
            _view.OnStuntsClicked -= HandleStuntsClicked;
            _view.OnGarageClicked -= HandleGarageClicked;
        }

        private void HandleDerbyClicked() => _gameFlow.ChangeState(GameState.DerbyMatch);
        private void HandleStuntsClicked() => _gameFlow.ChangeState(GameState.StuntsHub);
        private void HandleGarageClicked() => _gameFlow.ChangeState(GameState.GarageMeta);
    }
}
