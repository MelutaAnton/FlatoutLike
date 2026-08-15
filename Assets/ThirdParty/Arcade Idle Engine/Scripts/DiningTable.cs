using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace AIE
{
    public class DiningTable : MonoBehaviour
    {
        public SlotManager Seats;
        public CashStack cashstack;
        public AudioSource DrinkingSound;
        public AudioSource EnjoySound;
        float Playtimestamp;
        GameObject Notification;
        public GameObject FliesObj;
        public void PlayDrinkSound()
        {
            // return;

            if (!DrinkingSound.isPlaying)
            {
                DrinkingSound.Play();
            }
            Playtimestamp = Time.time + 0.5f;
            Invoke(nameof(DeactivateSound), 1);
        }
        public void FliesRefresh()
        {
            return;
            if (Seats.CheckAvaiableSlots(gameObject) == null)
            {
                //FliesObj.SetActive(true);
                Invoke(nameof(ActivateFlies), 15);
            }
            else
            {
                FliesObj.SetActive(false);
            }
        }
        void ActivateFlies()
        {
            if (Seats.CheckAvaiableSlots(gameObject) == null)
            {
                //Debug.Log("Activate");
                FliesObj.SetActive(true);
            }
            else
            {
                FliesObj.SetActive(false);
            }
        }
        void DeactivateSound()
        {
            if (Playtimestamp > Time.time)
            {
                Invoke(nameof(DeactivateSound), 1);
            }
            else
            {
                DrinkingSound.Stop();
            }
        }
        public void PlayEnjoySound()
        {
            EnjoySound.Play();

        }
        public void ShowCollectNotification()
        {
            if (!Notification)
            {
                string msg = "COLLECT CASH";
                Notification = _GameManager.instance.notificationMan.CreateNotification(msg, cashstack.transform);
            }
            else
            {
                Notification.GetComponent<NotificationUi>().KeepAlive(0.3f);
            }
        }
    }
}
