using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace AIE
{
    public class CarQueLine : MonoBehaviour
    {
        public SlotManager slotman;
        public GameObject CarPref;
        public Transform SpawnPos;
        public Transform EndPos;
        public List<GameObject> Cars = new List<GameObject>();
        public List<GameObject> FinishedCars = new List<GameObject>();
        public CarCounter TheCounter;
        float Spawntimestamp;
        public Transform QueExitPos;
        bool smoothieiconshown;
        float starttimstamp;
        private void Start()
        {
            starttimstamp = Time.time + 5;
        }
        private void Update()
        {
            if (slotman.Slots[slotman.Slots.Length - 1].TheOwner == null)
            {
                if (Spawntimestamp < Time.time)
                {
                    Spawntimestamp = Time.time + 3f;
                    CreateCar();

                }
            }
            NpcMan();
            FinishedMan();
            if (slotman.Slots[0].TheOwner != null
                )
            {
                if (!slotman.Slots[0].TheOwner.GetComponent<TheNpcCar>().TheItem &&
                    slotman.Slots[0].TheOwner.GetComponent<TheNpcCar>().OnCounter)
                {
                    slotman.Slots[0].TheOwner.GetComponent<TheNpcCar>().AddWaitTime();

                }

            }
        }
        public void StopWaitTime()
        {
            if (slotman.Slots[0].TheOwner != null
               )
            {
                if (!slotman.Slots[0].TheOwner.GetComponent<TheNpcCar>().TheItem)
                {
                    slotman.Slots[0].TheOwner.GetComponent<TheNpcCar>().StopWaitTime();

                }
            }
        }
        public bool BuyerOnCounter()
        {
            if (slotman.Slots[0].TheOwner != null
               )
            {
                if (slotman.Slots[0].TheOwner.GetComponent<TheNpcCar>().OnCounter)
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

        void NpcMan()
        {
            for (int i = 0; i < Cars.Count; i++)
            {
                GameObject Npc = Cars[i];
                Transform Target = Npc.GetComponent<SlotOwner>().TargetSlot.transform;
                Npc.transform.position = Vector3.MoveTowards(Npc.transform.position, Target.position, 2 * Time.deltaTime);
                float Dist = Vector3.Distance(Npc.transform.position, Target.position);
                if (Dist < 0.1f)
                {
                    if (Npc.GetComponent<TheNpcCar>().CurrentPos == 0)
                    {
                        TheCounter.Buy();
                        if (!Npc.GetComponent<TheNpcCar>().OnCounter)
                        {
                            Npc.GetComponent<TheNpcCar>().CounterHolder.SetActive(true);
                        }
                        Npc.GetComponent<TheNpcCar>().OnCounter = true;

                    }
                    else
                    {
                        if (!slotman.Slots[Npc.GetComponent<TheNpcCar>().CurrentPos - 1].TheOwner)
                        {
                            Npc.GetComponent<TheNpcCar>().CurrentPos -= 1;
                            Npc.GetComponent<SlotOwner>().TargetSlot.TheOwner = null;
                            Npc.GetComponent<SlotOwner>().TargetSlot = slotman.Slots[Npc.GetComponent<TheNpcCar>().CurrentPos];
                            slotman.Slots[Npc.GetComponent<TheNpcCar>().CurrentPos].TheOwner = Npc;
                            if (Npc.GetComponent<TheNpcCar>().CurrentPos == 0 &&
                               !smoothieiconshown)
                            {
                                smoothieiconshown = true;

                            }
                        }
                    }
                    //  if()
                }
                else
                {

                    Npc.transform.LookAt(Target.position);

                    if (Npc.GetComponent<TheNpcCar>().CurrentPos > 1 &&
                        !slotman.Slots[Npc.GetComponent<TheNpcCar>().CurrentPos - 1].TheOwner)
                    {
                        Npc.GetComponent<TheNpcCar>().CurrentPos -= 1;
                        Npc.GetComponent<SlotOwner>().TargetSlot.TheOwner = null;
                        Npc.GetComponent<SlotOwner>().TargetSlot = slotman.Slots[Npc.GetComponent<TheNpcCar>().CurrentPos];
                        slotman.Slots[Npc.GetComponent<TheNpcCar>().CurrentPos].TheOwner = Npc;
                        //if (Npc.GetComponent<TheNpc>().CurrentPos == 0)
                        //{
                        //    Npc.GetComponent<TheNpc>().TheEmotion.ShowEmotion(Emotion.TheEmotion.Smoothie);

                        //}
                    }
                }

            }
        }
        IEnumerator MoveToQueExit(Transform Which)
        {
            float TheT = 0.5f;
            while (TheT > 0)
            {
                TheT -= Time.deltaTime;
                Which.transform.position = Vector3.MoveTowards(Which.transform.position, QueExitPos.position, 2 * Time.deltaTime);
                yield return new WaitForSeconds(0.00001f);
            }
        }
        void FinishedMan()
        {
            for (int i = 0; i < FinishedCars.Count; i++)
            {
                GameObject Npc = FinishedCars[i];
                Transform Target = EndPos;
                Npc.transform.position = Vector3.MoveTowards(Npc.transform.position, Target.position, 2 * Time.deltaTime);
                float Dist = Vector3.Distance(Npc.transform.position, Target.position);
                if (Dist < 0.1f)
                {

                    FinishedCars.RemoveAt(i);
                    Npc.GetComponent<TheNpcCar>().TheEmotion.StopEmotions();
                    if (Npc.GetComponent<TheNpcCar>().TheItem)
                    {
                        Npc.GetComponent<TheNpcCar>().TheItem.SetActive(false);
                        SlotManager theslot = Npc.GetComponent<TheNpcCar>().TheItem.GetComponent<Crate>().slotman;
                        for (int r = 0; r < theslot.Slots.Length; r++)
                        {
                            if (theslot.Slots[r].TheOwner)
                            {
                                theslot.Slots[r].TheOwner.GetComponent<SlotOwner>().TargetSlot = null;
                                theslot.Slots[r].TheOwner.SetActive(false);
                                theslot.Slots[r].TheOwner = null;

                            }
                        }
                        Npc.GetComponent<TheNpcCar>().TheItem = null;
                    }
                    Npc.GetComponent<TheNpcCar>().RemoveItem();
                    // Destroy(Npc);
                    Npc.SetActive(false);

                    break;
                }
                else
                {
                    Npc.transform.LookAt(Target.position);

                }


            }
        }
        void CreateCar()
        {
            GameObject Target = slotman.Slots[slotman.Slots.Length - 1].gameObject;
            Transform _spawnpos = SpawnPos;
            //if (PlayerPrefs.GetInt("Tutorial") == 0||starttimstamp>Time.time)
            //{
            //    _spawnpos = slotman.Slots[slotman.Slots.Length - 1].transform;
            //}
            // GameObject Go= Instantiate(CarPref,_spawnpos.position,SpawnPos.transform.rotation,transform);
            GameObject Go = _GameManager.instance.poolman.GetNpcCar(transform);
            Go.transform.position = _spawnpos.position;
            Go.transform.rotation = _spawnpos.rotation;
            Cars.Add(Go);
            Go.GetComponent<TheNpcCar>().CurrentPos = slotman.Slots.Length - 1;
            Target.GetComponent<Slot>().TheOwner = Go;
            Go.GetComponent<SlotOwner>().TargetSlot = Target.GetComponent<Slot>();
            Go.GetComponent<TheNpcCar>().SetUp();
        }
        public void CarFinished(GameObject Npc, Emotion.TheEmotion Whichemotion)
        {

            for (int i = 0; i < Cars.Count; i++)
            {
                if (Cars[i] == Npc)
                {
                    StartCoroutine(MoveToQueExit(Npc.transform));
                    Cars.RemoveAt(i);
                    break;
                }
            }
            FinishedCars.Add(Npc);
            Npc.GetComponent<TheNpcCar>().TheEmotion.ShowEmotion(Whichemotion);
            Npc.GetComponent<SlotOwner>().TargetSlot.TheOwner = null;

        }

    }
}
