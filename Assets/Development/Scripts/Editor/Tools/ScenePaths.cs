#if UNITY_EDITOR
namespace FlatOutLike.Editor.Tools
{
    /// <summary>
    /// Единственное место в editor-коде, где собираются пути к .unity-сценам проекта.
    /// Ключи сцен берутся из <c>FlatOutLike.Models.SceneKeys</c> (единый источник имён),
    /// здесь — только папка и расширение.
    /// </summary>
    public static class ScenePaths
    {
        public const string Folder = "Assets/Development/Scenes/";

        /// <summary>Путь к сцене по её ключу (SceneKeys.*) или имени временной сцены.</summary>
        public static string For(string sceneKey) => Folder + sceneKey + ".unity";
    }
}
#endif
