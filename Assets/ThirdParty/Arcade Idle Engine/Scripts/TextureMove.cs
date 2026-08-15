using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace AIE
{
    public class TextureMove : MonoBehaviour
    {
        public float Speed = 5;
        public Vector2 DIr;
        // Start is called before the first frame update
        void Start()
        {

        }

        // Update is called once per frame
        void Update()
        {
            GetComponent<MeshRenderer>().material.SetTextureOffset("_MainTex", DIr * -Time.timeSinceLevelLoad * Speed);
        }
    }
}
