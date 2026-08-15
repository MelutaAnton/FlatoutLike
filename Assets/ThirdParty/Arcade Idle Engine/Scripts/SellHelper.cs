using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace AIE
{
    public class SellHelper : MonoBehaviour
    {
        public Timer TheTimer;
        float WaitTime;
        float ReleaseTimestamp;
        public Animator Anim;
        float CollectTimestamp;
        void Start()
        {

        }

        void Update()
        {

            if (TheTimer)
            {
                TheTimer.AddTime();

                if (!TheTimer.IsFilled())
                {
                    return;
                }
                else
                {
                    // TheTimer.ResetTimer();
                }
            }

            if (TheTimer.transform.GetComponentInParent<Counter>())
            {
                TheTimer.transform.GetComponentInParent<Counter>().ActivateSelling(false);
            }
            else if (TheTimer.transform.GetComponentInParent<CarCounter>())
            {
                TheTimer.transform.GetComponentInParent<CarCounter>().ActivateSelling(false);
            }

        }
        public void SellAction()
        {
            Anim.Play("Bow");
        }
    }
}
