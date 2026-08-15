using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace AIE
{
    public class UpgradeStand : MonoBehaviour
    {
        public void OpenUpgrades()
        {
            _GameManager.instance.upgradeManager.Openupgrades();
        }
        public void OpenStaffUpgrades()
        {
            _GameManager.instance.upgradeManager.OpenStaffupgrades();
        }

    }
}
