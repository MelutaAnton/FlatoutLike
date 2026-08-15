using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace AIE
{
    public class Bounce : MonoBehaviour
    {
        public void DelayBounce(float TheT)
        {
            Invoke(nameof(DoPulse), TheT);

        }
        public void _Bounce()
        {
            DoPulse();

        }
        public void DoPulse()
        {
            System.Collections.Hashtable hash =
                      new System.Collections.Hashtable();
            hash.Add("amount", new Vector3(1, 1, 1));
            hash.Add("time", 1.5f);

            //iTween.PunchScale(gameObject, hash);
        }
    }
}
