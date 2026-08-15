using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace AIE
{
    public class Cargo_Vehicle : MonoBehaviour
    {
        public ThePath path;
        public float Speed = 5;
        int CurrentPos;
        Transform Target;
        public Transform ParkPos;
        public Transform OffloadPos;
        public float ParkTime;
        float StopTime;
        Transform LookTarget;
        float LookDist;
        public ItemSpawner Spawner;
        float OffloadTime;
        float WaitOffload;
        float CloseDoorTime;
        public Door TheDoor;
        GameObject Notification;
        public GameObject DeliveryItem;
        int currentdeliveryitem;
        private void Start()
        {
            CurrentPos = 1;
            Target = path.Points[CurrentPos];
            //transform.position = Target.position;
        }
        void Update()
        {

            if (StopTime < Time.time)
            {
                transform.position = Vector3.MoveTowards(transform.position, Target.position, Speed * Time.deltaTime);
                LookTarget = Target;
                LookDist = Vector3.Distance(transform.position, Target.position);


            }
            else
            {
                if (!OffloadPos.GetComponent<OffloadPoint>().CanUse(gameObject))
                {
                    StopTime = 0;
                    return;
                }
                transform.position = Vector3.MoveTowards(transform.position, OffloadPos.position, Speed / 2 * Time.deltaTime);
                LookTarget = ParkPos;
                LookDist = Vector3.Distance(transform.position, OffloadPos.position);
                if (Vector3.Distance(transform.position, OffloadPos.position) < 0.1f)
                {
                    OffloadTime += Time.deltaTime;
                    if (OffloadTime > 4)
                    {
                        TheDoor.CloseDoor();
                        WaitOffload = 0;
                        CloseDoorTime += Time.deltaTime;
                        if (CloseDoorTime > 1)
                        {
                            StopTime = 0;
                            CloseDoorTime = 0;
                            OffloadTime = 0;

                        }
                        else
                        {
                            StopTime = Time.time + 1;
                        }
                    }
                    else
                    {
                        WaitOffload += Time.deltaTime;
                        if (WaitOffload > 1)
                        {
                            GameObject Go = Spawner.ManualAutoSpawn();
                            if (Go)
                            {
                                Go.GetComponentInChildren<SlotManager>().Fill(DeliveryItem);
                            }
                            if (Spawner.IsFull())
                            {
                                if (!Notification)
                                {
                                    string msg = "Max";
                                    Notification = _GameManager.instance.notificationMan.CreateNotification(msg, Spawner.transform);
                                }
                                else
                                {
                                    Notification.GetComponent<NotificationUi>().KeepAlive(0.2f);
                                }
                            }
                        }
                        TheDoor.OpenDoor(transform.position);

                    }
                }
            }
            if (Vector3.Distance(transform.position, Target.position) < 0.1f)
            {
                if (Target.GetComponent<Path_Point>().UsedTimestamp < Time.time)
                {
                    Target.GetComponent<Path_Point>().UsedTimestamp = Time.time + 3;

                    CurrentPos += 1;
                    if (CurrentPos > path.Points.Length - 1)
                    {
                        CurrentPos = 0;
                        transform.position = path.Points[CurrentPos].position;
                        transform.rotation = path.Points[CurrentPos].rotation;
                    }

                    if (Target == ParkPos)
                    {
                        StopTime = Time.time + 6;
                    }
                    Target = path.Points[CurrentPos];
                    if (CurrentPos == 0)
                    {
                        //transform.position = Target.position;
                    }
                }


            }

            if (LookDist > 0.1f)
            {
                Vector3 relativePos = LookTarget.position - transform.position;

                // the second argument, upwards, defaults to Vector3.up
                Quaternion rotation = Quaternion.LookRotation(relativePos, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, rotation, 5 * Time.deltaTime);
            }
            else
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, LookTarget.rotation, 5 * Time.deltaTime);
            }
        }
       
        public void UnlockedAction()
        {
            for (int i = 0; i < path.Points.Length; i++)
            {
                if (path.Points[i] == ParkPos)
                {
                    CurrentPos = i;
                    Target = path.Points[i];
                    break;
                }
            }
            transform.position = path.Points[CurrentPos - 1].position;
            transform.rotation = path.Points[CurrentPos - 1].rotation;

            //transform.position = ParkPos.position;
            //transform.rotation = ParkPos.rotation;

        }
    }
}
