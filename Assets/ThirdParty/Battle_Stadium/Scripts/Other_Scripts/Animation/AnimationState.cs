using System;
using UnityEngine;

namespace ithappy.Battle_Stadium {
    public class AnimationState : MonoBehaviour {
        [SerializeField]
        private AnimationNode m_Node;
        [SerializeField]
        private GameObject m_Button;

        private bool m_IsActivated;
        private Action m_OnDisabled;

        public bool IsActivated => m_IsActivated;
        public AnimationNode Node => m_Node;

        private void Awake() {
            m_Node.gameObject.SetActive(false);
            m_Button.SetActive(false);
        }

        public void DisableButton() {
            m_Button.SetActive(false);
        }

        public void EnableButton() {
            m_Button.SetActive(true);
        }

        public void Enable(Action onCallback = null) {
            m_Node.SetLock(false);
            m_Node.gameObject.SetActive(true);
            m_Button.SetActive(true);
            m_IsActivated = true;
            m_Node.Animate(onCallback);
        }

        public void Disable() {
            m_IsActivated = false;
            m_Button.SetActive(false);

            Deactivate();
        }

        public void Disable(Action onDisabled) {

            m_IsActivated = false;
            m_OnDisabled = onDisabled;

            if (m_Node.IsRunning || m_Node.ForwardBack) {
                m_Node.Animate(Deactivate, false);
                m_Node.SetLock(true);
            }
            else {
                Deactivate();
            }
        }

        private void Deactivate() {
            m_Node.SetLock(true);
            m_Node.gameObject.SetActive(false);
            m_OnDisabled?.Invoke();
        }
    }
}