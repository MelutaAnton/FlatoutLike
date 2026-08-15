using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace AIE
{
    public class TheCash : MonoBehaviour
    {
        public bool Moving = false;
        bool init;
        Vector3 targetpos;
        private void OnEnable()
        {
            if (!init)
            {
                init = true;
            }
            else
            {
                if (targetpos != Vector3.zero)
                {
                    transform.position = targetpos;
                    Moving = false;
                }
            }
        }
        public void Collected()
        {
            //Invoke(nameof(CollectEnd), 2);
            StopAllCoroutines();
            Moving = true;
            if (_GameManager.instance.cashman.CollectCashUi)
            {
                CollectEnd();
            }
            else
            {
                StartCoroutine(CollectAnimate(_GameManager.instance.ThePlayer.transform));

            }
        }
        void CollectEnd()
        {
            if (_GameManager.instance.cashman.CollectCashUi)
            {
                _GameManager.instance.cashman.SpawnCash(Camera.main.WorldToScreenPoint(transform.position),
                    1, 1, false);
            }
            else
            {
                _GameManager.instance.cashman.AdddTotalCash(1);

            }
            //   Destroy(gameObject);
            Moving = false;
            init = false;
            //if (GetComponentInParent<Slot>().TheOwner)
            //{
            //    GetComponentInParent<Slot>().TheOwner = null;
            //}
            // GetComponent<SlotOwner>().TargetSlot.TheOwner = null;
            gameObject.SetActive(false);

            // GameManager.instance.cashman.SpawnCash(Camera.main.WorldToScreenPoint(transform.position),
            //     1, 1);
        }
        IEnumerator CollectAnimate(Transform TargetEnd)
        {

            float duration = 2;
            float VectorArc = 0.4f;
            float T = 0;

            Vector3 Pos = TargetEnd.position + new Vector3(0, 1, 0);



            bool IsDone = false;
            while (!IsDone)
            {

                T += Time.deltaTime;
                float t01 = T / duration;
                Pos = TargetEnd.position + new Vector3(0, 1, 0.5f);
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
                yield return new WaitForSeconds(0.00000001f); ;
            }
            CollectEnd();


        }
        public void MoveToTarget(Transform Target)
        {
            StopAllCoroutines();
            Moving = true;
            targetpos = Target.position;
            if (GetComponentInParent<CashStack>())
            {
                StartCoroutine(_MoveToTarget(Target));

            }
            else
            {
                transform.position = Target.position;
                Moving = false;
            }
        }
        IEnumerator _MoveToTarget(Transform Target)
        {
            Moving = true;
            //float Dist = Vector3.Distance(transform.position, Target.position);
            //while (Dist > 0.1f)
            //{
            //    Dist = Vector3.Distance(transform.position, Target.position);
            //    transform.position = Vector3.Lerp(transform.position, Target.position, 5 * Time.deltaTime);
            //    yield return new WaitForSeconds(0.0000001f);
            //}
            float duration = 1;
            float VectorArc = 0.4f;
            float T = 0;

            Vector3 Pos = Target.position;



            bool IsDone = false;
            while (!IsDone)
            {

                T += Time.deltaTime;
                float t01 = T / duration;
                Pos = Target.position;
                Vector3 Pos_A = transform.position;
                Vector3 Pos_B = Pos;
                Vector3 pos = Vector3.MoveTowards(Pos_A, Pos_B, t01);

                Vector3 Arc = Mathf.Sin(t01 * 3.14f) * VectorArc * Vector3.up;

                transform.position = pos + Arc;
                //transform.Rotate(Vector3.up * 100 * Time.deltaTime);

                float TheDist = Vector3.Distance(transform.position, Pos);
                if (TheDist < 0.5f && T > duration / 2)
                {
                    IsDone = true;
                }
                yield return new WaitForSeconds(0.00000001f); ;
            }




            transform.position = Target.position;

            Moving = false;
            if (Target.GetComponentInParent<SlotManager>())
            {
                Target.GetComponentInParent<SlotManager>().Save();
            }

        }
    }
}
