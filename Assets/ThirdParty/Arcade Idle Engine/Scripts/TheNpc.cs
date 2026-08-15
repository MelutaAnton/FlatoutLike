using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.AI;
namespace AIE
{
    public class TheNpc : MonoBehaviour
    {
        public enum npctype
        {
            Default
        }
        public npctype type;
        public int CurrentPos = 0;
        public Transform Hands;
        public Animator Anim;
        public Emotion TheEmotion;
        public GameObject[] Characters;
        public GameObject[] VipCharacters;
        float waittimestamp;
        float SitTimestamp;
        public GameObject ItemPref;
        public GameObject TheItem;
        public bool ReachedDoor;
        public bool OnCounter;
        bool finished;
        bool finishedsit;
        float DrinkTimstamp;
        GameObject skinfound;
        public Stack TheStack;
        public Text CounterText;
        public int RemainingCount = 0;
        public GameObject CounterHolder;
        public GameObject NavmeshHelper;
        public NavMeshAgent _NavmeshAgent;
        public bool HasPath;

        float airesettimestamp;
        public bool UseStands;
        public bool FromStands;
        //private void Start()
        //{
        //    ResetSkin();
        //}
        public void ResetSkin()
        {
            for (int i = 0; i < VipCharacters.Length; i++)
            {
                VipCharacters[i].SetActive(false);
            }
            if (skinfound)
            {
                skinfound.SetActive(true);
                Anim = skinfound.GetComponent<Animator>();
                return;
            }
            int rand = Random.Range(0, Characters.Length);
            for (int i = 0; i < Characters.Length; i++)
            {
                if (rand == i)
                {
                    Characters[i].SetActive(true);
                    Anim = Characters[i].GetComponent<Animator>();
                    skinfound = Characters[i];


                }
                else
                {
                    Characters[i].SetActive(false);

                }
            }

        }
        public void MakeVip()
        {
            for (int i = 0; i < Characters.Length; i++)
            {
                Characters[i].SetActive(false);
            }
            int rand = Random.Range(0, VipCharacters.Length);
            for (int i = 0; i < VipCharacters.Length; i++)
            {
                if (rand == i)
                {
                    VipCharacters[i].SetActive(true);
                    Anim = VipCharacters[i].GetComponent<Animator>();

                }
                else
                {
                    VipCharacters[i].SetActive(false);

                }
            }

        }
        public void AddWaitTime()
        {
            waittimestamp += Time.deltaTime;
            if (waittimestamp > 15 && !finished&&!UseStands)
            {
                int theemotion = Random.Range(0, 3);
                //Debug.Log(theemotion);
                Emotion.TheEmotion emotion = Emotion.TheEmotion.Angry;
                if (theemotion == 1)
                {
                    emotion = Emotion.TheEmotion.Angry;
                }
                else if (theemotion == 2)
                {
                    emotion = Emotion.TheEmotion.Angry;
                }
                // TheEmotion.ShowEmotion(emotion);
                Exit(emotion);
                //GameManager.instance.npcTalkMan.ShowTalk(NpcTalkType.BadReview);
            }
        }
        public void StopWaitTime()
        {
            waittimestamp = 0;
        }
        public void AddSitTime()
        {
            SitTimestamp += Time.deltaTime;
            if (TheStack.Stacks.Count > 0 && SitTimestamp > 3 && !finishedsit ||
                TheStack.Stacks.Count == 0)
            {
                if (_GameManager.instance.economyMan.CanTip)
                {
                    CashStack thecashstack = GetComponent<SlotOwner>().TargetSlot.GetComponentInParent<DiningTable>().cashstack;
                    Slot Availableslot = thecashstack.slotman.CheckAvaiableSlots(gameObject);
                    if (Availableslot)
                    {
                        // finishedsit = true;
                        // FinishSit();
                        //GetComponent<SlotOwner>().TargetSlot.GetComponentInParent<DiningTable>().PlayEnjoySound();

                        int count = _GameManager.instance.economyMan.TipsAmount;
                        while (count > 0)
                        {
                            count -= 1;

                            Slot tempAvailableslot = thecashstack.slotman.CheckAvaiableSlots(gameObject);
                            thecashstack.AddCash(tempAvailableslot);
                        }
                    }
                    else
                    {
                        GetComponent<SlotOwner>().TargetSlot.GetComponentInParent<DiningTable>().ShowCollectNotification();
                    }
                }
                if (StackFinished())
                {
                    finishedsit = true;
                    GetComponent<SlotOwner>().TargetSlot.GetComponentInParent<DiningTable>().PlayEnjoySound();
                    FinishSit();
                }
                else
                {
                    SitTimestamp = 0;
                }



            }
            if (finishedsit)
            {

            }
            if (TheItem)
            {


            }
        }
        bool StackFinished()
        {
            if (TheStack.Stacks.Count == 0)
            {
                return true;
            }
            Slot TheSlot = GetComponent<SlotOwner>().TargetSlot;
            GameObject LastItem = TheStack.Stacks[TheStack.Stacks.Count - 1];
            LastItem.GetComponent<TheItem>().SetStep(Step.Trash);
            TheSlot.GetComponent<Chair>().TrashStack.AddStack(LastItem);
            TheStack.Stacks.Remove(LastItem);
            return false;
        }
        void FinishSit()
        {
            SitTimestamp = 0;
            finishedsit = false;
            ReachedDoor = false;
            Anim.Play("Move");
            Slot TheSlot = GetComponent<SlotOwner>().TargetSlot;
            TheSlot.GetComponent<Chair>().Used();
            GetComponentInParent<QueLine>().NpcFinished(gameObject, Emotion.TheEmotion.Happy);
            for (int i = 0; i < TheStack.Stacks.Count; i++)
            {
                TheSlot.GetComponent<Chair>().TrashStack.AddStack(TheStack.Stacks[i]);
                TheStack.Stacks[i].GetComponent<TheItem>().SetStep(Step.Trash);
            }
            Anim.GetComponent<IKControl>().enabled = false;
            // TheItem = TheStack.Stacks[0];
            //TheSlot.TheOwner = TheItem;
            // TheItem.GetComponent<TheItem>().SetStep(Step.Trash);
            //TheItem.GetComponent<SlotOwner>().TargetSlot = TheSlot;
            TheItem = null;
            TheStack.Stacks.Clear();

            TheSlot.GetComponentInParent<SlotManager>()._Refresh();
            // GameManager.instance.npcTalkMan.ShowTalk(NpcTalkType.GoodReview);
            //TheSlot.GetComponent<Chair>().NotUsed();
        }
        public void AddZooTime()
        {
            SitTimestamp += Time.deltaTime;
            if (SitTimestamp > 5 && !finishedsit)
            {
                /* CashStack thecashstack = GetComponent<SlotOwner>().TargetSlot.GetComponentInParent<ZooPoint>().cashstack;
                 Slot Availableslot = thecashstack.slotman.CheckAvaiableSlots(gameObject);
                 if (Availableslot)
                 {
                     // finishedsit = true;
                     // FinishSit();
                     thecashstack.AddCash(Availableslot);
                     //GetComponent<SlotOwner>().TargetSlot.GetComponentInParent<DiningTable>().PlayEnjoySound();

                     int count = 3;
                     while (count > 0)
                     {
                         count -= 1;

                         Slot tempAvailableslot = thecashstack.slotman.CheckAvaiableSlots(gameObject);
                         thecashstack.AddCash(tempAvailableslot);
                     }
                 }*/

                finishedsit = true;
            }

        }
        public void Exit(Emotion.TheEmotion emotion)
        {
            finished = true;
            //if ( GetComponentInParent<QueLine>().GetSeat(gameObject,emotion))
            //{
            //    if (TheCup == null)
            //    {
            //        AddCup(Instantiate(CupPref));
            //    }
            //}
            //else
            //{
            if (type == npctype.Default)
            {
                GetComponentInParent<QueLine>().NpcFinished(gameObject, emotion);

            }

            //}
        }

