using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
namespace AIE
{
    public class NotificationUi : MonoBehaviour
    {
        float AliveTime;
        public Text DetailsText;
        Transform pos;
        GameObject notworld;
        public void Setup(string Details, Transform Trans)
        {
            pos = transform;
            AliveTime = Time.time + 3;
            DetailsText.text = Details;
            notworld = _GameManager.instance.notificationMan.CreateNotificationTarget(Trans);
        }

        void Update()
        {
            if (AliveTime < Time.time)
            {
                Destroy(gameObject);
            }
            notworld.GetComponent<NotificationWorld>().KeepAlive(0.2f);
        }
        public void KeepAlive(float TheT)
        {
            AliveTime = Time.time + TheT;
        }
    }
}
