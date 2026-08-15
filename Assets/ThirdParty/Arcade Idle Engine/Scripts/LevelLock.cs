using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif
using UnityEngine.SceneManagement;
namespace AIE
{
    [System.Serializable] public class LevelBeforeUnlockAction : UnityEvent { }
    [System.Serializable] public class LevelUnlockAction : UnityEvent { }
    public class LevelLock : MonoBehaviour
    {
        public int TheLevel;
        public Text[] LevelText;
        public LevelBeforeUnlockAction BeforeUnlockAction;
        public LevelUnlockAction UnlockAction;
        bool LevelLocked;
        public GameObject Particles;
        public GameObject Holder;
        public bool IsAlwaysShow = true;
        private void Start()
        {

            _GameManager.instance.tasksMan.LevelLocks.Add(this);
            if (PlayerPrefs.GetInt("ActiveTask") >= TheLevel)
            {
                LevelLocked = false;
            }
            else
            {
                LevelLocked = true;
            }
            Refresh();
        }
        [ContextMenu("ShowAll")]
        void ShowAll()
        {
            LevelLock[] levels = FindObjectsOfType<LevelLock>();
            for (int i = 0; i < levels.Length; i++)
            {
                // levels[i].IsAlwaysShow = false;
                for (int r = 0; r < levels[i].LevelText.Length; r++)
                {
                    levels[i].LevelText[r].text = (levels[i].TheLevel).ToString();

                }
                // levels[i].Refresh(true);
            }
#if UNITY_EDITOR
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
#endif
        }

        public void Refresh(bool forceshow = false)
        {
            if (!gameObject.activeSelf)
                return;
            int activelevel = PlayerPrefs.GetInt("ActiveTask");
            if (PlayerPrefs.GetInt("ActiveTask") >= TheLevel)
            {
                Holder.SetActive(true);
                //Unlock
                if (LevelLocked)
                {
                    //if (GetComponentInParent<Bounce>())
                    //{
                    //    GetComponentInParent<Bounce>().DelayBounce(0.5f);
                    //}
                    //Invoke(nameof(Deactivate), 2f);
                    BeforeUnlockAction.Invoke();
                    //GameManager.instance.cam.ShowcasePoi(transform,Vector3.zero);

                    Invoke(nameof(Deactivate), 2f);
                    Particles.SetActive(true);


                }
                else
                {
                    gameObject.SetActive(false);
                }
            }
            else
            {
                if (activelevel == TheLevel - 1 || IsAlwaysShow || forceshow)
                {
                    Holder.SetActive(true);
                }
                else
                {
                    Holder.SetActive(false);
                }
                //Lock
            }
            for (int i = 0; i < LevelText.Length; i++)
            {
                LevelText[i].text = (TheLevel).ToString();

            }
        }
        void Deactivate()
        {
            if (LevelLocked)
            {
                if (GetComponentInParent<Bounce>())
                {
                    GetComponentInParent<Bounce>().DelayBounce(0.1f);
                }
                UnlockAction.Invoke();

            }
            gameObject.SetActive(false);

        }
        public bool IsLocked()
        {
            if (PlayerPrefs.GetInt("ActiveTask") >= TheLevel)
            {
                return false;
            }
            return true;
        }

    }
}
