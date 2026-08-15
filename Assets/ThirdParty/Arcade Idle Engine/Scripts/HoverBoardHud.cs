using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
namespace AIE
{
    public class HoverBoardHud : MonoBehaviour
    {
        public AdBtn TheBtn;
        public void Reward()
        {
            gameObject.SetActive(false);
            _GameManager.instance.ThePlayer.ActivateHoverBoard();
        }
    }
}
