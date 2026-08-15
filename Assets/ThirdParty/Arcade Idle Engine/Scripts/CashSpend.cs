using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace AIE
{
    public class CashSpend : MonoBehaviour
    {
        public Transform Target;
        float WaitTimestamp;
        public float Speed = 20;
        Vector3 randpos;
        float randtimestamp;
        bool ingamecash;
        float duration = 2;
        float VectorArc = 0.4f;
        float T = 0;

        public int ChangeAmount = 1;
        public void SetUp(Transform _Target, float Which, bool InGameCash, int changeamount)
        {
            T = 0;
            Target = _Target;
            WaitTimestamp = Time.time + 0.4f + Which / 100;
            ingamecash = InGameCash;
            ChangeAmount = changeamount;

        }
        void Update()
        {

            bool IsDone = false;
            if (!IsDone)
            {

                T += Time.deltaTime;
                float t01 = T / duration;
                Vector3 Pos = Target.position;
                Vector3 Pos_A = transform.position;
                Vector3 Pos_B = Pos;
                Vector3 pos = Vector3.MoveTowards(Pos_A, Pos_B, t01);

                Vector3 Arc = Mathf.Sin(t01 * 3.14f) * VectorArc * Vector3.up;

                transform.position = pos + Arc;
                transform.Rotate(Vector3.up * 100 * Time.deltaTime);

                float TheDist = Vector3.Distance(transform.position, Pos);
                if (TheDist < 0.5f && T > duration / 2)
                {
                    IsDone = true;
                }
            }

            if (IsDone)
            {
                gameObject.SetActive(false);
            }
            // transform.localScale = Vector3.Lerp(transform.localScale, Vector3.zero, 1 * Time.deltaTime);


        }
    }
}
