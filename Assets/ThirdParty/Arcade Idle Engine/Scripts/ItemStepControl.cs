using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace AIE
{
    public class ItemStepControl : MonoBehaviour
    {
        public GameObject DefaultObj;
        public GameObject PackedObj;
        public GameObject TrashObj;
        public void Refresh()
        {
            if (GetComponent<TheItem>().ActiveStep == Step.Trash)
            {
                DefaultObj.SetActive(false);
                PackedObj.SetActive(false);
                TrashObj.SetActive(true);

            }
            else if (GetComponent<TheItem>().ActiveStep == Step.Packed)
            {
                DefaultObj.SetActive(false);
                PackedObj.SetActive(true);
                TrashObj.SetActive(false);

            }
            else
            {
                DefaultObj.SetActive(true);
                PackedObj.SetActive(false);
                TrashObj.SetActive(false);
            }
        }
    }
}
