using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace AIE
{
    public class CashStack : MonoBehaviour
    {
        public SlotManager slotman;
        public GameObject Cashpref;
        public Transform SpawnPos;
        public bool AutoSpawn;
        public float SpawnRate = 1;
        float SpawnTimestamp;
        public bool AddCash(Slot AvailableSlot)
        {
            if (!AvailableSlot)
            {
                AvailableSlot = slotman.CheckAvaiableSlots(gameObject);
            }

            if (AvailableSlot)
            {
                //GameObject Go = Instantiate(Cashpref, transform);
                GameObject Go = _GameManager.instance.poolman.GetCash(transform);
                Go.transform.position = SpawnPos.position;
                Go.transform.rotation = SpawnPos.rotation;
                Go.GetComponent<SlotOwner>().TargetSlot = AvailableSlot;
                AvailableSlot.TheOwner = Go;

                Go.SetActive(true);
                Go.GetComponent<TheCash>().MoveToTarget(AvailableSlot.transform);
                // Go.transform.position = AvailableSlot.transform.position;
                Go.transform.rotation = AvailableSlot.transform.rotation;
                Go.transform.SetParent(AvailableSlot.transform);

                return true;
            }
            return false;
        }
        private void Update()
        {
            if (AutoSpawn)
            {
                if (SpawnTimestamp < Time.time)
                {
                    Slot AvailbleSlot = slotman.CheckAvaiableSlots(gameObject);
                    if (AvailbleSlot)
                    {
                        AddCash(AvailbleSlot);
                    }
                    SpawnTimestamp = Time.time + SpawnRate;
                }
            }
        }
    }
}
