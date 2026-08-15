using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
namespace AIE
{
    public enum UnlockType
    {
        None,
        Helper,
        DriveThrough
    }
    [System.Serializable]
    public class _Refresh : UnityEvent { }
    public class Unlock : MonoBehaviour
    {
        public GameObject LevelLockObj;
        public GameObject LockedObj;
        public GameObject UnlockObj;
        public bool UnlockedLockedAlwaysOn;
        public SpendMoney _spendMoney;
        public string PrefName;
        public UnlockType Type;
        public _Refresh refresh;
        private void Start()
        {
            PrefName = "_" + PrefName;
            float SavedCost = PlayerPrefs.GetFloat(PrefName + "SavedCost");
            _spendMoney.SetAmount(SavedCost);
            Refresh();
        }
        public void Refresh()
        {
            PlayerPrefs.SetFloat(PrefName + "SavedCost", _spendMoney.CashAmount);
            if (LevelLockObj.GetComponent<LevelLock>().IsLocked())
            {
                LevelLockObj.SetActive(true);
                LockedObj.SetActive(false);
                UnlockObj.SetActive(UnlockedLockedAlwaysOn);
            }
            else if (PlayerPrefs.GetInt(PrefName) == 0 && _spendMoney.MaxAmount != 0)
            {
                //Locked
                LockedObj.SetActive(true);
                UnlockObj.SetActive(UnlockedLockedAlwaysOn);
                LevelLockObj.SetActive(false);
            }
            else
            {
                PlayerPrefs.SetInt(PrefName, 1);
                //Unlocked
                LockedObj.SetActive(false);
                UnlockObj.SetActive(true);
                LevelLockObj.SetActive(false);
            }
            refresh.Invoke();
        }
        public void _Unlock()
        {
            PlayerPrefs.SetInt(PrefName, 1);

            Refresh();
            if (Type == UnlockType.Helper)
            {
                _GameManager.instance.tasksMan.TaskProgressAdd(TaskType.HireWorker, 1);
            }
            else if (Type == UnlockType.DriveThrough)
            {
                _GameManager.instance.tasksMan.TaskProgressAdd(TaskType.DriveThroughUnlock, 1);
            }
        }
        public bool IsUnlocked()
        {
            if (PlayerPrefs.GetInt(PrefName) == 1)
            {
                return true;
            }
            return false;
        }
        [ContextMenu("Lock")]
        public void ResetLock()
        {
            PlayerPrefs.SetInt(PrefName, 0);
            _spendMoney.SetAmount(0);
            Refresh();
        }
    }
}