using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
namespace AIE
{
    [System.Serializable] public class BeforeConvertEvent : UnityEvent { }
    [System.Serializable] public class ConvertEvent : UnityEvent { }
    public class ItemConverter : MonoBehaviour
    {
        public float WaitTime = 1;
        public float ProcessTime = 1;
        float actualprocesstime;
        public int Multiplier = 1;
        float timestamp;
        public StackRemover _StackRemover;
        public GameObject FinalItem;
        public Transform SpawnPos;
        public ItemManager FinalArea;
        public BeforeConvertEvent beforeConvertEvent;
        public ConvertEvent convertEvent;
        GameObject TargetObj;
        private void Update()
        {
            if (_StackRemover)
            {
                if (!TargetObj) {
                    if (timestamp < Time.time)
                    {
                        timestamp = Time.time + WaitTime;

                        TargetObj = _StackRemover.GetFirstObj();
                        if (TargetObj)
                        {
                            actualprocesstime = ProcessTime;
                            beforeConvertEvent.Invoke();
                        }
                        
                    }
                }
                else
                {
                    actualprocesstime-=Time.deltaTime;
                    TargetObj.transform.localScale = Vector3.one*(actualprocesstime/ProcessTime);

                    if (actualprocesstime < 0)
                    {
                        if (TargetObj &&
                            TargetObj.GetComponent<TheItem>().CanMove())
                        {
                            if (Convert(TargetObj))
                            {
                                _StackRemover.RemoveSpecificStack(TargetObj);
                                TargetObj = null;
                                actualprocesstime = ProcessTime;
                                timestamp = Time.time + WaitTime;

                            }
                        }
                    }
                }
                
              
            }
        }
        public bool Convert(GameObject Which)
        {
            bool IsSuccess = false;
            Slot availableslot = FinalArea.slotman.CheckAvaiableSlots(gameObject);
            if (availableslot)
            {
                Which.SetActive(false);
                int _multiplier = Multiplier;
                while (_multiplier > 0&&availableslot)
                {
                    _multiplier -= 1;
                    GameObject NewObj = _GameManager.instance.poolman.GetItem(transform, FinalItem);
                    NewObj.transform.position = SpawnPos.position;
                    NewObj.SetActive(true);
                    if (FinalArea.AddItem(NewObj, availableslot))
                    {
                        convertEvent.Invoke();

                    }
                    availableslot = FinalArea.slotman.CheckAvaiableSlots(gameObject);
                }
               
                IsSuccess = true;

            }
            return IsSuccess;
        }
    }
}
