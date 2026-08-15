using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace AIE
{
    public class Crate : MonoBehaviour
    {
        public SlotManager slotman;
        public bool IsPack;
        public void Deactivate()
        {
            for(int i = 0; i < slotman.Slots.Length; i++)
            {
                if (slotman.Slots[i].TheOwner)
                {
                    slotman.Slots[i].TheOwner.GetComponent<SlotOwner>().TargetSlot = null;
                    slotman.Slots[i].TheOwner.gameObject.SetActive(false);
                    slotman.Slots[i].TheOwner.transform.SetParent(_GameManager.instance.poolman.transform);
                    slotman.Slots[i].TheOwner = null;
                }
            }
        }
        public void Close()
        {
            if (GetComponentInChildren<Animator>())
            {
                GetComponentInChildren<Animator>().Play("Close");
            }
            Deactivate();
        }
    }
}