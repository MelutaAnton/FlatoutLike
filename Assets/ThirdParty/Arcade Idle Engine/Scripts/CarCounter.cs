using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
namespace AIE
{
    [System.Serializable] public class CarSellAction : UnityEvent { }
    public class CarCounter : MonoBehaviour
    {
        public StackRemover stacks;
        public CashStack cashstack;
        GameObject TargetItem;
        float waittimestamp;
        public Slot SellSlot;
        float BuyTime;
        public float SellingTimestamp;
        public Timer TheTimer;
        GameObject Notification;
        public CarSellAction TheSellAction;
        public CarQueLine queline;
        bool CustomerBuy;
        float playersellingtimestamp;
        public AudioSource SellSound;
        public CounterFloor counterfloor;

        private void Update()
        {
            if (SellingTimestamp > Time.time && stacks.Stacks.Count > 0 && queline.BuyerOnCounter())
            {
                TheTimer.gameObject.SetActive(true);
            }
            else
            {
                TheTimer.gameObject.SetActive(false);
            }
        }
        public void ActivateSelling(bool PlayerSelling)
        {
            SellingTimestamp = Time.time + 0.1f;
            if (PlayerSelling)
            {
                counterfloor.Activate();
                playersellingtimestamp = Time.time + 0.1f;
            }
        }
        public void Buy()
        {
            if (SellingTimestamp < Time.time)
                return;

            Slot Availableslot = cashstack.slotman.CheckAvaiableSlots(gameObject);
            if (!Availableslot)
            {
                if (!Notification)
                {
                    string msg = "COLLECT CASH";
                    Notification = _GameManager.instance.notificationMan.CreateNotification(msg, cashstack.transform);
                }
                else
                {
                    Notification.GetComponent<NotificationUi>().KeepAlive(0.5f);
                }
            }
            if (stacks.Stacks.Count > 0)
            {

                if (TargetItem == null && Availableslot)
                {
                    BuyTime = 0;
                    if (!stacks.Stacks[0].GetComponent<ItemManager>().slotman.CheckAvaiableSlots(gameObject))
                    {
                        TargetItem = stacks.Stacks[0];
                    }
                    // Debug.Log("Removed" + TargetCup.transform.name);
                    // waittimestamp = Time.time+0.1f;

                }
                else if (TargetItem && Availableslot)
                {

                    if (!TheTimer.IsFilled())
                    {
                        TheTimer.MaxTime = 1;
                        TheTimer.AddTime();

                    }
                    else
                    {
                        waittimestamp = Time.time + 0.1f;
                        if (Availableslot && !TargetItem.GetComponent<SlotOwner>().TargetSlot)
                        {
                            if (!CustomerBuy)
                            {

                                int count = _GameManager.instance.economyMan.Car_Counter_Price;
                                //while (TargetCup.GetComponent<SlotOwner>().TargetSlot == null)
                                //{
                                //    cashstack.AddCash(Availableslot);
                                //}
                                while (count > 0)
                                {
                                    count -= 1;
                                    Availableslot = cashstack.slotman.CheckAvaiableSlots(gameObject);
                                    if (Availableslot)
                                    {
                                        cashstack.AddCash(Availableslot);
                                    }
                                }
                                while (stacks.Stacks.Contains(TargetItem))
                                {
                                    stacks.RemoveSpecificStack(TargetItem);
                                }
                                //Destroy(TargetCup);
                                // stacks.Refresh();

                                TheSellAction.Invoke();
                                if (!CustomerBuy)
                                {
                                    if (playersellingtimestamp > Time.time)
                                    {
                                    }
                                    PlaySellSound();
                                    _GameManager.instance.tasksMan.TaskProgressAdd(TaskType.Sell, 4);

                                }
                                CustomerBuy = true;
                                SellSlot.TheOwner.GetComponent<TheNpcCar>().AddItem(TargetItem);
                                //int rating = GameManager.instance.leaderBoard._Rating;
                                //while (rating > 0)
                                //{
                                //    rating -= 1;

                                //    Slot tempAvailableslot = cashstack.slotman.CheckAvaiableSlots(gameObject);
                                //    cashstack.AddCash(tempAvailableslot); 
                                //}

                            }
                            if (CustomerBuy)
                            {

                                if (SellSlot.TheOwner.GetComponent<TheNpcCar>().TheStack.IsFull())
                                {
                                    RemoveCustomer();
                                }
                                else
                                {
                                    resetsell();
                                }

                            }


                        }

                    }
                    if (SellingTimestamp > Time.time)
                    {
                        BuyTime += Time.deltaTime;
                    }
                    else
                    {
                        BuyTime = 0;
                    }
                    if (waittimestamp < Time.time && SellSlot.TheOwner != null && BuyTime > 1)
                    {
                        BuyTime = 0;


                    }

                }
                else
                {
                    //TargetCup = null;
                }
            }
        }
        void resetsell()
        {
            TargetItem = null;
            BuyTime = 0;
            CustomerBuy = false;
            TheTimer.ResetTimer();
        }
        public void RemoveCustomer()
        {
            SellSlot.TheOwner.GetComponent<TheNpcCar>().Finished(TargetItem);
            SellSlot.TheOwner = null;
            TheTimer.ResetTimer();
            CustomerBuy = false;
            TargetItem = null;

        }
        public void PlaySellSound()
        {
            if (!SellSound.isPlaying)
            {
                SellSound.Play();
            }
        }
    }
}
