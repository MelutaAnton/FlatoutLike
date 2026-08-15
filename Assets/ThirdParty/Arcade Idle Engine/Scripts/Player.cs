using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace AIE
{
    public enum TheType { Player, Npc }
    public class Player : MonoBehaviour
    {
        public CharacterController controller;
        public Animator Anim;
        public float MoveSpeed = 5;
        public TheType type;
        public float X;
        public float V;
        float offloadTimestamp;
        public float OffloadRate = 0.1f;
        HandCartArea _CartArea;
        public Stack HandStack;
        float CollectTimestamp;
        float ReleaseTimestamp;
        public GameObject FullObj;
        public GameObject ArrowDir;
        public HandCart TheHandCart;
        float ActionAreaTimestamp;
        public AudioSource CashCollectSound;
        public AudioSource CashSpendSound;
        float pausemovementtimestamp;
        public bool UseHoverBoard;
        public GameObject TheHoverBoard;
        public Transform ModelHoverHeight;
        public Transform ModelHolder;
        public float Hoverboardtime;
        public float TurnDir;
        public bool StopToCollect = false;
        public float InteractionTimestamp;

        void Update()
        {
            AnimMan();
            MoveMan();
            HoverBoardMan();
            if (type == TheType.Player)
            {
                ShopMan();
            }
            if (transform.position.y < -5)
            {
                transform.position = Vector3.zero;
            }
        }
        public bool IsInteraction()
        {
            if (InteractionTimestamp > Time.time)
            {
                return true;
            }
            return false;
        }
        public void AddInteractionTime()
        {
            InteractionTimestamp = Time.time + 1;
        }
        public void ActivateHoverBoard()
        {
            Hoverboardtime = 120;
            UseHoverBoard = true;
            _GameManager.instance.OpenActiveHoverBoardHud();
        }
        void HoverBoardMan()
        {
            if (UseHoverBoard)
            {
                Hoverboardtime -= Time.deltaTime;
                if (Hoverboardtime < 0)
                {
                    UseHoverBoard = false;
                }
                TheHoverBoard.SetActive(true);
                ModelHolder.localPosition = ModelHoverHeight.localPosition;
                float animv = Vector2.SqrMagnitude(new Vector2(X, V));
                ModelHolder.localEulerAngles = new Vector3(animv * 5, 0, 0);
                TheHoverBoard.GetComponent<HoverBoard>().SetSpeed(animv);
            }
            else
            {
                TheHoverBoard.SetActive(false);
                ModelHolder.localPosition = Vector3.zero;
                ModelHolder.localEulerAngles = new Vector3(0, 0, TurnDir * 10);
            }
        }
        public void SetInput(float _X, float _V)
        {
            X = _X;
            V = _V;

        }
        void AnimMan()
        {



            float animv = Vector2.SqrMagnitude(new Vector2(X, V));
            if (animv > 0)
            {
                animv = 1;
                if (type == TheType.Player)
                {
                    Vector3 Dir = new Vector3(X, 0, V);
                    if (type == TheType.Player)
                    {
                        Dir = _GameManager.instance.cam.transform.TransformDirection(Dir);
                    }
                    if (Dir != Vector3.zero)
                    {
                        transform.forward = Dir;
                    }
                }
            }
            if (UseHoverBoard)
            {
                Anim.SetFloat("V", 0);
                Anim.SetFloat("RunMultiplier", 1);

            }
            else
            {
                Anim.SetFloat("V", animv);
                float runmultiplier = 1 + ((float)_GameManager.instance.upgradeManager.UpgradeHud.Playerupgrade.Speed) / 5;
                if (animv > 0)
                {
                    Anim.SetFloat("RunMultiplier", runmultiplier);

                }
                else
                {
                    Anim.SetFloat("RunMultiplier", 1);

                }

            }
        }
        public void PauseMovement(float TheT)
        {
            pausemovementtimestamp = Time.time + TheT;
            controller.enabled = false;

        }
        void MoveMan()
        {
            if (pausemovementtimestamp > Time.time)
                return;
            controller.enabled = true;
            Vector3 Dir = new Vector3(X, 0, V);
            // transform.forward = Dir;
            if (type != TheType.Player)
            {
                Dir = transform.TransformDirection(Dir);
            }
            else
            {
                Dir = _GameManager.instance.cam.transform.TransformDirection(Dir);
            }
            float Multiplier = 1;
            if (UseHoverBoard)
            {
                Multiplier = 1.5f;
            }
            controller.SimpleMove(Dir * (MoveSpeed + (float)_GameManager.instance.upgradeManager.UpgradeHud.Playerupgrade.Speed / 2.5f) * Multiplier);
        }
        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (type == TheType.Player)
            {
                if (hit.transform.GetComponentInParent<Timer>())
                {
                    hit.transform.GetComponentInParent<Timer>().AddTime();
                }
            }
            if (hit.transform.GetComponentInParent<HandCartArea>())
            {
                _CartArea = hit.transform.GetComponentInParent<HandCartArea>();
            }
            else
            {
                _CartArea = null;
            }

            //if (hit.transform.GetComponentInParent<Door>())
            //{
            //    hit.transform.GetComponentInParent<Door>().OpenDoor(transform.position);
            //}


        }
        float spendtime;
        float spendtimestamp;

        float vibratetimestamp;
        void ShopMan()
        {
            if (_CartArea)
            {
                if (!TheHandCart.gameObject.activeSelf)
                {
                    TheHandCart.gameObject.SetActive(true);
                }
                if (TheHandCart.transform.parent != transform)
                {
                    TheHandCart.transform.SetParent(transform);
                    TheHandCart.transform.localPosition = Vector3.zero;
                    TheHandCart.transform.localEulerAngles = Vector3.zero;
                }
                if (X == 0 && V == 0 || !StopToCollect)
                {
                    CollectCheck(TheHandCart.TheStack);
                    GroundCheck();
                }
                //  TheHandCart.gameObject.SetActive(true);

                // Anim.GetComponent<IKControl>().enabled = true;
                HandStack.gameObject.SetActive(false);
                HandStack.StacksParent.SetActive(false);
                Anim.GetComponent<IKControl>().RH = TheHandCart.GetComponent<IkTargets>().RH;
                Anim.GetComponent<IKControl>().LH = TheHandCart.GetComponent<IkTargets>().LH;
            }
            else
            {
                //TheHandCart.gameObject.SetActive(true);
                if (TheHandCart.transform.parent == transform)
                {
                    TheHandCart.transform.SetParent(null);
                    // TheHandCart.transform.position = TheHandCart.transform.position- TheHandCart.transform.forward * 2;

                }
                // TheHandCart.gameObject.SetActive(false);
                //TheTank.gameObject.SetActive(false);
                //TheHarvester.gameObject.SetActive(false);
                //Anim.GetComponent<IKControl>().enabled = false;

                CollectCheck(HandStack);
                GroundCheck();
                HandStack.gameObject.SetActive(true);
                HandStack.StacksParent.SetActive(true);
                if (HandStack.Stacks.Count > 0)
                {
                    Anim.GetComponent<IKControl>().RH = HandStack.GetComponent<IkTargets>().RH;
                    Anim.GetComponent<IKControl>().LH = HandStack.GetComponent<IkTargets>().LH;
                }
                else
                {
                    Anim.GetComponent<IKControl>().RH = null;
                    Anim.GetComponent<IKControl>().LH = null;
                }
            }

        }


        void GroundCheck()
        {
            RaycastHit hit;
            if (Physics.Raycast(transform.position, Vector3.down, out hit, 1))
            {
                if (hit.transform.GetComponentInParent<SpendMoney>() && type == TheType.Player)
                {
                    if (spendtimestamp > Time.time)
                    {
                        spendtime += Time.deltaTime;
                        if (spendtime > 0.5f)
                        {
                            if (hit.transform.GetComponentInParent<SpendMoney>().AddCash(false, hit.transform.GetComponentInParent<SpendMoney>().MaxAmount))
                            {
                                if (!CashSpendSound.isPlaying)
                                {
                                    CashSpendSound.Play();
                                }
                            }


                        }
                    }
                    else
                    {
                        spendtime = 0;
                    }
                    spendtimestamp = Time.time + 0.1f;

                }
            }
        }
        void CollectCheck(Stack TheStack)
        {

            RaycastHit Hit;
            if (Physics.Raycast(transform.position, Vector3.down, out Hit, 1))
            {

                if (Hit.transform.GetComponentInParent<SlotManager>())
                {
                    if (CollectTimestamp < Time.time)
                    {
                        SlotManager slotman = Hit.transform.GetComponentInParent<SlotManager>();
                        GameObject Target = null;
                        for (int i = slotman.Slots.Length - 1; i >= 0; i--)
                        {
                            if (slotman.Slots[i].TheOwner &&
                                slotman.Slots[i].TheOwner.activeSelf)
                            {
                                if (slotman.Slots[i].TheOwner.GetComponent<TheItem>() && !TheStack.IsFull()
                                    )
                                {
                                    if (!slotman.Slots[i].TheOwner.GetComponent<TheItem>().IsMoving)
                                    {
                                        Target = slotman.Slots[i].TheOwner;
                                        //slotman.Slots[i].TheOwner = null;
                                        break;
                                    }
                                }
                                else if (slotman.Slots[i].TheOwner.GetComponent<TheCash>()
                                   )
                                {
                                    if (!slotman.Slots[i].TheOwner.GetComponent<TheCash>().Moving)
                                    {
                                        Target = slotman.Slots[i].TheOwner;
                                        if (!CashCollectSound.isPlaying)
                                        {
                                            CashCollectSound.Play();
                                        }
                                        // slotman.Slots[i].TheOwner = null;
                                        break;
                                    }
                                }
                                else if (slotman.Slots[i].TheOwner.GetComponentInParent<TheNpc>())
                                {

                                }
                                else
                                {
                                    if (!TheStack.IsFull())
                                    {
                                        Target = slotman.Slots[i].TheOwner;
                                        // slotman.Slots[i].TheOwner = null;
                                        break;
                                    }
                                }

                            }
                            else
                            {
                                if (!slotman.Slots[i].TheOwner && slotman.Slots[i].TheStack &&
                                    slotman.Slots[i].TheStack.Stacks.Count > 0)
                                {
                                    TheStack.SiphonStack(slotman.Slots[i].TheStack);
                                    break;
                                }
                            }
                        }
                        if (Target)
                        {
                            if (type == TheType.Player)
                            {
                                AddInteractionTime();
                            }
                            if (Hit.transform.GetComponentInParent<CashStack>())
                            {
                                CollectTimestamp = Time.time + 0.05f;

                                // GameManager.instance.cashman.SpawnCash(Camera.main.WorldToScreenPoint(Target.transform.position), 1,1);
                                //Destroy(Target);
                                if (Target.GetComponent<SlotOwner>().TargetSlot)
                                {
                                    Target.GetComponent<SlotOwner>().TargetSlot.TheOwner = null;
                                    Target.GetComponent<SlotOwner>().TargetSlot = null;
                                }
                                Target.GetComponent<TheCash>().Collected();

                            }

                            else
                            {
                                if (X == 0 && V == 0 || !StopToCollect)
                                {
                                    if (Target.GetComponent<SlotOwner>().TargetSlot)
                                    {
                                        Target.GetComponent<SlotOwner>().TargetSlot.TheOwner = null;
                                        Target.GetComponent<SlotOwner>().TargetSlot = null;
                                    }
                                    TheStack.AddStack(Target);
                                    CollectTimestamp = Time.time + 0.1f;

                                }




                            }
                            if (slotman)
                            {
                                slotman._Refresh();
                            }
                        }
                    }
                    if (Hit.transform.GetComponentInParent<ItemSpawner>())
                    {
                        Hit.transform.GetComponentInParent<ItemSpawner>().PauseSpawn();
                    }
                }
                else if (Hit.transform.GetComponentInParent<StackRemover>())
                {
                    Timer TheTimer = Hit.transform.GetComponentInParent<StackRemover>().TheTimer;
                    if (TheTimer)
                    {
                        //if (type == TheType.Player)
                        //{
                        //    AddInteractionTime();
                        //}
                        TheTimer.AddTime();

                        if (!TheTimer.IsFilled())
                        {
                            return;
                        }
                        else
                        {
                            // TheTimer.ResetTimer();
                        }

                    }
                    if (ReleaseTimestamp < Time.time)
                    {
                        TheStack.RemoveStack(Hit.transform.GetComponentInParent<StackRemover>());
                        ReleaseTimestamp = Time.time + 0.1f;


                    }
                    if (Hit.transform.GetComponentInParent<Counter>())
                    {
                        if (type == TheType.Player)
                        {
                            AddInteractionTime();
                        }
                        Hit.transform.GetComponentInParent<Counter>().ActivateSelling(true);
                        Hit.transform.GetComponentInParent<Counter>().queline.StopWaitTime();
                    }
                    if (Hit.transform.GetComponentInParent<CarCounter>())
                    {
                        if (type == TheType.Player)
                        {
                            AddInteractionTime();
                        }
                        Hit.transform.GetComponentInParent<CarCounter>().ActivateSelling(true);
                        Hit.transform.GetComponentInParent<CarCounter>().queline.StopWaitTime();
                    }

                }


            }
        }
        void ReleaseCheck()
        {
            if (ReleaseTimestamp < Time.time)
            {

            }
        }
        public void Refreshtats()
        {
            HandStack.MaxSize = _GameManager.instance.upgradeManager.UpgradeHud.Playerupgrade.Capacity + _GameManager.instance.upgradeManager.PlayerCarryCapacity;
            TheHandCart.TheStack.MaxSize = _GameManager.instance.upgradeManager.UpgradeHud.Playerupgrade.HandCartCapacity + _GameManager.instance.upgradeManager.PlayerHandCartCapacity;
        }

    }
}