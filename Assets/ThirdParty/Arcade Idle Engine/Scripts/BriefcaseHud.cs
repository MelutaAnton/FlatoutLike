using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
namespace AIE
{
    public class BriefcaseHud : MonoBehaviour
    {
        public AdBtn TheBtn;
        public void Reward()
        {
            gameObject.SetActive(false);
            _GameManager.instance.cashman.SpawnCash(TheBtn.transform.position, 5, 5);
        }
    }
}
