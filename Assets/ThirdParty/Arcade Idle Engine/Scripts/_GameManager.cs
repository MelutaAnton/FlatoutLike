using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace AIE
{
    public class _GameManager : MonoBehaviour
    {
        public static _GameManager instance;
        public Cam cam;
        public Player ThePlayer;
        public CashMan cashman;
        public PoolManager poolman;
        public NotificationMan notificationMan;
        public HoverboardActiveHud hoverboardActiveHud;
        public GameObject HoverboardHud;
        public GameObject BriefCaseHud;
        public UpgradeManager upgradeManager;
        public TasksMan tasksMan;
        public EconomyMan economyMan;
        public Joystick _Joystick;
        private void Awake()
        {
            if (instance)
            {
                Destroy(instance);
            }
            instance = this;
        }
        private void Update()
        {
            InputMan();
        }
        void InputMan()
        {
            if (_Joystick.Direction.sqrMagnitude != 0)
            {
                ThePlayer.SetInput(_Joystick.Direction.x, _Joystick.Direction.y);
            }
            else
            {
                ThePlayer.SetInput(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));
            }
        }
        public void OpenActiveHoverBoardHud()
        {
            hoverboardActiveHud.gameObject.SetActive(true);
        }
        public void OpenHoverBoard()
        {
            HoverboardHud.SetActive(true);
        }
        public void OpenBriefCase()
        {
            BriefCaseHud.SetActive(true);
        }
    }
}
