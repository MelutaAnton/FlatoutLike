using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace AIE
{
    public class UpgradeHud : MonoBehaviour
    {
        public UpgradeStat Playerupgrade;
        public UpgradeStat Workerupgrade;
        public void Refresh()
        {
            Playerupgrade.RefreshAll();
            Workerupgrade.RefreshAll();
        }
        public void OpenUpgrades()
        {
            Playerupgrade.Holder.SetActive(true);
            Workerupgrade.Holder.SetActive(false);
        }
        public void OpenStaffUpgrades()
        {
            Playerupgrade.Holder.SetActive(false);
            Workerupgrade.Holder.SetActive(true);
        }
    }
}
