using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace AIE
{
    public class ProgressBtn : MonoBehaviour
    {
        public GameObject ActiveObj;
        private void OnEnable()
        {
            if (ActiveObj.activeSelf)
            {
                GetComponent<Bounce>()._Bounce();
            }
        }
        public void SetOn(bool IsTrue)
        {
            if (IsTrue && !ActiveObj.activeSelf)
            {
                // ActiveObj.GetComponent<Bounce>()._Bounce();
                GetComponent<Bounce>()._Bounce();
            }
            ActiveObj.SetActive(IsTrue);
        }
    }
}
