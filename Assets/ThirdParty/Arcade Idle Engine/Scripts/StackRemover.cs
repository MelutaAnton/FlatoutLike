using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
namespace AIE
{
    public enum RemoverType
    {
        Normal, Dustbin
    }
    [System.Serializable] public class AddAction : UnityEvent { }
    public class StackRemover : MonoBehaviour
    {
        [Header("Attributes")]
        public RemoverType TheType;
        public Step TargetStep;
        public GameObject TargetObj;
        public bool RejectPlayerStack = false;

        [Header("Attributes")]
        public bool ReverseAdd;
        public bool DontRefreshOnRemove;
        public bool DontLerpOnRemove;
        public bool CanLerp = true;
        public bool UseMax = false;
        public bool CanShowStuck = true;

        [Header("Attributes")]
        public int MaxSize = 5;
        public float spacing = 0.5f;

        [Header("Custom Events")]
        [Space(5)]
        public AddAction SuccessaddAction;
        public AddAction addAction;


        [Header("Important")]
        public Transform StackPos;
        public SlotManager slotManager;
        GameObject Notification;
       
        public GameObject ObjPref;
        string prefname;
        public float AddedTimestamp;
        public GameObject PackHolderPref;
        float fulltimestamp;

        public Timer TheTimer;
        public List<GameObject> Stacks = new List<GameObject>();


