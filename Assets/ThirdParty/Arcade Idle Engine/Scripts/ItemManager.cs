using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace AIE
{
    public class ItemManager : MonoBehaviour
    {
        public SlotManager slotman;
        public bool AddItem(GameObject Go, Slot AvailableSlot, bool CanMove = true, bool deactivateonarrive = false)
        {
            if (!AvailableSlot)
            {
                AvailableSlot = slotman.CheckAvaiableSlots(gameObject);
            }

            if (AvailableSlot)
            {
                // Go.transform.position= AvailableSlot.transform.position;
                if (CanMove)
                {
                    Go.GetComponent<TheItem>().MoveToTarget(AvailableSlot.transform,deactivateonarrive);
                }
                // Go.transform.rotation = AvailableSlot.transform.rotation;
                Go.transform.eulerAngles = new Vector3(0, 0, 0);
                Go.transform.parent = AvailableSlot.transform;
                //Go.transform.SetParent(AvailableSlot.transform);
                AvailableSlot.TheOwner = Go;
                Go.GetComponent<SlotOwner>().TargetSlot = AvailableSlot;
                return true;
            }
            return false;
        }
    }
}
