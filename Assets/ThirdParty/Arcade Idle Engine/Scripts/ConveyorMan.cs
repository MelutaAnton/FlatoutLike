using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace AIE
{
    public class ConveyorMan : MonoBehaviour
    {
        public StackRemover stacks;
        GameObject TargetItem;
        float waittimestamp;
        public ConveyorBelt conveyorbelt;
        public float ProcessTime = 1;
        public void PauseMove()
        {
            // waittimestamp = Time.time+1;
        }
        private void Update()
        {
            if (waittimestamp > Time.time)
            {
                return;
            }




            Slot Availableslot = conveyorbelt.slotman.CheckAvaiableSlots(gameObject);




            if (stacks.Stacks.Count > 0)
            {
                if (TargetItem == null && Availableslot)
                {

                    TargetItem = stacks.Stacks[stacks.Stacks.Count - 1];
                    // Debug.Log("Removed" + TargetCup.transform.name);
                    // waittimestamp = Time.time+0.1f;

                }
                else if (TargetItem && Availableslot)
                {

                    if (Availableslot && !TargetItem.GetComponent<SlotOwner>().TargetSlot)
                    {
                        if (conveyorbelt.AddItem(TargetItem))
                        {
                            while (stacks.Stacks.Contains(TargetItem))
                            {
                                stacks.RemoveSpecificStack(TargetItem);
                            }
                            TargetItem = null;
                            stacks.Refresh();
                        }


                    }

                }
            }

        }
    }
}
