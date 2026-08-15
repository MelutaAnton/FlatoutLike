using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace AIE
{
    public class HoverBoard : MonoBehaviour
    {
        public Rotate[] Wheels;
        public void SetSpeed(float Amount)
        {
            for (int i = 0; i < Wheels.Length; i++)
            {
                Wheels[i].Speed = Amount;
            }
        }
    }
}
