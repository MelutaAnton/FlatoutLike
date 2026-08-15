using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace AIE
{
    public class UpgradeManager : MonoBehaviour
    {
        [Header("Player")]
        public int PlayerCarryCapacity = 3;
        public int PlayerHandCartCapacity = 3;

        [Header("Worker")]
        public int WorkerCarryCapacity = 3;
        public int WorkerSpeed = 2;
        [Space(10)]
        public UpgradeHud UpgradeHud;
        private void OnEnable()
        {
            checkrefresh();
        }
        void checkrefresh()
        {
            if (_GameManager.instance != null)
            {
                UpgradeHud.Refresh();
            }
            else
            {
                Invoke(nameof(checkrefresh), 0.1f);
            }
        }
        public void Openupgrades()
        {
            UpgradeHud.OpenUpgrades();
            UpgradeHud.gameObject.SetActive(true);
        }
        public void OpenStaffupgrades()
        {
            UpgradeHud.OpenStaffUpgrades();
            UpgradeHud.gameObject.SetActive(true);
        }
        public void CloseUpgrades()
        {
            UpgradeHud.gameObject.SetActive(false);
        }
    }
}