        private void Start()
        {
            if (!StackPos)
            {
                StackPos = transform;
            }
            return;
            prefname = "_SavedStackRemover_" + transform.GetSiblingIndex().ToString();
            SpawnSaved();
        }
        public bool CanAdd(GameObject Which)
        {
            if(Which.GetComponent<TheItem>().ActiveStep != TargetStep||
                TargetObj&&TargetObj.GetComponent<TheItem>().ItemName!= Which.GetComponent<TheItem>().ItemName)
            {
                return false;
            }
            return true;
        }
        public bool IsFull()
        {
            if (slotManager && !slotManager.CheckAvaiableSlots(gameObject))
            {
                return true;
            }
            if (!UseMax)
            {
                return false;
            }

            if (Stacks.Count >= MaxSize || fulltimestamp > Time.time)
            {
                //   Debug.Log("Full");
                if (PackHolderPref)
                {
                    if (Stacks[Stacks.Count - 1].GetComponent<ItemManager>().slotman.CheckAvaiableSlots(gameObject))
                    {
                        return false;
                    }
                }
                if (CanShowStuck)
                {
                    if (!Notification)
                    {
                        string msg = "STUCK";
                        Notification = _GameManager.instance.notificationMan.CreateNotification(msg, transform);
                    }
                    else
                    {
                        Notification.GetComponent<NotificationUi>().KeepAlive(0.1f);
                    }
                }
                return true;
            }
            return false;
        }
        public void AddFullTimestamp(float TheT)
        {
            fulltimestamp = Time.time + TheT;
        }
        public void AddStack(GameObject Which)
        {

            if (slotManager)
            {
                SlotsAdd(Which);
            }
            else
            {
                NormalAdd(Which);
            }
           
            // Debug.Log("Add"+Which.transform.name);
        }
        void NormalAdd(GameObject Which)
        {
            bool success = true;
            AddedTimestamp = Time.time;
            if (PackHolderPref)
            {
                GameObject prev = null;

                if (Stacks.Count > 0)
                {
                    prev = Stacks[Stacks.Count - 1];
                }
                else
                {
                    // prev = Instantiate(PackHolderPref, transform);
                    prev = _GameManager.instance.poolman.GetItem(StackPos,PackHolderPref);
                    prev.transform.position = StackPos.position;
                    // prev.transform.rotation = transform.rotation;
                    prev.SetActive(true);
                    Stacks.Add(prev);
                }
                if (prev)
                {
                    while (!prev.GetComponent<ItemManager>().AddItem(Which, null))
                    {
                        //prev = Instantiate(PackHolderPref, transform);
                        prev = _GameManager.instance.poolman.GetItem(StackPos, PackHolderPref);
                        prev.transform.position = StackPos.position;
                        //prev.transform.rotation = transform.rotation;
                        prev.SetActive(true);
                        Stacks.Add(prev);
                    }
                    Which = prev;

                }
                Refresh(!PackHolderPref.GetComponent<Crate>().IsPack);

            }
            else
            {
                if (ReverseAdd && Stacks.Count > 0)
                {
                    GameObject[] tempstacks = Stacks.ToArray();
                    Stacks.Add(null);
                    for (int i = 0; i < Stacks.Count - 1; i++)
                    {
                        Stacks[i + 1] = tempstacks[i];
                        // tempstacks[i].GetComponent<TheItem>().StopCorutines();
                    }
                    Stacks[0] = Which;
                    // Invoke(nameof(Refresh), 0.5f);
                    //Stacks.Add(Which);
                }
                else
                {
                    Stacks.Add(Which);

                }
                Refresh();


            }
            if (success)
            {
                Transform TheTarget = StackPos;
                if (!TheTarget)
                {
                    StackPos = transform;
                }
                Which.transform.parent = StackPos;
                Which.transform.eulerAngles = new Vector3(0, 0, 0);
                //  Which.transform.localPosition = new Vector3(0, ((float)Stacks.Count - 1) / 2, 0);
                Save();
                if (GetComponent<Dustbin>())
                {
                    Which.transform.SetParent(transform);
                }

            }
        }
        public GameObject GetFirstObj()
        {
            GameObject Which = null;
            if (slotManager)
            {
                for(int i = 0; i < slotManager.Slots.Length; i++)
                {
                    if (slotManager.Slots[i].TheOwner)
                    {
                        Which = slotManager.Slots[i].TheOwner;
                        break;
                    }
                }
            }
            else
            {
                if(Stacks.Count > 0)
                {
                    Which = Stacks[Stacks.Count - 1];
                }
            }
            return Which;
        }
        void SlotsAdd(GameObject Which)
        {
            AddedTimestamp = Time.time;
            Slot AvailableSlot = slotManager.CheckAvaiableSlots(gameObject);
            if (AvailableSlot)
            {
                if (PackHolderPref)
                {
                    GameObject prev = null;

                    prev = _GameManager.instance.poolman.GetItem(StackPos, PackHolderPref);
                    prev.transform.position = StackPos.position;
                    // prev.transform.rotation = transform.rotation;
                    prev.SetActive(true);
                    if (prev)
                    {
                        while (!prev.GetComponent<ItemManager>().AddItem(Which, null))
                        {
                            //prev = Instantiate(PackHolderPref, transform);
                            prev = _GameManager.instance.poolman.GetItem(StackPos, PackHolderPref);
                            prev.transform.position = StackPos.position;
                            //prev.transform.rotation = transform.rotation;
                            prev.SetActive(true);
                        }

                        Which = prev;

                    }
                }


                AvailableSlot.TheOwner = Which;
                Which.GetComponent<SlotOwner>().TargetSlot = AvailableSlot;

                Transform TheTarget = AvailableSlot.transform;
                Which.transform.parent = TheTarget;
                Which.transform.eulerAngles = new Vector3(0, 0, 0);
                Refresh();
                Save();

                Which.GetComponent<TheItem>().MoveToTarget(AvailableSlot.transform);
            }
        }
        [ContextMenu("Refresh")]
        public void Refresh(bool IsRemoving = false)
        {
            for (int i = 0; i < Stacks.Count; i++)
            {
                if (!Stacks[i].GetComponent<TheItem>().StackMoving)
                {
                    Stacks[i].GetComponent<TheItem>().StackMoving = false;
                    Stacks[i].GetComponent<TheItem>().StopCorutines();
                    Vector3 ThePos = new Vector3(0, (spacing * (float)i), 0);
                    if (CanLerp && !IsRemoving || CanLerp && IsRemoving && !DontLerpOnRemove)
                    {
                        Stacks[i].GetComponent<TheItem>().MoveToPos(StackPos.TransformPoint(ThePos));

                    }
                    else
                    {
                        //Debug.Log(Stacks[i].transform.parent.name);
                        Stacks[i].transform.position = StackPos.TransformPoint(ThePos);
                        //Stacks[i].transform.localPosition = ThePos;

                    }
                   
                }
            }
        }
        public void RemoveLastStack()
        {
            Stacks.RemoveAt(Stacks.Count - 1);
            Save();
        }
        public void RemoveSpecificStack(GameObject Which)
        {
            if (slotManager)
            {
              for(int i = 0; i < slotManager.Slots.Length; i++)
                {
                    if (slotManager.Slots[i].TheOwner == Which)
                    {
                        slotManager.Slots[i].TheOwner = null;
                    }
                }
            }
            else
            {
                for (int i = 0; i < Stacks.Count; i++)
                {
                    if (Stacks[i] == Which)
                    {
                        Stacks.RemoveAt(i);
                        break;
                    }
                }
            }
           
            if (!DontRefreshOnRemove)
            {
                Refresh(true);
            }
            Save();
        }
        public void Save()
        {
            int count = Stacks.Count;
            //  Debug.Log(count);
            PlayerPrefs.SetInt(prefname, count);
        }
        void SpawnSaved()
        {
            if (!ObjPref)
                return;
            int count = PlayerPrefs.GetInt(prefname);
            //Debug.Log(count);
            for (int i = 0; i < count; i++)
            {
                GameObject Go = Instantiate(ObjPref, transform);
                AddStack(Go);

                if (Go.GetComponent<TheCash>())
                {
                    Go.GetComponent<TheCash>().Moving = false;
                }
            }
        }
    }
}
