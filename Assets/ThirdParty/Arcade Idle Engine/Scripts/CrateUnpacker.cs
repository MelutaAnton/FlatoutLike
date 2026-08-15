using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace AIE
{
    public class CrateUnpacker : MonoBehaviour
    {
        public StackRemover CrateHolder;
        public ItemManager TargetArea;
        float CollectTimestamp;
        float Waittimestamp;
        GameObject UsedCratesNotification;
        [Header("Optional")]
        public ItemManager UsedCrates;

        private void Update()
        {
            Slot availableslot = TargetArea.slotman.CheckAvaiableSlots(gameObject);
            if (availableslot &&
                CrateHolder.Stacks.Count > 0)
            {
                if (CollectTimestamp < Time.time)
                {
                    SlotManager slotman = CrateHolder.Stacks[CrateHolder.Stacks.Count - 1].GetComponent<Crate>().slotman;
                    GameObject Target = null;
                    for (int i = slotman.Slots.Length - 1; i >= 0; i--)
                    {
                        if (slotman.Slots[i].TheOwner)
                        {
                            if (slotman.Slots[i].TheOwner.GetComponent<TheItem>()
                                )
                            {
                                if (slotman.Slots[i].TheOwner.GetComponent<TheItem>().CanMove(2)&&!slotman.Slots[i].TheOwner.GetComponent<TheItem>().IsMoving)
                                {
                                    Target = slotman.Slots[i].TheOwner;
                                    slotman.Slots[i].TheOwner = null;
                                    break;
                                }
                            }
                            else if (slotman.Slots[i].TheOwner.GetComponent<TheCash>()
                               )
                            {
                                if (!slotman.Slots[i].TheOwner.GetComponent<TheCash>().Moving)
                                {
                                    Target = slotman.Slots[i].TheOwner;
                                    slotman.Slots[i].TheOwner = null;
                                    break;
                                }
                            }
                            else if (slotman.Slots[i].TheOwner.GetComponentInParent<TheNpc>())
                            {

                            }
                            else
                            {
                                Target = slotman.Slots[i].TheOwner;
                                slotman.Slots[i].TheOwner = null;
                                break;
                            }

                        }
                    }
                    if (Target)
                    {
                        Waittimestamp = Time.time + 0.3f;

                        TargetArea.AddItem(Target, availableslot);
                        if (slotman)
                        {
                            slotman._Refresh();
                        }
                    }
                    else
                    {
                        if (UsedCrates)
                        {

                            if (UsedCrates.AddItem(slotman.GetComponentInParent<TheItem>().gameObject, null))
                            {
                                CrateHolder.RemoveLastStack();

                            }
                            else
                            {
                                if (!UsedCratesNotification)
                                {
                                    string msg = "STUCK";
                                    UsedCratesNotification = _GameManager.instance.notificationMan.CreateNotification(msg, UsedCrates.transform);
                                }
                                else
                                {
                                    UsedCratesNotification.GetComponent<NotificationUi>().KeepAlive(0.2f);
                                }
                            }
                        }
                        else
                        {
                            if (slotman)
                            {
                                slotman.GetComponentInParent<Crate>().Deactivate();
                                slotman.GetComponentInParent<TheItem>().gameObject.SetActive(false);
                                CrateHolder.RemoveLastStack();
                            }
                        }

                    }
                }

            }
        }
    }
}
