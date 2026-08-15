using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace AIE
{
    public class Stand : MonoBehaviour
    {
        public SlotManager StandPos;
        public void GetItem(TheNpc WhichNpc)
        {
            GameObject TargetItem=null;
            SlotManager Itemslots=GetComponent<StackRemover>().slotManager;
            for(int i = 0; i < Itemslots.Slots.Length; i++)
            {
                if (Itemslots.Slots[i].TheOwner&&
                    Itemslots.Slots[i].TheOwner.GetComponent<TheItem>().MoveTimestamp+1<Time.time)
                {
                    TargetItem = Itemslots.Slots[i].TheOwner;
                    break;
                }
            }
            if (TargetItem&&!WhichNpc.TheStack.IsFull())
            {
                TargetItem.GetComponent<SlotOwner>().TargetSlot.TheOwner = null;
                TargetItem.GetComponent<SlotOwner>().TargetSlot = null;
                WhichNpc.AddItem(TargetItem);

            }
        }
       
    }
}
