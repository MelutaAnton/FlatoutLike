using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
namespace AIE
{
    [System.Serializable]
    public class FinishAction : UnityEvent { }
    [System.Serializable]
    public class Timer : MonoBehaviour
    {
        public Image FillImg;
        public float MaxTime = 1;
        public float TheTime;
        public bool AutoReset = true;
        public bool AutoReset_Off_Platform = true;
        float resettimestamp;
        public FinishAction finishAction;
        private void Start()
        {
            Refresh();
        }
        void Refresh()
        {
            FillImg.fillAmount = TheTime / MaxTime;

        }
        public bool IsFilled()
        {
            if (TheTime >= MaxTime)
            {
                return true;
            }
            return false;
        }
        bool autoresetrunning;
        float autoresettimestamp;
        void _Autoreset()
        {
            if (Time.time > resettimestamp)
            {
                ResetTimer();

            }
            else
            {
                if (autoresettimestamp > Time.time)
                    return;
                autoresettimestamp = Time.time + 0.1f;
                Invoke(nameof(_Autoreset), 1f);
            }
        }
        public void AddTime()
        {

            resettimestamp = Time.time + 0.5f;
            if (AutoReset_Off_Platform)
            {
                _Autoreset();
            }
            if (TheTime >= MaxTime)
            {
                return;
            }
            TheTime += Time.deltaTime;
            if (TheTime >= MaxTime)
            {
                finishAction.Invoke();
                if (AutoReset)
                {
                    ResetTimer();
                }
            }
            // Debug.Log("Add");
            Refresh();
        }
        public void ResetTimer()
        {
            TheTime = 0;
            Refresh();
        }
    }
}
