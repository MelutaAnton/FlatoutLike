using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace AIE
{
    public class CashSpawner : MonoBehaviour
    {
        public float SpawnRate = 1;
        float SpawnTimestamp;

        private void Update()
        {
            if (SpawnTimestamp < Time.time)
            {
                Slot tempAvailableslot = GetComponent<SlotManager>().CheckAvaiableSlots(gameObject);
                if (tempAvailableslot)
                {
                    GetComponent<CashStack>().AddCash(tempAvailableslot);
                }
                SpawnTimestamp = Time.time + SpawnRate;
            }
        }

    }
}
