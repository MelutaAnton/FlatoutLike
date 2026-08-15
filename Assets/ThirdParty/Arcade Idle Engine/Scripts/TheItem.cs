using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
namespace AIE
{
    [System.Serializable]
public class OnStepChange : UnityEvent { }
public enum Step
{
    Default,
    Packed,
    Trash
}
    public class TheItem : MonoBehaviour
    {
        public string ItemName = "Default_Item";
        public Step ActiveStep;
        public OnStepChange OnStepChanged;
        public bool IsMoving;
        public bool StackMoving;
        public bool StackRefreshed;
        float duration = 1;
        float VectorArc = 0.4f;
        float MinDist = 0.5f;
        public float MoveTimestamp;
        public void NextStep()
        {
            int CurrentStep = (int)ActiveStep;
            CurrentStep += 1;
            if (CurrentStep > System.Enum.GetValues(typeof(Step)).Length - 1)
            {
                CurrentStep = System.Enum.GetValues(typeof(Step)).Length - 1;
            }
            ActiveStep = (Step)CurrentStep;

        }
        public bool CanMove(float SafeTime=1)
        {
            if (MoveTimestamp > Time.time-SafeTime)
            {
                return false;
            }
            return true;
        }
        public bool SetStep(Step Which)
        {
            if (ActiveStep == Which)
            {
                return false;
            }
            ActiveStep = Which;
            OnStepChanged.Invoke();
            return true;
        }
        public void StopCorutines()
        {
            IsMoving = false;
            StopAllCoroutines();
        }
        public void MoveToPos(Vector3 Target)
        {
            StackRefreshed = true;
            if (StackMoving)
                return;
            StopCorutines();
            //StopCoroutine(_MoveToPos(Target));
            StartCoroutine(_MoveToPos(Target));
        }
        IEnumerator _MoveToPos(Vector3 Target)
        {
            MoveTimestamp = Time.time;
            StackMoving = true;




            float T = 0;

            Vector3 Pos = Target;



            bool IsDone = false;
            while (!IsDone)
            {

                T += Time.deltaTime;
                float t01 = T / duration;
                Pos = Target;
                Vector3 Pos_A = transform.position;
                Vector3 Pos_B = Pos;
                Vector3 pos = Vector3.MoveTowards(Pos_A, Pos_B, t01);

                Vector3 Arc = Mathf.Sin(t01 * 3.14f) * VectorArc * Vector3.up;

                transform.position = pos + Arc;

                float TheDist = Vector3.Distance(transform.position, Pos);
                if (TheDist < MinDist && T > duration / 2 && StackMoving ||
                    T > duration)
                {
                    IsDone = true;
                }
                yield return new WaitForSeconds(0.00000001f); ;
            }





            transform.position = Target;

            StackMoving = false;

        }


        public void MoveToTarget(Transform Target, bool DeactivateOnArrive = false)
        {
            MoveTimestamp = Time.time;
            StackMoving = false;
            StackRefreshed = false;
            IsMoving = false;
            StopCorutines();
            //StopCoroutine(_MoveToTarget(Target));
            StartCoroutine(_MoveToTarget(Target, DeactivateOnArrive));
        }
        IEnumerator _MoveToTarget(Transform Target, bool DeactivateOnArrive = false)
        {
            if (Target.GetComponentInParent<StackRemover>() &&
               Target.GetComponentInParent<StackRemover>().TheType == RemoverType.Dustbin)
            {
                transform.SetParent(Target.GetComponentInParent<StackRemover>().transform);

            }
            IsMoving = true;



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

                float TheDist = Vector3.Distance(transform.position, Pos);
                if (TheDist < MinDist && T > duration / 2 ||
                    T > duration)
                {
                    IsDone = true;
                }
                yield return new WaitForSeconds(0.00000001f); ;
            }

            //float Dist = Vector3.Distance(transform.position, Target.position);
            //while (Dist > 0.1f)
            //{
            //    Dist = Vector3.Distance(transform.position, Target.position);

            //    transform.position = Vector3.Lerp(transform.position, Target.position, 5 * Time.deltaTime);
            //    //if (Target.GetComponentInParent<CupManager>())
            //    //{


            //    //}
            //    //else
            //    //{
            //    //    Dist = 0;
            //    //}
            //    yield return new WaitForSeconds(0.0000001f);
            //}

            transform.position = Target.position;
            transform.eulerAngles = Target.eulerAngles;
            IsMoving = false;
            if (Target.GetComponentInParent<StackRemover>() &&
                Target.GetComponentInParent<StackRemover>().TheType == RemoverType.Dustbin
               || DeactivateOnArrive)
            {
                // Destroy(gameObject);
                gameObject.SetActive(false);
            }
            else
            {
                if (Target.GetComponentInParent<SlotManager>())
                {
                    Target.GetComponentInParent<SlotManager>().Save();
                }
            }

        }
        public void DestroyItem()
        {
            StartCoroutine(DestroyItem(gameObject));
        }
        IEnumerator DestroyItem(GameObject Which)
        {
            Vector3 OrigiScale = Which.transform.localScale;
            //Renderer[] rends = Which.GetComponentsInChildren<Renderer>();
            //for (int i = 0; i < rends.Length; i++)
            //{
            //    rends[i].material.color = Color.red;
            //}
            //Destroy(Which, 4f);
            Which.transform.SetParent(null);
            float TheT = Time.time + 3;
            while (TheT > Time.time)
            {
                Which.transform.position = Vector3.MoveTowards(Which.transform.position, Camera.main.transform.position, 5 * Time.deltaTime);
                Which.transform.localScale = Vector3.MoveTowards(Which.transform.localScale, Vector3.zero, 2 * Time.deltaTime);
                yield return new WaitForSeconds(0.00001f);
            }
            //Destroy(Which);
            gameObject.SetActive(false);
            Which.transform.localScale = OrigiScale;
        }
    }
}
