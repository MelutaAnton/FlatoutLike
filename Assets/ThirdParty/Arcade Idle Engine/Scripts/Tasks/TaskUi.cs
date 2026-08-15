using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
namespace AIE
{
    public class TaskUi : MonoBehaviour
    {
        public Text TaskText;
        float showtimestamp;
        public Transform Holder;
        public float Speed = 5;
        public bool TaskDone;
        public bool RewardClaimed;
        TheTask _Task;
        public Image SliderImg;
        public Text ProgressText;
        public GameObject GiftObj;
        public GameObject flashObj;
        float FlashTimestamp;
        public Text LevelText;
        public void SetUp(TheTask task)
        {
            used = false;
            _Task = task;
            Holder.transform.localPosition = new Vector3(-1000, 0, 0);
            showtimestamp = 3;
            TaskDone = false;
            RewardClaimed = false;
            GiftObj.GetComponent<Animator>().Rebind();
            GiftObj.GetComponent<Animator>().Play("Rest");
            GiftObj.GetComponentInChildren<Button>().interactable = false;
            gameObject.SetActive(true);
            Refresh();
            LevelText.text = (PlayerPrefs.GetInt("ActiveTask") + 1).ToString();
            // Invoke(nameof(Refresh), 2);
        }
        public void Refresh()
        {
            bool TaskFinished = false;
            string Description = "TheTask";
            switch (_Task.Type)
            {
                case TaskType.CarryItems:
                    Description = "CARRY " + "<color=#00ff00ff>" + _Task._MaxAmount.ToString() + "</color>" + " ITEMS";
                    break;
                case TaskType.CarryCrate:
                    Description = "CARRY " + "<color=#00ff00ff>" + _Task._MaxAmount.ToString() + "</color>" + " CRATES";
                    break;
                case TaskType.Sell:
                    Description = "SELL " + "<color=#00ff00ff>" + _Task._MaxAmount.ToString() + "</color>" + " ITEMS";
                    break;
                case TaskType.HireWorker:
                    Description = "HIRE " + "<color=#00ff00ff>" + _Task._MaxAmount.ToString() + "</color>" + " WORKERS";
                    break;
                case TaskType.DriveThroughUnlock:
                    Description = "UNLOCK " + "<color=#00ff00ff>" + _Task._MaxAmount.ToString() + "</color>" + " DRIVE THROUGH";
                    //if (GameManager.instance.tasksMan.DriveThrough.IsUnlocked())
                    //{
                    //    TaskFinished = true;
                    //}
                    break;
                case TaskType.CollectStars:
                    Description = "COLLECT " + "<color=#00ff00ff>" + _Task._MaxAmount.ToString() + "</color>" + " STARS";
                    break;

            }
            float Progress = _Task.Amount / _Task._MaxAmount;
            SliderImg.fillAmount = Progress;
            TaskText.text = Description;
            ProgressText.text = _Task.Amount.ToString() + "/" + _Task._MaxAmount.ToString();



            if (_Task.Amount >= _Task._MaxAmount || TaskFinished)
            {

                ShowTaskDone();
                // Invoke(nameof(ConfirmCheck),2);
            }

        }
        void CheckDone()
        {
            if (!TaskDone)
            {
                Refresh();
            }
        }
        public void ShowFlash()
        {
            FlashTimestamp = Time.time + 1;
            GetComponent<Animator>().Rebind();
            GetComponent<Animator>().Play("Flash");
            flashObj.SetActive(true);
        }
        bool used;
        public void ShowTaskDone()
        {

            TaskDone = true;
            GiftObj.SetActive(true);
            GiftObj.GetComponent<Animator>().Play("Bounce");
            // GiftObj.GetComponentInChildren<Button>().interactable = true;

            if (!used)
            {
                used = true;
                _GameManager.instance.tasksMan.SpawnStar(null);

                Invoke(nameof(LatReward), 2);
            }
        }
        void LatReward()
        {
            GiftPressed();
            _GameManager.instance.tasksMan._TaskReward.GiveReward();

        }
        void ConfirmCheck()
        {
            if (GiftObj.GetComponentInChildren<Button>().interactable)
            {
                GiftObj.SetActive(true);
                GiftObj.GetComponent<Animator>().Play("Bounce");
            }
        }
        public void GiftPressed()
        {
            _GameManager.instance.tasksMan._TaskReward.ShowReward(_Task, true, null);
        }
        public void Close()
        {
            TaskDone = true;
            RewardClaimed = true;
            showtimestamp = 1;
        }
        private void Update()
        {
            if (FlashTimestamp < Time.time)
            {
                flashObj.SetActive(false);
            }
            if (TaskDone && RewardClaimed)
            {
                showtimestamp -= Time.deltaTime;
            }
            if (showtimestamp < 1)
            {

                if (showtimestamp < 0)
                {
                    Holder.transform.localPosition = Vector3.Lerp(Holder.transform.localPosition, new Vector3(-1000, 0, 0), Speed / 2 * Time.deltaTime);
                    if (showtimestamp < -3f)
                    {
                        //Destroy(gameObject);
                        gameObject.SetActive(false);
                    }

                }
            }
            else
            {
                Holder.transform.localPosition = Vector3.Lerp(Holder.transform.localPosition, new Vector3(0, 0, 0), Speed * Time.deltaTime);
            }

        }
    }
}
