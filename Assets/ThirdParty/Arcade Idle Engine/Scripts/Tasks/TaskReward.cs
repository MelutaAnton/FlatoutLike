using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
namespace AIE
{
    public class TaskReward : MonoBehaviour
    {
        public GameObject CashRewardObj;
        public GameObject SkinRewardObj;

        [Header("CashRewardPart")]
        public Text CashText;
        public GameObject CashIcon;
        TheTask _RewardTask;
        public Button ClaimBtn;
        bool _IsTask;
        Challenge_Gift challenge_Gift;
        public void ShowReward(TheTask RewardTask, bool IsTask, Challenge_Gift challengeGift)
        {
            challenge_Gift = challengeGift;
            _IsTask = IsTask;
            //  ClaimBtn.interactable = GameManager.instance.Ads.RewardAdAvailable();

            CashRewardObj.SetActive(false);
            SkinRewardObj.SetActive(false);
            switch (RewardTask.rewardType)
            {
                case RewardType.Cash:
                    CashRewardObj.SetActive(true);
                    CashIcon.SetActive(true);
                    CashText.text = RewardTask.CashAmount.ToString();
                    break;


            }
            _RewardTask = RewardTask;
            // gameObject.SetActive(true);
        }
        public void ClaimReward()
        {
            // GameManager.instance.Ads.ShowRewarded(Ads_Manager.RewardAdType.GiveReward);
        }
        public void GiveReward()
        {
            switch (_RewardTask.rewardType)
            {
                case RewardType.Cash:
                    CashIcon.SetActive(false);
                    float Amount = _RewardTask.CashAmount;
                    //  GameManager.instance._cashReward.RewardCash("Gift", Mathf.RoundToInt(Amount / 10), 10);
                    // GameManager.instance.cashMan.SpawnCashUI(CashIcon.transform.position, Mathf.RoundToInt(Amount / 50),
                    //0.2f, 50);
                    break;

            }
            gameObject.SetActive(false);

            if (_IsTask)
            {
                //GameManager.instance.gameAnalytics_Man.Progression(PlayerPrefs.GetInt("ActiveTask").ToString() + "_Task Complete");
                _GameManager.instance.tasksMan.NextTask();
            }
            else
            {
                challenge_Gift.Collected();
            }
            // GameManager.instance._ReviewGame.AddReveiewCounter();

        }
        public void SkipReward()
        {
            gameObject.SetActive(false);
            if (_IsTask)
            {
                _GameManager.instance.tasksMan.NextTask();
            }



        }
    }
}
