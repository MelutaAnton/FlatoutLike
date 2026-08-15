using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace AIE
{
    public class UnpackCrateStack : MonoBehaviour
    {
        public StackRemover CrateHolder;
        public ItemManager UsedCrates;
        public ItemManager TargetSlots;
        float Waittimestamp;
        float CollectTimestamp;
        GameObject Notification;
        GameObject UsedCratesNotification;
        private void Update()
        {

            if (CrateHolder.Stacks.Count > 0 &&
                Waittimestamp < Time.time)
            {
                Waittimestamp = Time.time + 0.5f;
                if (CollectTimestamp < Time.time)
                {
                    SlotManager slotman = CrateHolder.Stacks[CrateHolder.Stacks.Count - 1].GetComponent<Crate>().slotman;
                    GameObject Target = null;
                    Slot availableslot = TargetSlots.slotman.CheckAvaiableSlots(gameObject);
                    if (availableslot)
                    {
                        for (int i = slotman.Slots.Length - 1; i >= 0; i--)
                        {
                            if (slotman.Slots[i].TheOwner)
                            {

                                if (slotman.Slots[i].TheOwner.GetComponent<TheItem>()
                                    )
                                {
                                    if (!slotman.Slots[i].TheOwner.GetComponent<TheItem>().IsMoving)
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
                    }
                    if (availableslot)
                    {
                        if (Target)
                        {
                            // Debug.Log(Target.name);
                            Waittimestamp = Time.time + 0.3f;

                            TargetSlots.AddItem(Target, null);
                            if (slotman)
                            {
                                slotman._Refresh();
                            }
                        }
                        else
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
                    }
                }

            }
        }
    }
}
