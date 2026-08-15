using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace AIE
{
    public class Stack : MonoBehaviour
    {
        [Header("Attributes")]
        public int MaxSize = 5;
        public float SwaySpeed = 20;
        public float spacing = 0.5f;
        public bool UseMax = true;

        [Header("Extra")]
        public AudioSource StackSound;
        public GameObject MaxObj;


        [Space(10)]
        public List<GameObject> Stacks = new List<GameObject>();
        [HideInInspector] public GameObject StacksParent;
        GameObject Notification;
        private void Start()
        {
            StacksParent = new GameObject("stackparent");
            if (GetComponentInParent<Player>())
            {
                GetComponentInParent<Player>().FullObj.SetActive(false);
            }
        }
        public bool IsFull()
        {
            if (Stacks.Count >= MaxSize)
            {
                if (MaxObj)
                {
                    MaxObj.SetActive(true);
                }
                return true;
            }

            return false;
        }
        public void AddStack(GameObject Which, bool ismoving = false)
        {
            if (Which.GetComponent<SlotOwner>().TargetSlot)
            {
                if (Which.GetComponent<SlotOwner>().TargetSlot.GetComponent<Chair>())
                {
                    Which.GetComponent<SlotOwner>().TargetSlot.GetComponent<Chair>().NotUsed();
                }
            }
            Stacks.Add(Which);
            Which.transform.parent = transform;
            Which.transform.localEulerAngles = new Vector3(0, 0, 0);
            //  Which.transform.localPosition = new Vector3(0, ((float)Stacks.Count-1)/2, 0);
            if (!ismoving)
            {
                if (Which.GetComponent<TheItem>())
                {
                    Which.GetComponent<TheItem>().StopCorutines();
                }
                Which.transform.localPosition = new Vector3(0, (spacing * (float)(Stacks.Count - 1)), 0);
            }

            Which.GetComponent<SlotOwner>().TargetSlot = null;
            if (StackSound)
            {
                StackSound.Play();
            }
            if (GetComponentInParent<Player>() && GetComponentInParent<Player>().type == TheType.Player)
            {
                if (Which.GetComponent<Crate>())
                {
                    _GameManager.instance.tasksMan.TaskProgressAdd(TaskType.CarryCrate, 1);
                }
                else if (Which.GetComponent<TheItem>())
                {
                    _GameManager.instance.tasksMan.TaskProgressAdd(TaskType.CarryItems, 1);
                }

            }
            RefreshMax();
        }
        public void Refresh()
        {
            for (int i = 1; i < Stacks.Count; i++)
            {
                Stacks[i].transform.position = transform.position + new Vector3(0, (spacing * (float)(i - 1)), 0);
            }
            Stacks[0].transform.localPosition = new Vector3(0, 0, 0);
        }
        void RefreshMax()
        {
            if (Stacks.Count >= MaxSize)
            {
                if (MaxObj)
                {
                    MaxObj.SetActive(true);
                }
            }
            else
            {
                if (MaxObj)
                {
                    MaxObj.SetActive(false);
                }
            }

        }
        public void DestroyLastStack()
        {
            if (Stacks.Count > 0)
            {
                GameObject Which = Stacks[Stacks.Count - 1];
                Stacks.RemoveAt(Stacks.Count - 1);
                StartCoroutine(DestroyItem(Which));
            }
        }

        public void SiphonStack(Stack Target)
        {
            if (Target.Stacks.Count > 0)
            {
                GameObject Which = Target.Stacks[Target.Stacks.Count - 1];
                if (!IsFull())
                {
                    Target.Stacks.RemoveAt(Target.Stacks.Count - 1);
                    AddStack(Which);
                    if (StackSound)
                    {
                        StackSound.Play();
                    }
                    if (GetComponentInParent<Player>() && GetComponentInParent<Player>().type == TheType.Player)
                    {
                        if (Which.GetComponent<Crate>())
                        {
                            _GameManager.instance.tasksMan.TaskProgressAdd(TaskType.CarryCrate, 1);
                        }
                        else if (Which.GetComponent<TheItem>())
                        {
                            _GameManager.instance.tasksMan.TaskProgressAdd(TaskType.CarryItems, 1);
                        }

                    }
                    //if (GetComponentInParent<Player>() && GetComponentInParent<Player>().type == TheType.Player)
                    //{
                    //    //Vibration.VibratePop();

                    //}
                }
                //else
                //{
                //    if (GetComponentInParent<Player>())
                //    {
                //        Target.Stacks.RemoveAt(Target.Stacks.Count - 1);

                //        StartCoroutine(DestroyItem(Which));
                //    }
                //}

            }

            RefreshMax();
        }
        public void RemoveStack(StackRemover Target)
        {
            if (Target.RejectPlayerStack && GetComponentInParent<Player>())
                return;
            if (Stacks.Count > 0)
            {
                GameObject Which = Stacks[Stacks.Count - 1];
                if (Target.TheType == RemoverType.Dustbin)
                {
                    Stacks.RemoveAt(Stacks.Count - 1);
                    if (StackSound)
                    {
                        StackSound.Play();
                    }
                    if (GetComponentInParent<Player>() && GetComponentInParent<Player>().type == TheType.Player)
                    {
                        //Vibration.VibratePop();
                    }
                    Which.transform.SetParent(null);
                    Which.GetComponent<TheItem>().MoveToTarget(Target.StackPos);
                    //Renderer[] rends = Which.GetComponentsInChildren<Renderer>();
                    //for (int i = 0; i < rends.Length; i++)
                    //{
                    //    rends[i].material.color = Color.red;
                    //}
                }

                else if (!Target.IsFull() &&Target.CanAdd(Which))// Which.GetComponent<TheItem>().ActiveStep == Target.TargetStep
                {
                    Stacks.RemoveAt(Stacks.Count - 1);
                    Target.AddStack(Which);
                    if (StackSound)
                    {
                        StackSound.Play();
                    }
                    //if (GetComponentInParent<Player>() && GetComponentInParent<Player>().type == TheType.Player)
                    //{
                    //    //Vibration.VibratePop();

                    //}
                }
                else
                {
                    if (GetComponentInParent<Player>())
                    {
                        Stacks.RemoveAt(Stacks.Count - 1);
                        //Renderer[] rends = Which.GetComponentsInChildren<Renderer>();
                        //for (int i = 0; i < rends.Length; i++)
                        //{
                        //    rends[i].material.color = Color.red;
                        //}
                        StartCoroutine(DestroyItem(Which));
                    }
                }
                Target.SuccessaddAction.Invoke();


            }
            //else
            //{
            //    if (Target.TheTimer)
            //    {
            //        Target.TheTimer.ResetTimer();
            //    }
            //}
            Target.addAction.Invoke();
            RefreshMax();
        }
        IEnumerator DestroyItem(GameObject Which)
        {
            Vector3 TheScale = Which.transform.localScale;
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
            Which.transform.localScale = TheScale;
            Which.SetActive(false);
        }
        private void Update()
        {
            for (int i = 0; i < Stacks.Count; i++)
            {
                if (i > 0)
                {
                    Transform Target = Stacks[i].transform;
                    if (Target.parent != StacksParent.transform)
                    {
                        Target.SetParent(StacksParent.transform);
                    }
                    else
                    {
                        Vector3 TargetPos = new Vector3(Stacks[i - 1].transform.position.x, Target.position.y, Stacks[i - 1].transform.position.z);
                        Target.position = Vector3.Lerp(Target.position, TargetPos, SwaySpeed * Time.deltaTime);

                        Vector3 relativePos = Target.position - Stacks[i - 1].transform.position;
                        float dist = Vector3.Distance(Target.position, TargetPos);
                        if (dist > 0.1f)
                        {
                            Quaternion rotation = Quaternion.LookRotation(relativePos, Vector3.forward);
                            //Target.rotation = rotation;
                            Transform LookTarget = Stacks[i - 1].transform;
                            Vector3 LookPos = LookTarget.position;
                            LookPos.y = Stacks[i].transform.position.y;
                            Target.LookAt(LookPos);
                            //Target.LookAt(Stacks[i - 1].transform.position,Vector3.up);
                            //Target.LookAt(relativePos);

                        }
                        else
                        {
                            Target.rotation = Quaternion.Slerp(Target.rotation,
                                 Stacks[0].transform.rotation, 5 * Time.deltaTime);
                            //Target.localRotation = Quaternion.Slerp(Target.localRotation, Quaternion.Euler(new Vector3(90,0,90)), 5 * Time.deltaTime);
                            //Quaternion rotation = Quaternion.LookRotation(relativePos, Vector3.up);
                            //Target.rotation = rotation;
                        }
                        // the second argument, upwards, defaults to Vector3.up

                    }
                }
                else
                {
                    //if (Stacks.Count > 1)
                    //{
                    //    Vector3 relativePos = Stacks[i].transform.position-Stacks[i + 1].transform.position ;
                    //    Quaternion rotation = Quaternion.LookRotation(relativePos, Vector3.up);
                    //    Stacks[i].transform.rotation = rotation ;
                    //}
                    //else
                    //{
                    //    Stacks[i].transform.localEulerAngles = new Vector3(90, 0, 0);
                    //}

                    Stacks[i].transform.localEulerAngles = new Vector3(0, 0, 0);
                }
            }
            if (UseMax)
            {
                if (IsFull())
                {
                    if (!Notification)
                    {
                        string msg = "Max";
                        Notification = _GameManager.instance.notificationMan.CreateNotification(msg, Stacks[Stacks.Count - 1].transform);
                    }
                    else
                    {
                        Notification.GetComponent<NotificationUi>().KeepAlive(0.2f);
                    }
                }
            }
        }
    }
}
