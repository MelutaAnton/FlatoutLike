namespace FlatOutLike.Models
{
    /// <summary>
    /// Ось вращения, по которой <c>FlipDetectorService</c> засчитывает полный оборот (флип).
    /// Pitch/Roll/Yaw — локальные оси ragdoll-таза водителя.
    /// </summary>
    public enum FlipAxis
    {
        Pitch,
        Roll,
        Yaw
    }
}
