using UnityEngine;

namespace ithappy.Battle_Stadium
{
    public enum DoorActivationMode
    {
        TopOnly,
        BottomOnly,
        Both,
        AnyPosition,
        Custom
    }

    public class DoorComponentBase : MonoBehaviour
    {
        public bool IsAtTop { get; protected set; }
        public bool IsAtBottom { get; protected set; }
    }
}
