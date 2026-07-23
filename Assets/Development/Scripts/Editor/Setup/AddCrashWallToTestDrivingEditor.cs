#if UNITY_EDITOR
namespace FlatOutLike.Editor.Setup
{
    /// <summary>
    /// Пункт меню «Add Crash Wall to Test Driving» удалён при чистке меню (2026-07-24):
    /// одноразовый хелпер тест-сцены шага 3 (_TestDriving), больше не нужен.
    /// Класс сохранён ради константы: CreatePhysicsLayersEditor назначает слой Wall
    /// объекту с этим именем, если стена ещё встречается в сцене.
    /// </summary>
    public static class AddCrashWallToTestDrivingEditor
    {
        // internal — чтобы CreatePhysicsLayersEditor мог назначить слой Wall по тому же имени
        internal const string WallName = "CrashTestWall";
    }
}
#endif
