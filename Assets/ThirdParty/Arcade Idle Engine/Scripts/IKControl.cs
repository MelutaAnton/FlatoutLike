using UnityEngine;
using System;
using System.Collections;
namespace AIE
{

    [RequireComponent(typeof(Animator))]

    public class IKControl : MonoBehaviour
    {

        protected Animator animator;
        public Transform RH;
        public Transform LH;
        public Transform RL;
        public Transform LL;

        void Start()
        {
            animator = GetComponent<Animator>();
        }


        public void ResetIk()
        {
            // Debug.Log("Reset");
            RH = null;
            LH = null;
            RL = null;
            LL = null;
        }

        public void OnAnimatorIK(int layerIndex)
        {

            if (RL != null)
            {
                animator.SetIKPositionWeight(AvatarIKGoal.RightFoot, 1);
                animator.SetIKRotationWeight(AvatarIKGoal.RightFoot, 1);
                animator.SetIKPosition(AvatarIKGoal.RightFoot, RL.position);
                animator.SetIKRotation(AvatarIKGoal.RightFoot, RL.rotation);
            }
            if (LL != null)
            {
                animator.SetIKPositionWeight(AvatarIKGoal.LeftFoot, 1);
                animator.SetIKRotationWeight(AvatarIKGoal.LeftFoot, 1);
                animator.SetIKPosition(AvatarIKGoal.LeftFoot, LL.position);
                animator.SetIKRotation(AvatarIKGoal.LeftFoot, LL.rotation);
            }

            if (RH != null)
            {
                animator.SetIKPositionWeight(AvatarIKGoal.RightHand, 1);
                animator.SetIKRotationWeight(AvatarIKGoal.RightHand, 1);
                animator.SetIKPosition(AvatarIKGoal.RightHand, RH.position);
                animator.SetIKRotation(AvatarIKGoal.RightHand, RH.rotation);
            }
            if (LH != null)
            {
                animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, 1);
                animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, 1);
                animator.SetIKPosition(AvatarIKGoal.LeftHand, LH.position);
                animator.SetIKRotation(AvatarIKGoal.LeftHand, LH.rotation);
            }

        }
    }
}

