using UnityEngine;

namespace ithappy.Battle_Stadium {
    [RequireComponent(typeof(Animator))]
    public class CharacterRandom : MonoBehaviour {
        [SerializeField]
        private float m_UpdateRate = 10f;

        private Animator m_Animator;
        private float m_Timer;

        private void OnEnable() {
            m_Animator = GetComponent<Animator>();
        }

        private void Update() {
            m_Timer += Time.deltaTime;
            if(m_Timer >= m_UpdateRate) {
                RandomizeIndex();
            }
        }

        private void RandomizeIndex() {
            m_Animator.SetInteger("AnimationIndex", Random.Range(0, 2));
        }
    }
}