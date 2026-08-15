using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace AIE
{
    public class Dustbin : MonoBehaviour
    {
        public Transform LidTrans;
        public Transform OpenTrans;
        public float Speed = 5;
        bool IsOpen;
        float timestamp;
        float closetime;
        private void Update()
        {
            if (IsOpen)
            {
                LidTrans.localRotation = Quaternion.Slerp(LidTrans.localRotation, OpenTrans.localRotation, Speed * Time.deltaTime);
                if (timestamp < Time.time)
                {
                    IsOpen = false;
                }
            }
            else
            {
                LidTrans.localRotation = Quaternion.Slerp(LidTrans.localRotation, Quaternion.Euler(Vector3.zero), Speed * Time.deltaTime);
                closetime += Time.deltaTime;
                if (closetime > 3)
                {
                    this.enabled = false;
                }
            }
        }
        public void OpenLid()
        {
            closetime = 0;
            timestamp = Time.time + 1f;
            IsOpen = true;
            this.enabled = true;
        }
    }
  
}
