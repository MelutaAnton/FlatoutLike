using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace AIE
{
    public enum TaskType
    {
        CarryItems,
        Sell,
        CarryCrate,
        HireWorker,
        DriveThroughUnlock,
        CollectStars
    }
    public enum RewardType
    {
        None,
        Cash,
        Skin
    }
    [System.Serializable]
    public class TheTask
    {
        public TaskType Type;
        public float Amount;
        public float _MaxAmount = 3;
        public RewardType rewardType;
        [DrawIf("rewardType", RewardType.Skin)]
        public string _Skin = "Skin_0";
        [DrawIf("rewardType", RewardType.Cash)]
        public int CashAmount = 500;


    }
    public class TasksMan : MonoBehaviour
    {
        public GameObject _ChallengeUIPref;
        public Transform SpawnPos;
        public Transform StarSpawnPos;
        GameObject[] Pool;
        public TheTask[] Tasks;
        TheTask ActiveTask;
        TaskUi ActiveTaskUi;
        public TaskReward _TaskReward;
        public Challenge_Gift[] ChallengeGifts;
        public TaskType DebugTaskType;
        public List<LevelLock> LevelLocks = new List<LevelLock>();

        public Unlock DriveThrough;
        public GameObject StarPref;
        private void Start()
        {
            Pool = new GameObject[10];
            for (int i = 0; i < Pool.Length; i++)
            {
                GameObject Go = Instantiate(_ChallengeUIPref, SpawnPos);
                Go.SetActive(false);
                Pool[i] = Go;
            }
            RefreshTasks();

            if (ActiveTask == null)
            {
                ShowChallenge();
            }

            //Refresh();
        }
        public void ForceFinishTask()
        {
            TaskProgressAdd(ActiveTask.Type, 100);
        }
        public void RefreshLevelLock()
        {
            for (int i = 0; i < LevelLocks.Count; i++)
            {
                LevelLocks[i].Refresh();
            }
        }
        public void RefreshChallengeGifts(TaskType Type, int Amount)
        {
            for (int i = 0; i < ChallengeGifts.Length; i++)
            {
                if (Type == ChallengeGifts[i]._Task.Type)
                {
                    ChallengeGifts[i].AddChallenge(Amount);
                }
            }
        }
        public void Activate()
        {
            PlayerPrefs.SetInt("TasksActivated", 1);
            Refresh();
        }
        void Refresh()
        {
            if (PlayerPrefs.GetInt("TasksActivated") == 0)
            {

                SpawnPos.gameObject.SetActive(false);
                if (PlayerPrefs.GetInt("FirstTestDriveStart") == 1)
                {
                    Activate();
                }
            }
            else
            {
                SpawnPos.gameObject.SetActive(true);
            }
        }
        public void ShowChallenge()
        {
            ActiveTask = Tasks[PlayerPrefs.GetInt("ActiveTask")];
            GameObject Go = GetUi();
            if (Go)
            {
                ActiveTaskUi = Go.GetComponent<TaskUi>();
                Go.GetComponent<TaskUi>().SetUp(ActiveTask);
                Go.transform.SetAsLastSibling();
                Go.SetActive(true);
            }
            for (int i = 0; i < Pool.Length; i++)
            {
                if (Pool[i] != Go && Pool[i].activeSelf)
                {
                    Pool[i].GetComponent<TaskUi>().Close();
                }
            }
        }
        GameObject GetUi()
        {
            for (int i = 0; i < Pool.Length; i++)
            {
                if (!Pool[i].activeSelf)
                {
                    return Pool[i];
                }
            }
            return null;
        }
        public void TaskProgressAdd(TaskType Type, int Amount)
        {


            RefreshChallengeGifts(Type, Amount);

            if (!SpawnPos.gameObject.activeSelf)
                return;
            if (ActiveTask == null)
                return;
            if (Type != ActiveTask.Type)
                return;

            string PrefName = "Task" + PlayerPrefs.GetInt("ActiveTask").ToString();

            PlayerPrefs.SetFloat(PrefName + "Amount", PlayerPrefs.GetFloat(PrefName + "Amount") + Amount);


            RefreshTasks();

            for (int i = 0; i < Pool.Length; i++)
            {
                if (Pool[i].activeSelf)
                {
                    Pool[i].GetComponent<TaskUi>().Refresh();
                }
            }
            ActiveTaskUi.ShowFlash();
        }
        public void NextTask()
        {


            ActiveTaskUi.Close();
            PlayerPrefs.SetInt("ActiveTask", PlayerPrefs.GetInt("ActiveTask") + 1);
            if (PlayerPrefs.GetInt("ActiveTask") < Tasks.Length)
            {
                ShowChallenge();
            }
            else
            {
            }
            RefreshLevelLock();

            //if (PlayerPrefs.GetInt("ActiveTask") == 5)
            //{
            //}
            //GameManager.instance._GameReview.AddReveiewCounter();

        }
        void RefreshTasks()
        {
            for (int i = 0; i < Tasks.Length; i++)
            {
                string PrefName = "Task" + i.ToString();
                Tasks[i].Amount = PlayerPrefs.GetFloat(PrefName + "Amount");

            }
        }
        public void SpawnStar(Transform _SpawnPos)
        {
            Debug.Log("StarSpawned");
            GameObject Go = Instantiate(StarPref, StarSpawnPos);
            // Go.transform.position = Camera.main.WorldToScreenPoint(_SpawnPos.position);
            Go.GetComponent<Star>().SetUp(ActiveTaskUi.LevelText.transform, 1);
        }
        private void Update()
        {
            if (Input.GetKey(KeyCode.V))
            {
                TaskProgressAdd(ActiveTask.Type, 1);
                // // TaskProgressAdd(TaskType.TaxiJob, 1);
                //TaskProgressAdd(DebugTaskType, 1);
                //ShowChallenge();

            }
        }
    }
}
