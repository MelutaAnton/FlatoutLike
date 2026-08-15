using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AIE
{
    public class Emotion : MonoBehaviour
    {
        public enum TheEmotion
        {
            Happy, Angry, Drool, Seat
        }
        [System.Serializable]
        public class EmotionData
        {
            public TheEmotion theEmotion;
            public GameObject Obj;
        }
        public EmotionData[] TheData;
        public void ShowEmotion(TheEmotion emotion)
        {
            //Debug.Log(emotion);
            for (int i = 0; i < TheData.Length; i++)
            {
                if (TheData[i].theEmotion == emotion)
                {
                    if (!TheData[i].Obj.GetComponent<ParticleSystem>().isPlaying)
                    {
                        TheData[i].Obj.SetActive(false);
                    }
                    TheData[i].Obj.SetActive(true);
                }
                else
                {
                    TheData[i].Obj.SetActive(false);
                }
            }
        }
        public void StopEmotions()
        {
            for (int i = 0; i < TheData.Length; i++)
            {
                TheData[i].Obj.SetActive(false);
            }
        }
    }
}