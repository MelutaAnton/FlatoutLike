using UnityEngine;

namespace ithappy.Battle_Stadium {
    public class ObjectSwitcher : MonoBehaviour {
        [SerializeField]
        private GameObject[] m_Objects = new GameObject[0];

        private bool m_IsActive;

        private void Awake() {
            m_IsActive = false;
            UpdateObjects();
        }

        public void Switch() {
            m_IsActive = !m_IsActive;
            UpdateObjects();
        }

        public void Enable(bool enabled) {
            m_IsActive = enabled;
            UpdateObjects();
        }

        private void UpdateObjects() {
            foreach (var obj in m_Objects) {
                if (obj != null) {
                    obj.SetActive(m_IsActive);
                }
            }
        }
    }
}