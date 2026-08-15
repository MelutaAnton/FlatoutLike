using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace ithappy.Battle_Stadium {
    public abstract class AnimationNode : MonoBehaviour {
        [SerializeField]
        private AnimationFPS m_Fps = AnimationFPS.FPS_60;
        [SerializeField]
        private float m_Duration = 10f;

        [SerializeField]
        private UnityEvent m_OnForward;
        [SerializeField]
        private UnityEvent m_OnBackward;

        private Action m_OnCallback;
        private bool m_IsRunning;
        private bool m_ForwardBack;

        private float m_Time;
        private bool m_IsLocked;

        public void SetLock(bool state) {
            m_IsLocked = state;
        }

        public bool ForwardBack => m_ForwardBack;

        public bool IsRunning => m_IsRunning;
        public AnimationFPS Fps => m_Fps;

        protected virtual void OnDisable() {
            StopAllCoroutines();
        }

        public void Animate() {
            Animate(null, !m_ForwardBack);
        }

        public void Animate(bool forwardBack = true) {
            Animate(null, forwardBack);
        }

        public void Animate(Action onCallback, bool forwardBack = true) {
            if (m_IsLocked) {
                return;
            }

            if (!gameObject.activeSelf) { 
                return; 
            }

            StopAllCoroutines();
            if(m_IsRunning) {
                m_OnCallback?.Invoke();
                if(m_ForwardBack) {
                    m_OnForward?.Invoke();
                }
                else {
                    m_OnBackward?.Invoke();
                }
            }

            m_OnCallback = onCallback;
            m_ForwardBack = forwardBack;
            if(m_IsRunning) {
                if (m_ForwardBack) {
                    StartCoroutine(RunForward(m_Time));
                }
                else {
                    StartCoroutine(RunBackward(m_Duration - m_Time));
                }
            }
            else {
                if (m_ForwardBack) {
                    StartCoroutine(RunForward());
                }
                else {
                    StartCoroutine(RunBackward());
                }
            }

            m_IsRunning = true;

        }
        protected abstract void AnimateFrame(float time);

        private void EndAnimation() {
            m_IsRunning = false;
            m_OnCallback?.Invoke();
            m_OnCallback = null;
        }

        private IEnumerator RunForward(float offset = 0f) {
            m_Time = 0f + offset;
            var waitTime = 1f / (int)m_Fps;
            var wait = new WaitForSeconds(waitTime);

            while (m_Time <= m_Duration) {
                AnimateFrame(m_Time / m_Duration);
                m_Time += waitTime;

                yield return wait;
            }

            m_Time = m_Duration;
            EndAnimation();
            m_OnForward?.Invoke();
        }

        private IEnumerator RunBackward(float offset = 0f) {
            m_Time = m_Duration - offset;
            var waitTime = 1f / (int)m_Fps;
            var wait = new WaitForSeconds(waitTime);

            while (m_Time >= 0f) {
                AnimateFrame(m_Time / m_Duration);
                m_Time -= waitTime;

                yield return wait;
            }

            m_Time = 0f;
            EndAnimation();
            m_OnBackward?.Invoke();
        }


        public enum AnimationFPS : int {
            FPS_27 = 27,
            FPS_30 = 30,
            FPS_60 = 60,
            FPS_80 = 80,
            FPS_100 = 100
        }
    }
}