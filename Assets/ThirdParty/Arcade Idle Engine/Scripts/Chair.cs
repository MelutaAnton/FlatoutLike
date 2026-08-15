using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace AIE
{
    public class Chair : MonoBehaviour
    {
        public GameObject Chairobj;
        public Transform UsingTrans;
        public Transform UsedTrans;
        public Transform NotUsedTrans;
        public Transform CupPos;
        public Stack TrashStack;
        private void Start()
        {
            NotUsed();
        }
        public void Using()
        {
            Chairobj.transform.position = UsingTrans.position;
            Chairobj.transform.rotation = UsingTrans.rotation;
        }
        public void Used()
        {
            Chairobj.transform.position = UsedTrans.position;
            Chairobj.transform.rotation = UsedTrans.rotation;
        }
        public void NotUsed()
        {
            Chairobj.transform.position = NotUsedTrans.position;
            Chairobj.transform.rotation = NotUsedTrans.rotation;
        }
    }
}
