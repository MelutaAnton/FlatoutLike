using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
namespace AIE
{
    public class TheNpcCar : MonoBehaviour
    {
        public int CurrentPos = 0;
        public Emotion TheEmotion;
        public GameObject[] CarModels;
        float waittimestamp;
        float SitTimestamp;
        public GameObject TheItem;
        public bool OnCounter;
        public Stack TheStack;
        public Text CounterText;
        public GameObject CounterHolder;

        private void Start()
        {
            int rand = Random.Range(0, CarModels.Length);
            for (int i = 0; i < CarModels.Length; i++)
            {
                if (rand == i)
                {
                    CarModels[i].SetActive(true);
                }
                else
                {
                    CarModels[i].SetActive(false);

                }
            }
        }
        bool finished;
        bool finishedsit;
        float DrinkTimstamp;
        public void AddWaitTime()
        {
            waittimestamp += Time.deltaTime;
            if (waittimestamp > 5 && !finished)
            {
                int theemotion = Random.Range(0, 3);
                //Debug.Log(theemotion);
                Emotion.TheEmotion emotion = Emotion.TheEmotion.Angry;
                if (theemotion == 1)
                {
                    emotion = Emotion.TheEmotion.Angry;
                }
                else if (theemotion == 2)
                {
                    emotion = Emotion.TheEmotion.Angry;
                }
                // TheEmotion.ShowEmotion(emotion);
                Exit(emotion);
                //GameManager.instance.npcTalkMan.ShowTalk(NpcTalkType.BadReview);
            }
        }
        public void StopWaitTime()
        {
            waittimestamp = 0;
        }
        public void Exit(Emotion.TheEmotion emotion)
        {
            finished = true;

            GetComponentInParent<CarQueLine>().CarFinished(gameObject, emotion);
        }
        public void Finished(GameObject _Item)
        {
            finished = true;
            AddItem(_Item);
            GetComponentInParent<CarQueLine>().CarFinished(gameObject, Emotion.TheEmotion.Drool);
            CounterHolder.SetActive(false);
        }
        public void AddItem(GameObject _Item)
        {
            //_Item.transform.parent = transform;
            // Cup.transform.localPosition = Vector3.zero;
            //_Item.transform.localEulerAngles = new Vector3(0, 0, 0);

            _Item.GetComponent<TheItem>().MoveToTarget(transform);
            TheItem = _Item;
            TheStack.AddStack(_Item, true);
            CounterText.text = (TheStack.MaxSize - TheStack.Stacks.Count).ToString();

        }
        public void RemoveItem()
        {
            for (int i = 0; i < TheStack.Stacks.Count; i++)
            {
                TheStack.Stacks[i].transform.SetParent(null);
                TheStack.Stacks[i].SetActive(false);
            }
            TheStack.Stacks.Clear();
            TheItem = null;
        }
        public void SetUp()
        {
            TheStack.MaxSize = Random.Range(1, 5);
            CounterText.text = TheStack.MaxSize.ToString();
            CounterHolder.SetActive(false);
            OnCounter = false;
        }
    }
}
