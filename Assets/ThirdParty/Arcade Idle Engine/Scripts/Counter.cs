using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
namespace AIE
{
    [System.Serializable] public class SellAction : UnityEvent { }
    public class Counter : MonoBehaviour
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
        public SellAction TheSellAction;
        public QueLine queline;
        bool CustomerBuy;
        public bool RequireSeat;
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
          
           if (stacks.Stacks.Count > 0|| SellSlot.TheOwner.GetComponent<TheNpc>().RemainingCount <= 0)
            {
                if (TargetItem == null&& stacks.Stacks.Count > 0 && Availableslot)
                {

                    BuyTime = 0;
                    TargetItem = stacks.Stacks[stacks.Stacks.Count - 1];
                    // Debug.Log("Removed" + TargetCup.transform.name);
                    // waittimestamp = Time.time+0.1f;

                }
                else if (TargetItem && Availableslot||Availableslot&& SellSlot.TheOwner.GetComponent<TheNpc>().RemainingCount <= 0)
                {

                    if (!TheTimer.IsFilled())
                    {
                        if (playersellingtimestamp > Time.time)
                        {
                            TheTimer.MaxTime = (1 / ((float)_GameManager.instance.upgradeManager.UpgradeHud.Playerupgrade._Time + 1));

                        }
                        else
                        {
                            TheTimer.MaxTime = (1 / ((float)_GameManager.instance.upgradeManager.UpgradeHud.Workerupgrade._Time + 1));

                        }
                        TheTimer.AddTime();

                    }
                    else
                    {
                        waittimestamp = Time.time + 0.1f;
                        if (Availableslot&&TargetItem && !TargetItem.GetComponent<SlotOwner>().TargetSlot||
                            Availableslot&& SellSlot.TheOwner.GetComponent<TheNpc>().RemainingCount <= 0)
                        {

                            if (!CustomerBuy)
                            {
                               
                                TheSellAction.Invoke();
                                if (!CustomerBuy)
                                {
                                    PlaySellSound();
                                    _GameManager.instance.tasksMan.TaskProgressAdd(TaskType.Sell, 1);

                                }
                                CustomerBuy = true;

                                int ExtraCash = _GameManager.instance.economyMan.Npc_Counter_Price;
                                if (SellSlot.TheOwner.GetComponent<TheNpc>().Anim.GetComponent<NpcModel>().IsVip)
                                {
                                    ExtraCash = _GameManager.instance.economyMan.Vip_Counter_Price;
                                }
                                if (TargetItem)
                                {
                                    while (stacks.Stacks.Contains(TargetItem))
                                    {
                                        stacks.RemoveSpecificStack(TargetItem);
                                    }
                                    SellSlot.TheOwner.GetComponent<TheNpc>().AddItem(TargetItem);

                                }
                                else
                                {
                                    ExtraCash *= (SellSlot.TheOwner.GetComponent<TheNpc>().TheStack.Stacks.Count);
                                }
                                while (ExtraCash > 0)
                                {
                                    ExtraCash -= 1;

                                    Slot tempAvailableslot = cashstack.slotman.CheckAvaiableSlots(gameObject);
                                    if (tempAvailableslot)
                                    {
                                        cashstack.AddCash(tempAvailableslot);
                                    }
                                }

                            }
                            if (CustomerBuy)
                            {
                                if (SellSlot.TheOwner.GetComponent<TheNpc>().TheStack.IsFull()||
                                    SellSlot.TheOwner.GetComponent<TheNpc>().UseStands&&stacks.Stacks.Count==0)
                                {
                                    if (RequireSeat)
                                    {
                                        if (!queline.SeatAvailable())
                                        {
                                            SellSlot.TheOwner.GetComponent<TheNpc>().TheEmotion.ShowEmotion(Emotion.TheEmotion.Seat);

                                        }
                                        else
                                        {
                                            RemoveCustomer();
                                        }
                                    }
                                    else
                                    {
                                        RemoveCustomer();

                                    }
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
        void Earn()
        {

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
            SellSlot.TheOwner.GetComponent<TheNpc>().Finished(TargetItem);
            if (TargetItem.GetComponent<Crate>())
            {
                TargetItem.GetComponent<Crate>().Close();
            }
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
