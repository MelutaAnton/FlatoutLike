using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
namespace AIE
{
    [System.Serializable] public class RefreshAction : UnityEvent { }
    public class SlotManager : MonoBehaviour
    {
        [Header("Attributes")]
        public Step TargetStep;


        [Header("Important")]
        public Slot[] Slots;

        [Header("Custom Event")]
        [Space(5)]
        public RefreshAction Refresh;

        [Header("Save Options")]
        public GameObject ObjPref;
        public bool AutoPrefName = true;
        public string prefname;
        public bool IsStartFill = false;



        [Header("Miscelenious")]
        public int AssignedSlots_;
        public int TotalSlots_;
      
      
        private void Start()
        {
            if (IsStartFill)
            {
                if (!ObjPref)
                    return;
                Fill(ObjPref);
            }
            else
            {
                if (AutoPrefName)
                {
                    if (transform.parent)
                    {
                        prefname = "_" + transform.parent.GetSiblingIndex().ToString() + "_" + transform.GetSiblingIndex().ToString() + "_" + "_SavedSlots_" + prefname + transform.GetSiblingIndex().ToString();

                    }
                    else
                    {
                        prefname = "_" + "_" + transform.GetSiblingIndex().ToString() + "_" + "_SavedSlots_" + prefname + transform.GetSiblingIndex().ToString();

                    }
                }
                SpawnSaved();
            }


        }
        [ContextMenu("Fill")]
        void TestFill()
        {
            if (!ObjPref)
                return;
            Fill(ObjPref);
            return;

            int count = Slots.Length;
            // Debug.Log(count);
            for (int i = 0; i < count; i++)
            {
                if (i > Slots.Length - 1)
                    return;

                GameObject Go = null;
                if (ObjPref.GetComponent<TheCash>())
                {
                    Go = _GameManager.instance.poolman.GetCash(Slots[i].transform);
                    Go.transform.position = Slots[i].transform.position;
                    // Go.transform.rotation = Slots[i].transform.rotation;
                    Go.SetActive(true);
                }
                else
                {
                    Go = Instantiate(ObjPref, Slots[i].transform);
                }
                Slots[i].TheOwner = Go;

                if (Go.GetComponent<TheCash>())
                {
                    Go.GetComponent<TheCash>().Moving = false;
                    Go.GetComponent<SlotOwner>().TargetSlot = Slots[i];
                }
            }
            if (gameObject.GetComponentInParent<DiningTable>())
            {
                GetComponentInParent<DiningTable>().FliesRefresh();
            }
        }
        public void Fill(GameObject fillObj)
        {
            for (int i = 0; i < Slots.Length; i++)
            {
                if (!Slots[i].TheOwner)
                {
                    //GameObject Go = Instantiate(fillObj, Slots[i].transform);
                    GameObject Go = _GameManager.instance.poolman.GetItem(Slots[i].transform,fillObj);
                    Go.transform.position = Slots[i].transform.position;
                    Go.transform.rotation = Slots[i].transform.rotation;
                    Go.GetComponent<TheItem>().SetStep(TargetStep);
                    Go.SetActive(true);
                    Slots[i].TheOwner = Go;
                    Slots[i].TheOwner.GetComponent<SlotOwner>().TargetSlot = Slots[i];
                }
            }

        }
        public void _Refresh()
        {
            Refresh.Invoke();
            Save();
        }
        public Slot AssignSlot(GameObject Assignee)
        {
            Slot AvailableSlot = CheckAvaiableSlots(Assignee);
            if (AvailableSlot)
            {
                //Assign
                AvailableSlot.TheOwner = Assignee;
            }
            return AvailableSlot;
        }

        public void RemoveSlotOwner(Slot _slot)
        {
            _slot.TheOwner = null;
        }

        public Slot CheckAvaiableSlots(GameObject Checker)
        {
            Slot Which = null;
            for (int i = 0; i < Slots.Length; i++)
            {
                if (Slots[i].TheOwner == null)
                {
                    Which = Slots[i];
                    return Which;
                }
            }
            return Which;
        }

        public int GetAssignedSlots()
        {
            int Count = 0;
            for (int i = 0; i < Slots.Length; i++)
            {
                if (Slots[i].TheOwner != null)
                {
                    Count += 1;
                }

            }
            AssignedSlots_ = Count;
            return Count;
        }

        public int GetTotalSlots()
        {
            TotalSlots_ = Slots.Length;
            return TotalSlots_;
        }
        void SaveSlotStacks()
        {
            for (int i = 0; i < Slots.Length; i++)
            {
                if (Slots[i].TheStack != null)
                {
                    PlayerPrefs.SetInt(prefname + "Slot_" + i.ToString(), Slots[i].TheStack.Stacks.Count);

                }

            }
        }
        public void Save()
        {
            int count = GetAssignedSlots();
            //  Debug.Log(count);
            PlayerPrefs.SetInt(prefname, count);
            SaveSlotStacks();
        }
        void SpawnSavedStacks()
        {
            for (int i = 0; i < Slots.Length; i++)
            {
                if (Slots[i].TheStack != null)
                {
                    int Amount = PlayerPrefs.GetInt(prefname + "Slot_" + i.ToString());
                    for (int r = 0; r < Amount; r++)
                    {
                        GameObject Go = SpawnObj(Slots[i].TheStack.transform);
                        Slots[i].TheStack.AddStack(Go);
                    }
                }

            }
        }
        GameObject SpawnObj(Transform Parent)
        {
            GameObject Go = null;
            if (ObjPref.GetComponent<TheCash>())
            {
                Go = _GameManager.instance.poolman.GetCash(Parent);
                Go.transform.position = Parent.position;
                // Go.transform.rotation = Slots[i].transform.rotation;
                Go.SetActive(true);
            }
            else if (ObjPref.GetComponent<TheItem>())
            {
                Go = _GameManager.instance.poolman.GetItem(Parent.transform,ObjPref);
                Go.transform.position = Parent.transform.position;
                //Go.transform.rotation = Slots[i].transform.rotation;
                Go.SetActive(true);
            }
            else
            {
                Go = Instantiate(ObjPref, Parent.transform);
            }
            if (Go.GetComponent<TheCash>())
            {
                Go.GetComponent<TheCash>().Moving = false;

            }
            else if (Go.GetComponent<TheItem>())
            {
                Go.GetComponent<TheItem>().SetStep(TargetStep);
                Go.transform.eulerAngles = new Vector3(0, 0, 0);
            }
            return Go;
        }
        void SpawnSaved()
        {
            if (!ObjPref)
                return;
            int count = PlayerPrefs.GetInt(prefname);
            // Debug.Log(count);
            for (int i = 0; i < count; i++)
            {
                if (i > Slots.Length - 1)
                    break;
                GameObject Go = SpawnObj(Slots[i].transform);
                Slots[i].TheOwner = Go;


                Go.GetComponent<SlotOwner>().TargetSlot = Slots[i];

            }
            if (gameObject.GetComponentInParent<DiningTable>())
            {
                GetComponentInParent<DiningTable>().FliesRefresh();
            }
            SpawnSavedStacks();
        }
    }
}
