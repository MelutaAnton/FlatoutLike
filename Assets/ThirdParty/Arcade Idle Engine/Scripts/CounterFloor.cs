using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace AIE
{
    public class CounterFloor : MonoBehaviour
    {
        public MeshRenderer TheRend;
        public Color InactiveColor = Color.white;
        public Color ActiveCalor = Color.green;
        float activetimestamp;
        void Update()
        {
            if (activetimestamp < Time.time)
            {
                TheRend.material.color = InactiveColor;
                this.enabled = false;
            }
        }
        public void Activate()
        {
            activetimestamp = Time.time + 0.5f;
            TheRend.material.color = ActiveCalor;
            this.enabled = true;
        }
    }
}
