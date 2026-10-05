namespace FlatOutLike.Core.Input
{
    public enum InputContext
    {
        Driving,
        Flight,
        UI,

        // BaseHub: персонаж бегает по базе — только Move (полный вектор), без машинных действий.
        Hub
    }
}
