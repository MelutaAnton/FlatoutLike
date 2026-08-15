using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
namespace AIE
{
    public class Challenge_Gift : MonoBehaviour
    {
        public TheTask _Task;
        public Text HeaderText;
        public Text TaskText;
        void Start()
        {
            DoneCheck();
            Refresh();
        }
        public void AddChallenge(int Amount)
        {
            Debug.Log("Add" + Amount.ToString());
            string PrefName = "GiftTask" + transform.GetSiblingIndex().ToString();

            PlayerPrefs.SetFloat(PrefName + "Amount", PlayerPrefs.GetFloat(PrefName + "Amount") + Amount);

            Refresh();
        }
        public void Refresh()
        {
            string PrefName = "GiftTask" + transform.GetSiblingIndex().ToString();
            _Task.Amount = PlayerPrefs.GetFloat(PrefName + "Amount");
            string Description = "TheTask";
            switch (_Task.Type)
            {
                case TaskType.CarryItems:
                    Description = "CARRY ITEMS";
                    break;
                case TaskType.Sell:
                    Description = "SELL CUPS";
                    break;
                case TaskType.CarryCrate:
                    Description = "CARRY CRATE";
                    break;

            }
            HeaderText.text = Description;
            TaskText.text = _Task.Amount.ToString() + "/" + _Task._MaxAmount.ToString();
            if (_Task.Amount >= _Task._MaxAmount)
            {
                ///  Debug.Log("Done");
                //  ShowTaskDone();
                //Invoke(nameof(ConfirmCheck), 2);
            }

        }
        public void CheckReady()
        {
            if (_Task.Amount >= _Task._MaxAmount)
            {
                _GameManager.instance.tasksMan._TaskReward.ShowReward(_Task, false, this);
            }
        }
        public void Collected()
        {
            string PrefName = "GiftTask" + transform.GetSiblingIndex().ToString();
            PlayerPrefs.SetInt(PrefName + "Done", 1);
            DoneCheck();
        }
        void DoneCheck()
        {
            string PrefName = "GiftTask" + transform.GetSiblingIndex().ToString();
            if (PlayerPrefs.GetInt(PrefName + "Done") == 1)
            {
                gameObject.SetActive(false);
            }
            else
            {
                gameObject.SetActive(true);
            }
        }
        public void OnMouseDown()
        {
            // Debug.Log("Pressed");
            _GameManager.instance.tasksMan.DebugTaskType = _Task.Type;
        }

    }
}
