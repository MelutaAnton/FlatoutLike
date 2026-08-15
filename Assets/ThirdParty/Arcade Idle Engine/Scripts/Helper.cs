using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
namespace AIE
{
    public class Helper : MonoBehaviour
    {
        public StackRemover[] stackremover;
        int currentstack;
        SlotManager SlotTarget;
        public SlotManager[] SlotTargets;
        public Transform Target;
        float WaitTime;
        float MoveSeed = 2;
        float CollectTimestamp;
        public Stack TheStack;
        float ReleaseTimestamp;
        public Animator Anim; 
        public bool UseHandcart;
        public bool UseNavmesh;
        public GameObject NavmeshHelper;
        public NavMeshAgent _NavmeshAgent;
        float airesettimestamp;
        Vector3 _targetpos;
        void Start()
        {
            Refreshtats();
        }
        public void Refreshtats()
        {
            TheStack.MaxSize = _GameManager.instance.upgradeManager.WorkerCarryCapacity + _GameManager.instance.upgradeManager.UpgradeHud.Workerupgrade.Capacity;
            MoveSeed = _GameManager.instance.upgradeManager.WorkerSpeed + _GameManager.instance.upgradeManager.UpgradeHud.Workerupgrade.Speed;
        }
        void Update()
        {
            if (SlotTargets.Length == 0 || stackremover.Length == 0)
                return;
            if (TheStack.Stacks.Count > 0 || UseHandcart)
            {
                Anim.GetComponent<IKControl>().RH = TheStack.GetComponent<IkTargets>().RH;
                Anim.GetComponent<IKControl>().LH = TheStack.GetComponent<IkTargets>().LH;
            }
            else
            {
                Anim.GetComponent<IKControl>().RH = null;
                Anim.GetComponent<IKControl>().LH = null;
            }

            if (Target == null)
            {
               
                if (SlotTarget)
                {
                    Target = SlotTarget.transform;
                    transform.LookAt(Target);
                }
                else
                {
                    List<SlotManager> thelist = new List<SlotManager>();

                    for (int i = 0; i < SlotTargets.Length; i++)
                    {
                        if (SlotTargets[i].gameObject.activeInHierarchy &&
                            SlotTargets[i].GetAssignedSlots() > 0)
                        {
                            thelist.Add(SlotTargets[i]);
                        }

                    }
                    SlotManager temp = null;
                    if (thelist.Count > 0)
                    {
                        temp = thelist[Random.Range(0, thelist.Count)];
                       
                    }
                    if (temp)
                    {
                        SlotTarget = temp;
                    }
                    else
                    {
                        SlotTarget = SlotTargets[0];
                    }


                }
            }
            else
            {
                float Dist = Vector3.Distance(transform.position, Target.position);
                if (Target.GetComponentInChildren<Timer>())
                {
                    Dist = Vector3.Distance(transform.position, Target.GetComponentInChildren<Timer>().transform.position);

                }

                if (Dist < 1)
                {
                    Anim.SetFloat("V", 0);
                    WaitTime += Time.deltaTime;
                    if (WaitTime > 3)
                    {
                        if (Target == SlotTarget.transform)
                        {
                            Target = stackremover[currentstack].transform;
                            if (TheStack.Stacks.Count > 0)
                            {
                                GameObject theobj = TheStack.Stacks[0];
                                if (Target.GetComponent<StackRemover>())
                                {
                                    GameObject TargetObj = Target.GetComponent<StackRemover>().TargetObj;
                                    if (TargetObj && TargetObj.GetComponent<TheItem>().ItemName != theobj.GetComponent<TheItem>().ItemName)
                                    {
                                        for (int i = 0; i < stackremover.Length; i++)
                                        {
                                            TargetObj = stackremover[i].TargetObj;
                                            if (TargetObj && TargetObj.GetComponent<TheItem>().ItemName == theobj.GetComponent<TheItem>().ItemName)
                                            {
                                                Target = stackremover[i].transform;
                                                currentstack = i;
                                                break;
                                            }
                                        }
                                    }
                                }
                            }



                            WaitTime = 0;
                           
                        }
                        else
                        {
                            WaitTime = 0;
                            GetNextStack();
                            if (TheStack.Stacks.Count == 0)
                            {
                                if (SlotTargets.Length > 0)
                                {
                                    SlotTarget = null;
                                    Target = null;
                                }
                                else
                                {
                                    Target = SlotTarget.transform;
                                 
                                }

                            }
                            else
                            {
                            }

                        }


                    }
                    else
                    {
                        if (Target == SlotTarget.transform)
                        {
                            if (Target.GetComponentInParent<ItemSpawner>())
                            {
                                Target.GetComponentInParent<ItemSpawner>().PauseSpawn();
                            }
                            if (CollectTimestamp < Time.time && !TheStack.IsFull())
                            {
                                SlotManager slotman = SlotTarget;
                                GameObject Target = null;
                                for (int i = slotman.Slots.Length - 1; i >= 0; i--)
                                {
                                    if (slotman.Slots[i].TheOwner)
                                    {
                                        if (slotman.Slots[i].TheOwner.GetComponent<TheItem>())
                                        {
                                            if (!slotman.Slots[i].TheOwner.GetComponent<TheItem>().IsMoving)
                                            {
                                                Target = slotman.Slots[i].TheOwner;
                                                slotman.Slots[i].TheOwner = null;
                                                break;
                                            }
                                        }
                                        else if (slotman.Slots[i].TheOwner.GetComponentInParent<TheNpc>())
                                        {

                                        }
                                        else
                                        {
                                            Target = slotman.Slots[i].TheOwner;
                                            slotman.Slots[i].TheOwner = null;
                                            break;
                                        }

                                    }
                                    else
                                    {
                                        if (!slotman.Slots[i].TheOwner && slotman.Slots[i].TheStack &&
                                   slotman.Slots[i].TheStack.Stacks.Count > 0)
                                        {
                                            TheStack.SiphonStack(slotman.Slots[i].TheStack);
                                            break;
                                        }
                                    }
                                }
                                if (Target)
                                {
                                    if (SlotTarget.transform.GetComponentInParent<CashStack>())
                                    {
                                        CollectTimestamp = Time.time + 0.1f;

                                        _GameManager.instance.cashman.SpawnCash(Camera.main.WorldToScreenPoint(Target.transform.position), 1, 10);
                                        Destroy(Target);
                                    }

                                    else
                                    {
                                        CollectTimestamp = Time.time + 0.3f;

                                        TheStack.AddStack(Target);



                                    }
                                    if (slotman)
                                    {
                                        slotman._Refresh();
                                    }
                                }
                            }
                        }
                        else
                        {
                            Timer TheTimer = stackremover[currentstack].TheTimer;
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
                            if (ReleaseTimestamp < Time.time)
                            {
                                TheStack.RemoveStack(stackremover[currentstack]);
                                ReleaseTimestamp = Time.time + 0.1f;

                            }
                            //if (stackremover.transform.GetComponentInParent<Counter>())
                            //{
                            //    stackremover.transform.GetComponentInParent<Counter>().ActivateSelling();
                            //}
                        }
                    }
                }
                else
                {
                    // transform.LookAt(Target);
                    if (Target.GetComponentInChildren<Timer>())
                    {
                        // transform.position = Vector3.MoveTowards(transform.position, Target.GetComponentInChildren<Timer>().transform.position, MoveSeed * Time.deltaTime);
                        Move(Target.GetComponentInChildren<Timer>().transform.position, UseNavmesh);
                    }
                    else
                    {
                        // transform.position = Vector3.MoveTowards(transform.position, Target.position, MoveSeed * Time.deltaTime);
                        Move(Target.position, UseNavmesh);

                    }

                    Anim.SetFloat("V", 0.5f + (_GameManager.instance.upgradeManager.UpgradeHud.Workerupgrade.Speed / 2.5f));
                }
            }
        }
        void GetNextStack()
        {
            currentstack += 1;
            if (currentstack > stackremover.Length - 1 || !stackremover[currentstack].gameObject.activeInHierarchy)
            {
                currentstack = 0;
            }
        }
        public void Move(Vector3 Target, bool UseNavmesh)
        {
            if (UseNavmesh)
            {
                if (!NavmeshHelper.activeSelf || Target != _targetpos)
                {
                    airesettimestamp = Time.time + 0.5f;
                    _NavmeshAgent.transform.SetParent(null);
                    NavmeshHelper.SetActive(true);
                }
                _targetpos = Target;


                _NavmeshAgent.SetDestination(Target);


                float Dist = Vector3.Distance(transform.position, Target);
                if (Dist > 2 && airesettimestamp < Time.time)
                {

                    transform.Translate(Vector3.forward * MoveSeed * Time.deltaTime);
                    transform.eulerAngles = _NavmeshAgent.transform.eulerAngles;




                }
                else
                {
                    transform.position = Vector3.MoveTowards(transform.position, Target, MoveSeed * Time.deltaTime);
                    transform.LookAt(Target);
                }
                _NavmeshAgent.transform.position = NavmeshHelper.transform.position;

            }
            else
            {
                NavmeshHelper.SetActive(false);
                transform.position = Vector3.MoveTowards(transform.position, Target, MoveSeed * Time.deltaTime);
                transform.LookAt(Target);
            }
        }
    }
   
}
