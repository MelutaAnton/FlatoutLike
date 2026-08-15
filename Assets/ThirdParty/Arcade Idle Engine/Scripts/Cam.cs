using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace AIE
{
    public class Cam : MonoBehaviour
    {
        public Transform Target;
        public float Speed = 10;
        void Update()
        {
            transform.position = Vector3.Lerp(transform.position, Target.position, Speed * Time.deltaTime);

        }
    }
}
