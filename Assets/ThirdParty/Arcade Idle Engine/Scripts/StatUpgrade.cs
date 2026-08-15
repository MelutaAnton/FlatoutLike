using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace AIE
{
    public class StatUpgrade : MonoBehaviour
    {
        public ProgressBtn[] Btns;
        public void SetLevel(int Which)
        {
            for (int I = 0; I < Btns.Length; I++)
            {
                if (Which > I)
                {
                    Btns[I].SetOn(true);
                }
                else
                {
                    Btns[I].SetOn(false);
                }
            }
        }
    }
}
