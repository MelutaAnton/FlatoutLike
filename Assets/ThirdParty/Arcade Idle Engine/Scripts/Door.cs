using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace AIE
{
    public class Door : MonoBehaviour
    {
        public DoorObj[] doors;
        public AudioSource DoorSound;
        public bool IsOpen;
        public LevelLock _LevelLock;

       

        public void OpenDoor(Vector3 Pos)
        {
            if (_LevelLock && _LevelLock.IsLocked())
                return;
            if (!IsOpen)
            {
                IsOpen = true;
                if (DoorSound)
                {
                    DoorSound.Play();
                }
            }
            Pos = transform.InverseTransformPoint(Pos);
            if (Pos.z < 0)
            {
                //Debug.Log("Front");
                for (int i = 0; i < doors.Length; i++)
                {
                    doors[i].Open(true);
                }
            }
            else
            {
                // Debug.Log("Back");
                for (int i = 0; i < doors.Length; i++)
                {
                    doors[i].Open(false);
                }
            }
        }
        public void FrontOpen()
        {
            OpenDoor(transform.position);
        }
        public void CloseDoor()
        {
            if (IsOpen)
            {
                IsOpen = false;
                if (DoorSound)
                {
                    DoorSound.Play();
                }
            }
        }
       
        private void OnTriggerStay(Collider other)
        {

            if (other.transform.GetComponentInParent<TheNpc>())
            {
                OpenDoor(other.transform.GetComponentInParent<TheNpc>().transform.position);
            }else if (other.transform.GetComponentInParent<Player>())
            {
                OpenDoor(other.transform.GetComponentInParent<Player>().transform.position);
            }
            else if (other.transform.GetComponentInParent<Helper>())
            {
                OpenDoor(other.transform.GetComponentInParent<Helper>().transform.position);
            }
        }

    }
}