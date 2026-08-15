using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace AIE
{
    public class EconomyMan : MonoBehaviour
    {
        [Header("Npc Counter")]
        public int Npc_Counter_Price = 3;
        public int Vip_Counter_Price = 6;

        [Header("Car Counter")]
        public int Car_Counter_Price = 4;

        [Header("Dining Table")]
        public bool CanTip = true;
        public int TipsAmount = 3;
    }
}
