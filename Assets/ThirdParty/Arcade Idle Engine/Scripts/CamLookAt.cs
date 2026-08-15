using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace AIE
{
    public class CamLookAt : MonoBehaviour
    {

        void LateUpdate()
        {
            transform.LookAt(Camera.main.transform);
        }
    }
}
