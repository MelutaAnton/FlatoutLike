using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace AIE
{
    public class Rotate : MonoBehaviour
    {
        public float Speed = 5;
        public Vector3 Dir;

        void Update()
        {
            transform.Rotate(Dir * Speed * Time.deltaTime);
        }
    }
}
