using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
namespace AIE
{
    public class CashUi : MonoBehaviour
    {
        public Transform Target;
        float WaitTimestamp;
        public float Speed = 20;
        Vector3 randpos;
        float randtimestamp;
        bool IsSpend;

        public Text ChangesText;
        public int ChangeAmount = 1;
        public void SetUp(Transform _Target, float Which, bool _IsSpend, int changeamount)
        {
            Target = _Target;
            WaitTimestamp = Time.time + 0.4f + Which / 100;
            IsSpend = _IsSpend;
            ChangeAmount = changeamount;
            if (IsSpend)
            {
                ChangesText.text = "-" + ChangeAmount.ToString();
                ChangesText.color = Color.red;
            }
            else
            {
                ChangesText.text = "+" + ChangeAmount.ToString();
                ChangesText.color = Color.green;
            }
        }
        void Update()
        {
            if (IsSpend)
            {
                if (WaitTimestamp > Time.time)
                {
                    if (randtimestamp < Time.time)
                    {
                        randpos = new Vector3(transform.position.x, transform.position.y + Random.Range(-50, 50), 0);
                        randtimestamp = Time.time + 0.5f;
                    }
                    transform.position = Vector3.Lerp(transform.position, randpos, Speed * Time.deltaTime);
                    return;
                }
                transform.position = Vector3.Lerp(transform.position, Camera.main.WorldToScreenPoint(Target.position), Speed * Time.deltaTime);
                float Dist = Vector3.Distance(transform.position, Camera.main.WorldToScreenPoint(Target.position));
                if (Dist < 1)
                {
                    Destroy(gameObject);
                }
                if (Dist < 10)
                {

                }
                transform.localScale = Vector3.Lerp(transform.localScale, Vector3.zero, 5 * Time.deltaTime);
            }
            else
            {
                if (WaitTimestamp > Time.time)
                {
                    if (randtimestamp < Time.time)
                    {
                        randpos = new Vector3(transform.position.x, transform.position.y + Random.Range(-30, 30), 0);
                        randtimestamp = Time.time + 0.5f;
                    }
                    transform.position = Vector3.Lerp(transform.position, randpos, Speed * Time.deltaTime);
                    return;
                }
                transform.position = Vector3.Lerp(transform.position, Target.position, Speed * Time.deltaTime);
                if (Vector3.Distance(transform.position, Target.position) < 1)
                {
                    _GameManager.instance.cashman.AdddTotalCash(ChangeAmount);

                    Destroy(gameObject);
                }
            }

        }
    }
}