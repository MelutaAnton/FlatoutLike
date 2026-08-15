using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
namespace AIE
{
    public class HoverboardActiveHud : MonoBehaviour
    {
        public Text TheTime;
        public Image FillImg;
        void Update()
        {

            FillImg.fillAmount = 1 - (_GameManager.instance.ThePlayer.Hoverboardtime / 120);
            //TheTime.text = GameManager.instance.ThePlayer.Hoverboardtime.ToString();
            if (!_GameManager.instance.ThePlayer.UseHoverBoard)
            {
                gameObject.SetActive(false);
            }
        }

    }
}
