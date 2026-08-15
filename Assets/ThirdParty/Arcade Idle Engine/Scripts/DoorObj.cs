using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace AIE
{
    public class DoorObj : MonoBehaviour
    {
        public Vector3 FrontOpenRot;
        public Vector3 BackOpenRot;
        public bool IsOpen;
        public bool IsFront;
        public float OpenSpeed = 5;
        float opentimestamp;
        float closedtimestamp;
        float BounceTimestamp;
        Vector3 Bounce;
        float range;
        public float Maxrange = 20;
        float multiplier = 1;
        public Vector3 TheBounce = new Vector3(0, 1, 0);
        private void Start()
        {
            this.enabled = false;
        }
        void Update()
        {
            if (opentimestamp < Time.time)
            {
                IsOpen = false;
                range = Maxrange;
            }
            if (IsOpen)
            {
                if (IsFront)
                {
                    transform.localRotation = Quaternion.Slerp(transform.localRotation, Quaternion.Euler(FrontOpenRot + Bounce), OpenSpeed * Time.deltaTime);
                }
                else
                {
                    transform.localRotation = Quaternion.Slerp(transform.localRotation, Quaternion.Euler(BackOpenRot + Bounce), OpenSpeed * Time.deltaTime);
                }
                closedtimestamp = 0;
                if (BounceTimestamp < Time.time)
                {
                    multiplier = -multiplier;
                    float tempbounce = range * multiplier;
                    Bounce = TheBounce * tempbounce;
                    //Bounce = new Vector3(0,range*multiplier, 0);
                    BounceTimestamp = Time.time + 0.2f;
                    if (range > 0)
                    {
                        range -= 5;
                    }
                }
            }
            else
            {
                if (BounceTimestamp < Time.time)
                {
                    multiplier = -multiplier;
                    //Bounce = new Vector3(0,range * multiplier, 0);
                    float tempbounce = range * multiplier;
                    Bounce = TheBounce * tempbounce;
                    BounceTimestamp = Time.time + 0.2f;
                    if (range > 0)
                    {
                        range -= 5;
                    }
                }
                transform.localRotation = Quaternion.Slerp(transform.localRotation, Quaternion.Euler(Vector3.zero + Bounce), OpenSpeed * Time.deltaTime);
                closedtimestamp += Time.deltaTime;
                if (closedtimestamp > 1)
                {
                    this.enabled = false;
                    transform.localRotation = Quaternion.Euler(Vector3.zero);
                }
                GetComponentInParent<Door>().CloseDoor();

            }
        }
        public void Open(bool isfront)
        {
            this.enabled = true;
            opentimestamp = Time.time + 0.5f;
            if (IsOpen)
                return;
            IsOpen = true;
            IsFront = isfront;
            range = Maxrange;

        }
    }
}