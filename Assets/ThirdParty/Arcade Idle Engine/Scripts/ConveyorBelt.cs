using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace AIE
{
    public class ConveyorBelt : MonoBehaviour
    {
        [Header("Important")]
        public Step EndStep;
        public ItemManager _ItemManager;
        public SlotManager slotman;
        public ConveyorMan _ConveyorMan;
        public GameObject ActionSlot;

        [Header("Extra")]

        public Transform EndPos;
        public List<GameObject> Items = new List<GameObject>();
        GameObject TargetItem;
        public ParticleSystem StepParticles;
        public TextureMove[] Belts;
        float Movetimestamp;
        GameObject Notification;
        public GameObject ActiveItem;
        public float ActionTimestamp;
        bool IsProcess = false;
        float processtime = 0;
        private void Update()
        {
            if (Movetimestamp > Time.time)
            {
                for(int i = 0; i < Belts.Length; i++)
                {
                    Belts[i].enabled = true;

                }
            }
            else
            {
                for (int i = 0; i < Belts.Length; i++)
                {
                    Belts[i].enabled = false;

                }
                /* if (Items.Count == 0)//||slotman.Slots[0].TheOwner==null)
                 {
                     if (!Notification)
                     {
                         string msg = TheStep.ToString() + " Error";
                         Notification = GameManager.instance.notificationMan.CreateNotification(msg,transform);
                     }
                     else
                     {
                         Notification.GetComponent<NotificationUi>().KeepAlive(0.1f);
                     }
                 }*/

            }
            MoveMan();

        }
        void MoveMan()
        {
            for (int i = 0; i < Items.Count; i++)
            {
                GameObject TheItem = Items[i];
                TheItem.GetComponent<TheItem>().StopCorutines();
                Transform Target = TheItem.GetComponent<SlotOwner>().TargetSlot.transform;
                TheItem.transform.position = Vector3.MoveTowards(TheItem.transform.position, Target.position, 2 * Time.deltaTime);
                float Dist = Vector3.Distance(TheItem.transform.position, Target.position);
                if (Dist < 0.1f)
                {

                    if (TheItem.GetComponent<SlotOwner>().CurrentPos == slotman.Slots.Length-1)
                    {
                        //TheCounter.Buy();
                        Slot Availableslot = _ItemManager.slotman.CheckAvaiableSlots(gameObject);
                        if (TargetItem == null && Availableslot)
                        {

                            TargetItem = TheItem;
                        }
                        else if (TargetItem && Availableslot)
                        {

                            if (Availableslot)
                            {
                                Slot theslot = TargetItem.GetComponent<SlotOwner>().TargetSlot;

                                if (_ItemManager.AddItem(TargetItem, Availableslot))
                                {
                                    while (TargetItem.GetComponent<SlotOwner>().TargetSlot == null)
                                    {
                                        _ItemManager.AddItem(TargetItem, Availableslot);
                                    }
                                    while (Items.Contains(TargetItem))
                                    {
                                        RemoveSpecificStack(TargetItem);
                                    }
                                    while (theslot.TheOwner)
                                    {
                                        theslot.TheOwner = null;
                                    }
                                    //if (TheStep == Step.Fill && TargetCup)
                                    //{
                                    //    TargetCup.GetComponent<Cup>().SwitchCream();
                                    //}
                                    TargetItem = null;
                                    break;


                                }

                            }
                        }


                    }
                    else
                    {
                        if (TheItem.GetComponent<SlotOwner>().TargetSlot.gameObject == ActionSlot)
                        {
                            if (!slotman.Slots[TheItem.GetComponent<SlotOwner>().CurrentPos + 1].TheOwner)
                            {
                                if (TheItem.GetComponent<TheItem>().ActiveStep != EndStep)
                                {
                                    //Debug.Log("Fill");
                                    if (ProcessComplete())
                                    {

                                        TheItem.GetComponent<TheItem>().SetStep(EndStep);
                                        TheItem.GetComponent<SlotOwner>().CurrentPos += 1;
                                        TheItem.GetComponent<SlotOwner>().TargetSlot.TheOwner = null;
                                        TheItem.GetComponent<SlotOwner>().TargetSlot = slotman.Slots[TheItem.GetComponent<SlotOwner>().CurrentPos];
                                        slotman.Slots[TheItem.GetComponent<SlotOwner>().CurrentPos].TheOwner = TheItem;
                                        StepParticles.Play();
                                        ActiveItem = null;

                                    }
                                    else
                                    {
                                        ActiveItem = TheItem;
                                        //if (GetComponentInParent<FillCups>())
                                        //{
                                        //    if (GetComponentInParent<FillCups>()&&
                                        //        GetComponentInParent<FillCups>().CanFill())
                                        //    {
                                        //        JuiceColor thecolor;

                                        //        TheItem.GetComponent<Cup>().FillTank.TargetTank.FillNewTank(TheItem.GetComponent<Tank>(), out thecolor);
                                        //        GetComponentInParent<FillCups>().ActivateFluid(thecolor);


                                        //    }
                                        //}
                                    }
                                }
                                else
                                {
                                    bool finished = false;
                                    if (finished)
                                    {
                                        ActiveItem = null;
                                        TheItem.GetComponent<TheItem>().SetStep(EndStep);
                                        TheItem.GetComponent<SlotOwner>().CurrentPos -= 1;
                                        TheItem.GetComponent<SlotOwner>().TargetSlot.TheOwner = null;
                                        TheItem.GetComponent<SlotOwner>().TargetSlot = slotman.Slots[TheItem.GetComponent<SlotOwner>().CurrentPos];
                                        slotman.Slots[TheItem.GetComponent<SlotOwner>().CurrentPos].TheOwner = TheItem;
                                        StepParticles.Play();

                                    }
                                }
                            }
                        }
                        else if (slotman.Slots.Length> TheItem.GetComponent<SlotOwner>().CurrentPos+1&&
                            !slotman.Slots[TheItem.GetComponent<SlotOwner>().CurrentPos + 1].TheOwner)
                        {
                            TheItem.GetComponent<SlotOwner>().CurrentPos += 1;
                            TheItem.GetComponent<SlotOwner>().TargetSlot.TheOwner = null;
                            TheItem.GetComponent<SlotOwner>().TargetSlot = slotman.Slots[TheItem.GetComponent<SlotOwner>().CurrentPos];
                            slotman.Slots[TheItem.GetComponent<SlotOwner>().CurrentPos].TheOwner = TheItem;


                            //if (TheItem.GetComponent<SlotOwner>().TargetSlot.gameObject == ActionSlot)
                            //{
                            //    if (TheItem.GetComponent<Cup>().SetStep(TheStep))
                            //    {
                            //        StepParticles.Play();
                            //    }
                            //   // Debug.Log("Action");
                            //}
                        }
                    }
                    //  if()
                }
                else
                {
                    Movetimestamp = Time.time + 1;
                    //TheItem.transform.LookAt(Target.position);
                }
            }
        }

        bool ProcessComplete()
        {
            processtime += Time.deltaTime;
            if (processtime > _ConveyorMan.ProcessTime)
            {
                processtime = 0;
                return true;
            }
            return false;
        }

        public bool AddItem(GameObject Go)
        {
            if (slotman.Slots[0].TheOwner == null)
            {
                GameObject Target = slotman.Slots[0].gameObject;
                Go.transform.position = Target.transform.position;
                //GameObject Go = Instantiate(NpcPref, SpawnPos.transform.position, SpawnPos.transform.rotation, transform);
                Items.Add(Go);
                Go.GetComponent<SlotOwner>().CurrentPos = 0;
                Target.GetComponent<Slot>().TheOwner = Go;
                Go.GetComponent<SlotOwner>().TargetSlot = Target.GetComponent<Slot>();
                return true;
            }
            else
            {
                return false;
            }


        }
        public void RemoveSpecificStack(GameObject Which)
        {
            for (int i = 0; i < Items.Count; i++)
            {
                if (Items[i] == Which)
                {
                    Items.RemoveAt(i);
                    break;
                }
            }

        }
        //public void NpcFinished(GameObject Npc)
        //{
        //    npcs.RemoveAt(0);
        //    Finishednpcs.Add(Npc);
        //}
    }
}
