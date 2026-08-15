using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace AIE
{
    public class ItemSpawner : MonoBehaviour
    {
        [Header("Important")]
        public GameObject ItemPref;
        public SlotManager slotman;
        public Transform SpawnPos;


        [Header("Attributes")]
        public float SpawnRate = 1;
        float SpawnTimestamp;
        public bool AutoSpawn;

        [Header("Miscelenious")]
        public GameObject FilllItem;

       
        private void Update()
        {
            if (AutoSpawn)
            {

                GameObject Go = ManualAutoSpawn();
                if (Go && FilllItem)
                {
                    GameObject[] fillobjs = new GameObject[slotman.Slots.Length];
                    for (int i = 0; i < fillobjs.Length; i++)
                    {
                        fillobjs[i] = FilllItem;
                    }
                    Go.GetComponentInChildren<SlotManager>().Fill(FilllItem);
                }
            }

            //if (SpawnTimestamp < Time.time)
            //{
            //    SpawnTimestamp = Time.time + SpawnRate;
            //    Spawn();
            //}
        }
        public GameObject ManualAutoSpawn()
        {
            if (SpawnTimestamp < Time.time)
            {
                SpawnTimestamp = Time.time + SpawnRate;
                return Spawn();
            }
            return null;
        }
        public GameObject Spawn()
        {
            Slot AvailbleSlot = slotman.CheckAvaiableSlots(gameObject);
            if (AvailbleSlot)
            {
                GameObject Go = null;
                Go = _GameManager.instance.poolman.GetItem(transform, ItemPref);

                //GameObject Go = Instantiate(CupPref, SpawnPos.position, AvailbleSlot.transform.rotation, transform);

                if (SpawnPos)
                {
                    Go.transform.position = SpawnPos.position;
                    Go.SetActive(true);
                    Go.GetComponent<TheItem>().MoveToTarget(AvailbleSlot.transform);

                }
                else
                {
                    Go.transform.position = AvailbleSlot.transform.position;
                    Go.SetActive(true);

                }
                Go.transform.rotation = AvailbleSlot.transform.rotation;
                GetComponent<ItemManager>().AddItem(Go, AvailbleSlot, false);
                return Go;

            }
            return null;
        }
        public bool IsFull()
        {
            Slot AvailbleSlot = slotman.CheckAvaiableSlots(gameObject);
            if (AvailbleSlot)
            {
                return false;
            }
            else
            {
                return true;
            }
        }
        public void PauseSpawn()
        {
            //Debug.Log("Pause");
            //SpawnTimestamp = Time.time + SpawnRate;
        }
    }
}