        public void Finished(GameObject _item)
        {
            finished = true;
            //AddItem(_item);
            if (GetComponentInParent<QueLine>().TheCounter.RequireSeat)
            {
                if (GetComponentInParent<QueLine>().GetSeat(gameObject, Emotion.TheEmotion.Drool))
                {
                }
                else
                {
                    GetComponentInParent<QueLine>().NpcFinished(gameObject, Emotion.TheEmotion.Seat);
                }
            }
            else
            {
                GetComponentInParent<QueLine>().NpcFinished(gameObject, Emotion.TheEmotion.Drool);
            }

            //GetComponentInParent<QueLine>().NpcFinished(gameObject, Emotion.TheEmotion.Happy);
            CounterHolder.SetActive(false);
        }
        public void AddItem(GameObject _item)
        {
            _item.GetComponent<TheItem>().StopCorutines();
            //_item.transform.parent = Hands;
            //_item.transform.localPosition = Vector3.zero;
            //_item.transform.localEulerAngles = new Vector3(0, 0, 0);
            Anim.GetComponent<IKControl>().enabled = true;
            TheItem = _item;
            TheStack.AddStack(_item);
            RemainingCount = TheStack.MaxSize - TheStack.Stacks.Count;
            CounterText.text = (RemainingCount).ToString();


        }

