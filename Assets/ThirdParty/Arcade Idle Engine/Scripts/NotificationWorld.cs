using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
namespace AIE
{
    public class NotificationWorld : MonoBehaviour
    {
        float AliveTime;
        Transform trans;
        public Vector2 MaxBounds;
        public void Setup(Transform TheTrans)
        {
            trans = TheTrans;
            AliveTime = Time.time + 3;
            transform.position = new Vector2(Screen.safeArea.width, Screen.safeArea.height);
            MaxBounds = new Vector2(Screen.safeArea.width, Screen.safeArea.height);
        }

        void Update()
        {
            if (AliveTime < Time.time)
            {
                Destroy(gameObject);
            }
            Vector3 pos = Camera.main.WorldToScreenPoint(trans.position);

            float XSpace = 10;
            float YSpace = 10;
            if (pos.x > MaxBounds.x - XSpace)
            {
                pos.x = (MaxBounds.x) - XSpace;
            }
            else if (pos.x < XSpace)
            {
                pos.x = XSpace;
            }
            if (pos.y > MaxBounds.y - YSpace)
            {
                pos.y = (MaxBounds.y) - YSpace;
            }
            else if (pos.y < YSpace)
            {
                pos.y = YSpace;
            }

            if (pos.z > 0)
            {
                transform.position = pos;

            }
        }
        public void KeepAlive(float TheT)
        {
            AliveTime = Time.time + TheT;
        }
    }
}