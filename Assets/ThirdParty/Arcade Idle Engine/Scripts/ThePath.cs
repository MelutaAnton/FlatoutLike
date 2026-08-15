using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace AIE
{
    [ExecuteInEditMode]
    public class ThePath : MonoBehaviour
    {
        public Transform[] Points;

        void Update()
        {
            if (!Application.isPlaying)
            {
                if (Points.Length > 1)
                {
                    for (int i = 0; i < Points.Length - 1; i++)
                    {
                        Debug.DrawLine(Points[i].position, Points[i + 1].position, Color.green);
                    }
                    //Debug.DrawLine(Points[0].position, Points[Points.Length-1].position, Color.green);
                }
            }
        }
    }
}
