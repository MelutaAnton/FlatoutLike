using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace AIE
{
    public class OffloadPoint : MonoBehaviour
    {
        public float UsedTimestamp;
        GameObject theowner;

        public bool CanUse(GameObject Owner)
        {
            UsedTimestamp = Time.time + 1;

            if (theowner == null)
            {
                theowner = Owner;
            }
            if (theowner == Owner)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        private void Update()
        {
            if (UsedTimestamp < Time.time)
            {
                theowner = null;
            }
        }
    }
}
