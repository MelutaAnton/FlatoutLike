using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace AIE
{
    public class QueLine : MonoBehaviour
    {
        public SlotManager slotman;
        public GameObject NpcPref;
        public Transform SpawnPos;
        public Transform EndPos;
        public List<GameObject> npcs = new List<GameObject>();
        public List<GameObject> Seatnpcs = new List<GameObject>();
        public List<GameObject> Finishednpcs = new List<GameObject>();
        public List<GameObject> Standnpcs = new List<GameObject>();
        public Counter TheCounter;
        float Spawntimestamp;
        public Transform QueExitPos;
        float starttimstamp;
        public int VipCounter;
        public int VipSpawnCounter = 5;
        float vipshowcasetimestamp;
        public DiningTable[] tables;
        public Stand[] Stands;
        public bool UseNavmesh;
        float stackdelaytimestamp;
        private void Start()
        {
            starttimstamp = Time.time + 5;
        }
        private void Update()
        {
            if (slotman.Slots[slotman.Slots.Length - 1].TheOwner == null&&Standnpcs.Count<10)
            {
                if (Spawntimestamp < Time.time)
                {
                    Spawntimestamp = Time.time + 1f;
                    CreateNpc();

                }
            }
            NpcMan();
            StandsMan();
            FinishedMan();
            SeatMan();
            if (slotman.Slots[0].TheOwner != null
                )
            {
                if (slotman.Slots[0].TheOwner.GetComponent<TheNpc>().TheStack.Stacks.Count == 0)
                {
                    slotman.Slots[0].TheOwner.GetComponent<TheNpc>().AddWaitTime();

                }
                else
                {
                    if (TheCounter.RequireSeat&&SeatAvailable()&&
                        slotman.Slots[0].TheOwner.GetComponent<TheNpc>().RemainingCount<=0)
                    {
                        TheCounter.RemoveCustomer();
                    }

                }
            }
        }
        public void StopWaitTime()
        {
            if (slotman.Slots[0].TheOwner != null
               )
            {
                if (!slotman.Slots[0].TheOwner.GetComponent<TheNpc>().TheItem)
                {
                    slotman.Slots[0].TheOwner.GetComponent<TheNpc>().StopWaitTime();

                }
            }
        }
        public bool BuyerOnCounter()
        {
            if (slotman.Slots[0].TheOwner != null
               )
            {
                if (slotman.Slots[0].TheOwner.GetComponent<TheNpc>().OnCounter)
                {
                    return true;

                }
                else
                {
                    return false;
                }
            }
            return false;
        }
        void SeatMan()
        {
            for (int i = 0; i < Seatnpcs.Count; i++)
            {
                GameObject Npc = Seatnpcs[i];
                Transform Target = Seatnpcs[i].GetComponent<SlotOwner>().TargetSlot.transform;
                
                float Dist = Vector3.Distance(Npc.transform.position, Target.position);
                if (Dist < 0.1f)
                {
                    Npc.transform.rotation = Target.rotation;
                    Npc.GetComponent<TheNpc>().Anim.SetFloat("V", 0);
                    if (!Npc.GetComponent<TheNpc>().Anim.GetCurrentAnimatorStateInfo(0).IsName("Sit"))
                    {
                        Npc.GetComponent<TheNpc>().Anim.Play("Sit");
                        Target.GetComponent<Chair>().Using();
                    }
                    Npc.GetComponent<TheNpc>().AddSitTime();

                    // Finishednpcs.RemoveAt(i);
                    //  Destroy(Npc);

                    // break;
                }
                else
                {
                    Npc.GetComponent<TheNpc>().Move(Target.position, UseNavmesh);
                    Npc.GetComponent<TheNpc>().Anim.SetFloat("V", 0.5f);
                 
                }
            }
        }
        public void ResetNpc()
        {
            for (int i = 0; i < npcs.Count; i++)
            {
                npcs[i].SetActive(false);
                npcs[i].GetComponent<TheNpc>().OnCounter = false;
                npcs[i].GetComponent<TheNpc>().TheEmotion.StopEmotions();
                npcs[i].GetComponent<SlotOwner>().TargetSlot = null;
                npcs[i].GetComponent<TheNpc>().ResetSkin();
            }
            npcs.Clear();
            for (int i = 0; i < slotman.Slots.Length; i++)
            {
                slotman.Slots[i].TheOwner = null;
            }
            starttimstamp = Time.time + 5;
            CreateNpc();
        }
        void NpcMan()
        {
            for (int i = 0; i < npcs.Count; i++)
            {
                GameObject Npc = npcs[i];
                Transform Target = Npc.GetComponent<SlotOwner>().TargetSlot.transform;
              
                float Dist = Vector3.Distance(Npc.transform.position, Target.position);
                if (Dist < 0.1f)
                {
                    Npc.GetComponent<TheNpc>().Anim.SetFloat("V", Mathf.MoveTowards(Npc.GetComponent<TheNpc>().Anim.GetFloat("V"), 0, 1 * Time.deltaTime));
                    if (Npc.GetComponent<TheNpc>().CurrentPos == 0)
                    {

                        if (Npc.GetComponent<TheNpc>().FromStands)
                        {
                            if (TheCounter.SellingTimestamp > Time.time)
                            {
                                if (stackdelaytimestamp < Time.time)
                                {
                                    Npc.GetComponent<TheNpc>().TheStack.RemoveStack(TheCounter.stacks);
                                    stackdelaytimestamp = Time.time + 0.2f;
                                }
                                if (Npc.GetComponent<TheNpc>().TheStack.Stacks.Count == 0)
                                {
                                    Npc.GetComponent<TheNpc>().FromStands = false;
                                    Npc.GetComponent<TheNpc>().RemoveItem();
                                }

                            }
                        }
                        else
                        {
                            TheCounter.Buy();

                        }

                        if (Npc.GetComponent<TheNpc>().RemainingCount>0&&
                            !Npc.GetComponent<TheNpc>().OnCounter)
                        {
                            Npc.GetComponent<TheNpc>().CounterHolder.SetActive(true);
                        }
                        Npc.GetComponent<TheNpc>().OnCounter = true;
                        Npc.transform.eulerAngles = slotman.Slots[Npc.GetComponent<TheNpc>().CurrentPos].transform.eulerAngles;
                    }
                    else
                    {
                        if (!slotman.Slots[Npc.GetComponent<TheNpc>().CurrentPos - 1].TheOwner)
                        {
                            Npc.GetComponent<TheNpc>().CurrentPos -= 1;
                            Npc.GetComponent<SlotOwner>().TargetSlot.TheOwner = null;
                            Npc.GetComponent<SlotOwner>().TargetSlot = slotman.Slots[Npc.GetComponent<TheNpc>().CurrentPos];
                            slotman.Slots[Npc.GetComponent<TheNpc>().CurrentPos].TheOwner = Npc;
                            //if (Npc.GetComponent<TheNpc>().CurrentPos == 0&&
                            //   !smoothieiconshown)
                            //{
                            //    smoothieiconshown = true;
                            //    //Npc.GetComponent<TheNpc>().TheEmotion.ShowEmotion(Emotion.TheEmotion.Smoothie);

                            //}
                        }
                        else
                        {
                            Npc.transform.eulerAngles = slotman.Slots[Npc.GetComponent<TheNpc>().CurrentPos].transform.eulerAngles;

                        }
                    }
                    //  if()
                }
                else
                {

                    Npc.GetComponent<TheNpc>().Move(Target.position, UseNavmesh);
                    Npc.GetComponent<TheNpc>().Anim.SetFloat("V", 0.5f);
                

                    if (Npc.GetComponent<TheNpc>().CurrentPos > 1 &&
                        !slotman.Slots[Npc.GetComponent<TheNpc>().CurrentPos - 1].TheOwner)
                    {
                        Npc.GetComponent<TheNpc>().CurrentPos -= 1;
                        Npc.GetComponent<SlotOwner>().TargetSlot.TheOwner = null;
                        Npc.GetComponent<SlotOwner>().TargetSlot = slotman.Slots[Npc.GetComponent<TheNpc>().CurrentPos];
                        slotman.Slots[Npc.GetComponent<TheNpc>().CurrentPos].TheOwner = Npc;
                    }
                }

            }
        }
        void StandsMan()
        {
            for (int i = 0; i < Standnpcs.Count; i++)
            {
                GameObject Npc = Standnpcs[i];
                Transform Target = Npc.GetComponent<SlotOwner>().TargetSlot.transform;
                float Dist = Vector3.Distance(Npc.transform.position, Target.position);
                if (Dist < 0.1f)
                {
                    Target.GetComponentInParent<Stand>().GetItem(Npc.GetComponent<TheNpc>());
                    if (Npc.GetComponent<TheNpc>().RemainingCount > 0 &&
                        !Npc.GetComponent<TheNpc>().OnCounter)
                    {
                        Npc.GetComponent<TheNpc>().CounterHolder.SetActive(true);
                    }
                    Npc.GetComponent<TheNpc>().Anim.SetFloat("V", Mathf.MoveTowards(Npc.GetComponent<TheNpc>().Anim.GetFloat("V"), 0, 1 * Time.deltaTime));

                    if (Npc.GetComponent<TheNpc>().TheStack.IsFull())
                    {
                        Npc.GetComponent<TheNpc>().CounterHolder.SetActive(false);

                        if (slotman.Slots[slotman.Slots.Length - 1].TheOwner == null)
                        {
                            NpcStandFinished(Npc, Emotion.TheEmotion.Happy);
                        }
                    }
                    Npc.transform.eulerAngles =Target.transform.eulerAngles;


                }
                else
                {
                  
                    Npc.GetComponent<TheNpc>().Move(Target.position,UseNavmesh);
                    Npc.GetComponent<TheNpc>().Anim.SetFloat("V", 0.5f);
                }

            }
        }
        IEnumerator MoveToQueExit(Transform Which)
        {
            float TheT = 0.3f;
            // TheT = 0;
            while (TheT > 0)
            {
                TheT -= Time.deltaTime;
                Which.transform.position = Vector3.MoveTowards(Which.transform.position, QueExitPos.position, 2 * Time.deltaTime);
                yield return new WaitForSeconds(0.00001f);
            }
        }
        void FinishedMan()
        {
            for (int i = 0; i < Finishednpcs.Count; i++)
            {
                GameObject Npc = Finishednpcs[i];
                Transform Target = EndPos;
               
                float Dist = Vector3.Distance(Npc.transform.position, Target.position);
                if (Dist < 0.1f)
                {
                    Npc.GetComponent<TheNpc>().Anim.SetFloat("V", 0);
                    Finishednpcs.RemoveAt(i);
                    //Destroy(Npc);
                    Npc.GetComponent<TheNpc>().TheEmotion.StopEmotions();
                    Npc.GetComponent<TheNpc>().RemoveItem();
                    Npc.SetActive(false);
                    break;
                }
                else
                {
                    Npc.GetComponent<TheNpc>().Move(Target.position, UseNavmesh);
                    Npc.GetComponent<TheNpc>().Anim.SetFloat("V", 0.5f);
                }
            }
        }
        void CreateNpc()
        {
            Slot StandSlot = GetStandSlot();
            if (Stands.Length > 0 && !StandSlot)
                return;
            Transform _spawnpos = SpawnPos;
            bool spawnatdoor = true;
            if (Stands.Length==0&&starttimstamp > Time.time)
            {
                spawnatdoor = false;
                _spawnpos = slotman.Slots[slotman.Slots.Length - 1].transform;
            }
            //GameObject Go= Instantiate(NpcPref,_spawnpos.position,SpawnPos.transform.rotation,transform);
            GameObject Go = _GameManager.instance.poolman.GetNpc(transform);
            Go.GetComponent<TheNpc>().type = TheNpc.npctype.Default;
            Go.transform.position = _spawnpos.position;
            Go.transform.rotation = _spawnpos.rotation;
            
            if (StandSlot)
            {
                Go.GetComponent<TheNpc>().FromStands = true;
                Go.GetComponent<TheNpc>().UseStands = true;
                AddToStands(Go,StandSlot);
            }
            else
            {
               
                AddToQueLine(Go);
            }

            


            VipChecker(Go);
            Go.GetComponent<TheNpc>().SetUp();


        }
        void AddToStands(GameObject Go,Slot standslot)
        {
            Standnpcs.Add(Go);
            standslot.TheOwner = Go;
            Go.GetComponent<SlotOwner>().TargetSlot = standslot;
        }
        void AddToQueLine(GameObject Go)
        {
            npcs.Add(Go);
            Go.GetComponent<TheNpc>().CurrentPos = slotman.Slots.Length - 1;
            GameObject Target = slotman.Slots[slotman.Slots.Length - 1].gameObject;
            Target.GetComponent<Slot>().TheOwner = Go;
            Go.GetComponent<SlotOwner>().TargetSlot = Target.GetComponent<Slot>();
        }
        void VipChecker(GameObject Go)
        {
            VipCounter += 1;
            if (VipCounter >= VipSpawnCounter)
            {
                VipCounter = 0;
                Go.GetComponent<TheNpc>().MakeVip();
            }
            else
            {
                //if (Go.GetComponent<TheNpc>().Anim.GetComponent<NpcModel>().IsVip)
                //{

                //}
                Go.GetComponent<TheNpc>().ResetSkin();
            }
        }
        public void NpcStandFinished(GameObject Npc, Emotion.TheEmotion Whichemotion)
        {
            for (int i = 0; i < Standnpcs.Count; i++)
            {
                if (Standnpcs[i] == Npc)
                {
                    Standnpcs.RemoveAt(i);
                    break;
                }
            }
            Npc.GetComponent<SlotOwner>().TargetSlot.TheOwner = null;
            AddToQueLine(Npc);
            Npc.GetComponent<TheNpc>().TheEmotion.ShowEmotion(Whichemotion);

        }
        public void NpcFinished(GameObject Npc, Emotion.TheEmotion Whichemotion)
        {
            for (int i = 0; i < Seatnpcs.Count; i++)
            {
                if (Seatnpcs[i] == Npc)
                {
                    Seatnpcs.RemoveAt(i);
                    break;
                }
            }
            for (int i = 0; i < npcs.Count; i++)
            {
                if (npcs[i] == Npc)
                {
                    StartCoroutine(MoveToQueExit(Npc.transform));
                    npcs.RemoveAt(i);
                    break;
                }
            }
            Finishednpcs.Add(Npc);
            Npc.GetComponent<TheNpc>().TheEmotion.ShowEmotion(Whichemotion);
            Npc.GetComponent<SlotOwner>().TargetSlot.TheOwner = null;

        }
        public void NpcSeat(GameObject Npc, Emotion.TheEmotion Whichemotion, Slot TheSlot)
        {
            for (int i = 0; i < npcs.Count; i++)
            {
                if (npcs[i] == Npc)
                {
                    npcs.RemoveAt(i);
                    StartCoroutine(MoveToQueExit(Npc.transform));
                    break;
                }
            }
            Seatnpcs.Add(Npc);
            Npc.GetComponent<TheNpc>().TheEmotion.ShowEmotion(Whichemotion);
            Npc.GetComponent<SlotOwner>().TargetSlot.TheOwner = null;

            Npc.GetComponent<SlotOwner>().TargetSlot = TheSlot;
            Npc.GetComponent<SlotOwner>().TargetSlot.TheOwner = Npc;
        }
        public Slot GetStandSlot()
        {
            Slot AvailableSlot = null;
            if (Stands.Length == 0)
                return null;
            int safe = 0;
            while (!AvailableSlot&&safe<5)
            {
                safe += 1;
                int rand = Random.Range(0, Stands.Length);
                if (Stands[rand].gameObject.activeInHierarchy)
                {
                    for (int r = 0; r < Stands[rand].StandPos.Slots.Length; r++)
                    {
                        if (Stands[rand].StandPos.Slots[r].TheOwner == null)
                        {
                            AvailableSlot = Stands[rand].StandPos.Slots[r];
                            break;
                        }
                    }
                }
            }


            //for (int i = 0; i < Stands.Length; i++)
            //{
            //    if (Stands[i].gameObject.activeInHierarchy)
            //    {
            //        for (int r = 0; r < Stands[i].StandPos.Slots.Length; r++)
            //        {
            //            if (Stands[i].StandPos.Slots[r].TheOwner == null)
            //            {
            //                AvailableSlot = Stands[i].StandPos.Slots[r];
            //                break;
            //            }
            //        }
            //        if (AvailableSlot)
            //            break;
            //    }
            //}
            return AvailableSlot;

        }
        public bool GetSeat(GameObject Npc, Emotion.TheEmotion Whichemotion)
        {
            Slot AvailableSlot = null;
            for (int i = 0; i < tables.Length; i++)
            {
                if (tables[i].gameObject.activeInHierarchy)
                {
                    for (int r = 0; r < tables[i].Seats.Slots.Length; r++)
                    {
                        if (tables[i].Seats.Slots[r].TheOwner == null &&
                            tables[i].Seats.Slots[r].GetComponent<Chair>().TrashStack.Stacks.Count == 0)
                        {
                            AvailableSlot = tables[i].Seats.Slots[r];
                            break;
                        }
                    }
                    if (AvailableSlot)
                        break;
                }
            }

            if (AvailableSlot)
            {
                NpcSeat(Npc, Whichemotion, AvailableSlot);
                return true;
            }
            else
            {
                return false;
            }
        }
        public bool SeatAvailable()
        {
            Slot AvailableSlot = null;
            for (int i = 0; i < tables.Length; i++)
            {
                if (tables[i].gameObject.activeInHierarchy)
                {
                    for (int r = 0; r < tables[i].Seats.Slots.Length; r++)
                    {
                        if (tables[i].Seats.Slots[r].TheOwner == null &&
                             tables[i].Seats.Slots[r].GetComponent<Chair>().TrashStack.Stacks.Count == 0)
                        {
                            AvailableSlot = tables[i].Seats.Slots[r];
                            break;
                        }
                    }
                    if (AvailableSlot)
                    {
                        break;
                    }
                    else
                    {

                    }
                }
            }

            if (AvailableSlot)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

    }
}
