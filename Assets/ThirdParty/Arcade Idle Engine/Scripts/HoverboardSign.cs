using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace AIE
{
    public class HoverboardSign : MonoBehaviour
    {
        Transform ThePlayer;
        public Rotate TheModel;
        public void OpenHoverBardHud()
        {
            _GameManager.instance.OpenHoverBoard();
        }
        private void Update()
        {
            if (!ThePlayer)
            {
                ThePlayer = _GameManager.instance.ThePlayer.transform;
            }
            else
            {
                float thedist = Vector3.Distance(transform.position, ThePlayer.position);
                if (thedist < 3)
                {
                    Vector3 relativePos = ThePlayer.position - transform.position;

                    // the second argument, upwards, defaults to Vector3.up
                    Quaternion rotation = Quaternion.LookRotation(relativePos, Vector3.up);
                    TheModel.enabled = false;
                    TheModel.transform.rotation = Quaternion.Slerp(TheModel.transform.rotation, rotation, 5 * Time.deltaTime);
                }
                else
                {
                    if (!TheModel.enabled)
                    {
                        TheModel.transform.localEulerAngles = new Vector3(0, TheModel.transform.localEulerAngles.y, 0);
                    }
                    TheModel.enabled = true;
                }

            }
        }
    }
}
