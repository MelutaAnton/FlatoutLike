using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace AIE
{
    public class NotificationMan : MonoBehaviour
    {
        public Transform SpawnParent;
        public Transform WorldUiSpawnParent;
        public GameObject NotificationPref;
        public GameObject NotificationWorldPref;

        [ContextMenu("Test")]
        void Test()
        {
            CreateNotification("Testing", Camera.main.transform);
        }
        public GameObject CreateNotification(string Details, Transform TheTrans)
        {
            GameObject Go = Instantiate(NotificationPref, SpawnParent);
            Go.GetComponent<NotificationUi>().Setup(Details, TheTrans);
            //if (Details == "NO SMOOTHIE" || Details == "COLLECT CASH")
            //{
            //    GameManager.instance.cam.ShowcasePoi(TheTrans);
            //}

            return Go;
        }
        public GameObject CreateNotificationTarget(Transform TheTrans)
        {
            GameObject Go = Instantiate(NotificationWorldPref, WorldUiSpawnParent);
            Go.GetComponent<NotificationWorld>().Setup(TheTrans);
            return Go;
        }
    }
}
