using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
namespace AIE
{
    [System.Serializable]
    public class SpendAction : UnityEvent { }
    [System.Serializable]
    public class SpendRefresh : UnityEvent { }
    public class SpendMoney : MonoBehaviour
    {
        public Text CashAmountText;
        public Image FillImg;
        public float CashAmount;
        public float MaxAmount = 10;
        public bool AutoReset = true;
        float resettimestamp;
        public SpendAction spendAction;
        public SpendRefresh spendRefresh;
        public SlotManager slotManager;
        private void Start()
        {
            Refresh();
        }
        void Refresh()
        {
            //StopCoroutine(Smoothmove(0,true));
            //StartCoroutine(Smoothmove(CashAmount / MaxAmount,false));
            FillImg.fillAmount = CashAmount / MaxAmount;
            CashAmountText.text = Mathf.RoundToInt(MaxAmount - CashAmount).ToString();

        }
        IEnumerator Smoothmove(float newvalue, bool isstoping)
        {
            float thet = 1;
            bool isdone = false;
            while (!isdone)
            {
                thet -= Time.deltaTime;
                if (thet <= 0)
                {
                    isdone = true;
                }
                if (!isstoping)
                {
                    FillImg.fillAmount = Mathf.MoveTowards(FillImg.fillAmount, newvalue, 5 * Time.deltaTime);
                }
                yield return new WaitForSeconds(0.0001f);
            }
            if (!isstoping)
            {
                FillImg.fillAmount = newvalue;
            }
        }
        public void SetAmount(float Amount)
        {
            CashAmount = Amount;
            Refresh();
        }
        public bool IsFilled()
        {
            if (CashAmount >= MaxAmount)
            {
                return true;
            }
            return false;
        }
        void _Autoreset()
        {
            if (Time.time > resettimestamp)
            {
                ResetTimer();
            }
            else
            {
                Invoke(nameof(_Autoreset), 1);
            }
        }
        float spendtimestamp;
        public bool AddCash(bool usedelay, float MaxAmount = 10)
        {
            if (usedelay)
            {
                if (spendtimestamp > Time.time)
                {
                    return false;
                }
                spendtimestamp = Time.time + 0.1f;
            }
            if (slotManager &&
                !slotManager.CheckAvaiableSlots(gameObject))
                return false;
            resettimestamp = Time.time + 0.5f;
            //_Autoreset();
            if (CashAmount >= MaxAmount)
            {
                return false;
            }
            var count = Mathf.FloorToInt(Mathf.Log10(MaxAmount - CashAmount));
            //Debug.Log(count);
            if (count > 1)
            {
                count = 10 * count;
                //count = 10;

                if (count >= PlayerPrefs.GetInt("TotalCoins"))
                {
                    count = 1;
                }
            }
            else
            {
                count = Mathf.RoundToInt((MaxAmount / 10));

                if (PlayerPrefs.GetInt("TotalCoins") >= MaxAmount)
                {
                }
                if (count >= PlayerPrefs.GetInt("TotalCoins") || count == 0)
                {
                    count = PlayerPrefs.GetInt("TotalCoins");
                }
            }
            //int count = 5;
            //if (count >= PlayerPrefs.GetInt("TotalCoins"))
            //{
            //    count = PlayerPrefs.GetInt("TotalCoins");
            //}
            if (_GameManager.instance.cashman.SpendCash(transform, count))
            {
                spendRefresh.Invoke();
                CashAmount += count;
                if (CashAmount >= MaxAmount)
                {
                    if (AutoReset)
                    {
                        ResetTimer();
                    }
                    spendAction.Invoke();
                }
                // Debug.Log("Add");
                Refresh();

                return true;

            }
            return false;
        }
        float vibratetimestamp;
        public void ResetTimer()
        {
            CashAmount = 0;
            Refresh();
        }
    }
}
