namespace FlatOutLike.Models
{
    /// <summary>
    /// Единственный источник соответствия GameState ↔ сцена. Все места, которым нужно узнать
    /// "в какой сцене живёт это состояние" или "с какого состояния стартовать в этой сцене",
    /// обязаны обращаться сюда — дублирование этой связи по местам запрещено.
    /// </summary>
    public static class GameStateSceneMap
    {
        /// <summary>
        /// Сцена, соответствующая состоянию, или null, если состояние не привязано к отдельной
        /// сцене и живёт внутри текущей (Flight/Scoring/MatchEnd/Paused).
        /// </summary>
        public static string SceneFor(GameState state)
        {
            switch (state)
            {
                case GameState.MainMenu:              return SceneKeys.MainMenu;
                case GameState.GarageMeta:             return SceneKeys.Garage;
                case GameState.StuntsHub:              return SceneKeys.StuntsHub;
                case GameState.DerbyMatch:              return SceneKeys.DerbyArena01;
                case GameState.StuntsHighJumpDriving:  return SceneKeys.StuntsHighJump;
                default:                                return null;
            }
        }

        /// <summary>
        /// Состояние, с которого GameFlowService должен стартовать в этой сцене.
        /// Boot сразу уводит в главное меню — самостоятельного состояния для Boot нет.
        /// Неизвестная сцена (тестовые "_"-сцены) даёт MainMenu без исключения —
        /// вызывающая сторона может залогировать это отдельно, если нужно.
        /// </summary>
        public static GameState InitialStateFor(string sceneName)
        {
            if (sceneName == SceneKeys.Boot)               return GameState.MainMenu;
            if (sceneName == SceneKeys.MainMenu)            return GameState.MainMenu;
            if (sceneName == SceneKeys.Garage)              return GameState.GarageMeta;
            if (sceneName == SceneKeys.StuntsHub)           return GameState.StuntsHub;
            if (sceneName == SceneKeys.DerbyArena01)        return GameState.DerbyMatch;
            if (sceneName == SceneKeys.StuntsHighJump)      return GameState.StuntsHighJumpDriving;

            return GameState.MainMenu;
        }
    }
}
