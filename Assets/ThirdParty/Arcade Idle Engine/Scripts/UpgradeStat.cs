using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
namespace AIE
{
    public class UpgradeStat : MonoBehaviour
    {
        public string PrefName = "Stat_0";
        public int HandCartCapacity;
        public int Capacity;
        public int Speed;
        public int _Time;
        bool init;

        public StatUpgrade HandCartStat;
        public StatUpgrade CapacityStat;
        public StatUpgrade SpeedStat;
        public StatUpgrade TimeStat;

        public GameObject Holder;
        private void OnEnable()
        {
            if (!init)
            {

            }
            RefreshAll();
        }


        public void UpgradeSpeed()
        {
            string tempprefname = PrefName + "_Speed";
            int Current = PlayerPrefs.GetInt(tempprefname);
            Current += 1;
            if (Current > 5)
            {
                Current = 5;
            }
            PlayerPrefs.SetInt(tempprefname, Current);
            RefreshAll();
        }
        public void UpgradeTime()
        {
            string tempprefname = PrefName + "_Time";
            int Current = PlayerPrefs.GetInt(tempprefname);
            Current += 1;
            if (Current > 5)
            {
                Current = 5;
            }
            PlayerPrefs.SetInt(tempprefname, Current);
            RefreshAll();
        }
        public void UpgradeCapacity()
        {
            string tempprefname = PrefName + "_Capacity";
            int Current = PlayerPrefs.GetInt(tempprefname);
            Current += 1;
            if (Current > 5)
            {
                Current = 5;
            }
            PlayerPrefs.SetInt(tempprefname, Current);
            RefreshAll();
        }

        void RefreshCapacity()
        {
            string tempprefname = PrefName + "_Capacity";
            int Current = PlayerPrefs.GetInt(tempprefname);
            float progress = (float)Current / 5;
            Capacity = Current;

            CapacityStat.SetLevel(Current);

        }
        void RefreshSpeed()
        {
            string tempprefname = PrefName + "_Speed";
            int Current = PlayerPrefs.GetInt(tempprefname);
            float progress = (float)Current / 5;
            Speed = Current;

            SpeedStat.SetLevel(Current);
        }
        void RefreshTime()
        {
            string tempprefname = PrefName + "_Time";
            int Current = PlayerPrefs.GetInt(tempprefname);
            float progress = (float)Current / 5;
            _Time = Current;

            TimeStat.SetLevel(Current);
        }


        [ContextMenu("Reset")]
        void ResetStats()
        {
            string level = 0.ToString();
            string tempprefname = level + PrefName + "_Speed";
            PlayerPrefs.SetInt(tempprefname, 0);
            tempprefname = level + PrefName + "_Capacity";
            PlayerPrefs.SetInt(tempprefname, 0);
            tempprefname = level + PrefName + "_Time";
            PlayerPrefs.SetInt(tempprefname, 0);
            tempprefname = level + PrefName + "_HandCartCapacity";
            PlayerPrefs.SetInt(tempprefname, 0);
            RefreshAll();
        }
        public void UpgradeHandCartCapacity()
        {
            string tempprefname = PrefName + "_HandCartCapacity";
            int Current = PlayerPrefs.GetInt(tempprefname);
            Current += 1;
            if (Current > 5)
            {
                Current = 5;
            }
            PlayerPrefs.SetInt(tempprefname, Current);
            RefreshAll();
        }

        void RefreshHandCartCapacity()
        {
            string tempprefname = PrefName + "_HandCartCapacity";
            int Current = PlayerPrefs.GetInt(tempprefname);
            float progress = (float)Current / 5;
            HandCartCapacity = Current;

            HandCartStat.SetLevel(Current);

        }
        public void RefreshAll()
        {
            RefreshSpeed();
            RefreshCapacity();
            RefreshHandCartCapacity();
            RefreshTime();

            Helper[] helpers = FindObjectsOfType<Helper>();
            for (int i = 0; i < helpers.Length; i++)
            {
                helpers[i].Refreshtats();
            }
            if (_GameManager.instance.ThePlayer)
            {
                _GameManager.instance.ThePlayer.Refreshtats();
            }
        }
    }
}