        public void RemoveItem()
        {
            for (int i = 0; i < TheStack.Stacks.Count; i++)
            {
                TheStack.Stacks[i].transform.SetParent(null);
                TheStack.Stacks[i].SetActive(false);
            }
            TheStack.Stacks.Clear();
            TheItem = null;

            Anim.GetComponent<IKControl>().enabled = false;
        }
        public void SetUp()
        {
            TheStack.MaxSize = Random.Range(1, 5);
            RemainingCount = TheStack.MaxSize;
            CounterText.text = RemainingCount.ToString();
            CounterHolder.SetActive(false);
            OnCounter = false;
        }
        Vector3 _targetpos;
        public void Move(Vector3 Target,bool UseNavmesh)
        {
            if (UseNavmesh)
            {
                if (!NavmeshHelper.activeSelf||Target!=_targetpos)
                {
                    airesettimestamp = Time.time + 0.5f;
                    _NavmeshAgent.transform.SetParent(null);
                    NavmeshHelper.SetActive(true);
                }
                HasPath = _NavmeshAgent.hasPath;
                _targetpos = Target;


                _NavmeshAgent.SetDestination(Target);


                // Target = _NavmeshAgent.posi;
                float Dist = Vector3.Distance(transform.position, Target);
                if (Dist > 2 && airesettimestamp<Time.time)
                {
                    //bool isFound;
                    //Vector3 targetpos = ClosestAiPos(out isFound);
                    //if (isFound)
                    //{
                    //    transform.position = Vector3.MoveTowards(transform.position, targetpos, 2 * Time.deltaTime);
                    //    transform.LookAt(targetpos);

                    //}
                    //else
                    //{
                    //    transform.position = Vector3.MoveTowards(transform.position, Target, 2 * Time.deltaTime);
                    //        transform.LookAt(Target);
                    //}
                    transform.Translate(Vector3.forward * 2 * Time.deltaTime);
                    transform.eulerAngles = _NavmeshAgent.transform.eulerAngles;
                   
                   
                    

                }
                else
                {
                    transform.position = Vector3.MoveTowards(transform.position, Target, 2 * Time.deltaTime);
                    transform.LookAt(Target);
                }
                _NavmeshAgent.transform.position =NavmeshHelper.transform.position;

            }
            else
            {
                NavmeshHelper.SetActive(false);
                transform.position = Vector3.MoveTowards(transform.position, Target, 2 * Time.deltaTime);
                transform.LookAt(Target);
            }
        }
        public void GetPath()
        {
            for (int i = 0; i < _NavmeshAgent.path.corners.Length; i++)
            {
                GameObject New = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                New.transform.position=_NavmeshAgent.path.corners[i];
            }
        }
        Vector3 ClosestAiPos(out bool Found)
        {
            bool isfound = false;
            Vector3 thepos = Vector3.zero;
            float Mindist = 10000;
           for(int i = 0; i < _NavmeshAgent.path.corners.Length; i++)
            {
                float tempdist = Vector3.Distance(transform.position, _NavmeshAgent.path.corners[i]);
                if (tempdist < Mindist&&tempdist>1)
                {
                    thepos = _NavmeshAgent.path.corners[i];
                    Mindist = tempdist;
                    isfound = true;
                }
            }
           Found= isfound;
            return thepos;
        }
    }
   
}
