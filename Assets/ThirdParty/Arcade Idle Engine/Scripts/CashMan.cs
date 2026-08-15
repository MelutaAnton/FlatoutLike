using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
namespace AIE
{
    public class CashMan : MonoBehaviour
    {
        [Header("Menu")]
        public Text MenuCashText;


        public Transform CashTarget;

        [Header("SpawnCash")]
        public GameObject CashUiPref;
        public Transform spawnParent;
        public Transform spawnPos;

        public GameObject SpendCashUiPref;
        public GameObject CashSpendPref;
        public bool CollectCashUi;

        public void AdddTotalCash(int ChangeAmount)
        {
            PlayerPrefs.SetInt("TotalCoins", PlayerPrefs.GetInt("TotalCoins") + ChangeAmount);
        }




        public void Update()
        {
            if (Input.GetKeyDown(KeyCode.C))
            {
                SpawnCash(new Vector2(Screen.width / 2, Screen.height / 2), (10), 1);
            }
            //if (Input.GetKeyDown(KeyCode.Space))
            //{
            //    PlayerPrefs.SetInt("TotalCoins", PlayerPrefs.GetInt("TotalCoins") + 10000);
            //}
            MenuCashText.text = PlayerPrefs.GetInt("TotalCoins").ToString("n0");
            // GameOverCashText.text = PlayerPrefs.GetInt("TotalCoins").ToString("n0");

        }
        public void SpawnCash(Vector3 Pos, int Amount, int CashAdd, bool randomizepos = true)
        {
            Vector3 Offset = Vector3.zero;
            Transform Target = FindObjectOfType<TotalCash>().CashTarget;
            for (int i = 0; i < Amount; i++)
            {
                Vector2 offset2 = Random.insideUnitCircle * (50);
                Offset.x = offset2.x;
                Offset.y = offset2.y;
                GameObject Go = Instantiate(CashUiPref, spawnParent);
                if (randomizepos)
                {
                    Go.transform.position = Pos + Offset;

                }
                else
                {
                    Go.transform.position = Pos;

                }
                Go.GetComponent<CashUi>().SetUp(Target, i, false, CashAdd);
            }

        }
        float spendtimestamp;
        public bool SpendCash(Transform Target, int CashAdd)
        {
            if (spendtimestamp > Time.time)
            {
                return false;
            }
            spendtimestamp = Time.time + 0.1f;
            if (CashAdd > 0 && PlayerPrefs.GetInt("TotalCoins") >= CashAdd)
            {
                //spendUi(Target, CashAdd);
                SpendWorld(Target, CashAdd);
                PlayerPrefs.SetInt("TotalCoins", PlayerPrefs.GetInt("TotalCoins") - CashAdd);
                return true;
            }
            else
            {
                return false;
            }

        }
        void SpendWorld(Transform Target, int CashAdd)
        {
            Transform thecam = _GameManager.instance.cam.transform;
            Vector3 offset = new Vector3(-2, 2, 0);

            // Debug.Log(Camera.main.transform.name);
            //GameObject Go = Instantiate(CashSpendPref, transform);
            int count = CashAdd;
            while (count > 0)
            {
                count -= 1;
                GameObject Go = _GameManager.instance.poolman.GetCashSpendObj();
                if (Go)
                {
                    Go.transform.position = _GameManager.instance.ThePlayer.transform.position + new Vector3(Random.Range(-1, 1), 3, Random.Range(-1, 1));
                    // Go.transform.position = CashTarget.position;
                    Go.GetComponent<CashSpend>().SetUp(Target, 0, true, 1);
                    Go.SetActive(true);

                }
            }

        }
        void spendUi(Transform Target, int CashAdd)
        {
            Transform thecam = _GameManager.instance.cam.transform;
            Vector3 offset = new Vector3(-2, 2, 0);

            // Debug.Log(Camera.main.transform.name);
            GameObject Go = Instantiate(CashUiPref, spawnParent);
            Go.transform.position = Camera.main.WorldToScreenPoint(thecam.position +
              thecam.transform.TransformDirection(offset));
            // Go.transform.position = CashTarget.position;
            Go.GetComponent<CashUi>().SetUp(Target, 0, true, CashAdd);
        }
    
    }
}
