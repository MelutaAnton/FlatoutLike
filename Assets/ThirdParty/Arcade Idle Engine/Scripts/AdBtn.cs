using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
namespace AIE
{
    [System.Serializable]
    public class AdSuccess : UnityEvent { }
    [System.Serializable]
    public class AdBtn : MonoBehaviour
    {
        public bool UseCash;
        public bool IsUseIncrementalCash = true;
        public int IncreaseRate = 50;
        public string PrefName = "CashBtn";
        public AdSuccess adSuccess;
        public GameObject LoadingImg;
        public GameObject AdAvailableImg;

        public GameObject AdHolder;
        public GameObject CashHolder;
        public int CashAmount = 100;
        public Text CashText;

        //private void OnEnable()
        //{
        //    Refresh();
        //}
        public void Refresh()
        {
            if (UseCash)
            {
                if (IsUseIncrementalCash)
                {
                    RefreshCash();
                }

                CashHolder.SetActive(true);
                AdHolder.SetActive(false);
                if (PlayerPrefs.GetInt("TotalCoins") >= CashAmount)
                {
                    GetComponent<Button>().interactable = true;
                }
                else
                {
                    GetComponent<Button>().interactable = false;
                }
                CashText.text = CashAmount.ToString();


            }
            else
            {
                CashHolder.SetActive(false);
                AdHolder.SetActive(true);

                if (CheckRewardedAdAvailable())
                {
                    GetComponent<Button>().interactable = true;
                    LoadingImg.SetActive(false);
                    AdAvailableImg.SetActive(true);
                }
                else
                {
                    GetComponent<Button>().interactable = false;
                    LoadingImg.SetActive(true);
                    AdAvailableImg.SetActive(false);
                }
            }

        }
        bool CheckRewardedAdAvailable()
        {
            return true;
        }
        void IncreaseCash()
        {
            PlayerPrefs.SetInt(PrefName, PlayerPrefs.GetInt(PrefName) + IncreaseRate);
            Refresh();
        }
        void RefreshCash()
        {
            CashAmount = PlayerPrefs.GetInt(PrefName) + IncreaseRate;
        }
        private void Update()
        {
            Refresh();
        }
        public void _AdSuccess()
        {
            //Debug.Log("Success");
            if (UseCash)
            {
                _GameManager.instance.cashman.AdddTotalCash(-CashAmount);
                if (IsUseIncrementalCash)
                {
                    IncreaseCash();
                }
            }
            adSuccess.Invoke();
        }
        public void ShowAd()
        {
            //Debug.Log("ShowAd");
            if (UseCash)
            {
                if (PlayerPrefs.GetInt("TotalCoins") >= CashAmount)
                {
                    _AdSuccess();
                }
            }
            else
            {
                _AdSuccess();
            }


        }
    }
}
