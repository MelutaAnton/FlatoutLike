using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace AIE
{
    public class Star : MonoBehaviour
    {
        public Transform Target;
        float WaitTimestamp;
        public float Speed = 20;
        Vector3 randpos;
        float randtimestamp;
        float multiplier = 1;
        public void SetUp(Transform _Target, float Which)
        {
            multiplier = 1;
            Target = _Target;
            WaitTimestamp = Time.time + 1 + Which / 100;

        }
        void Update()
        {
            if (WaitTimestamp > Time.time)
            {
                if (randtimestamp < Time.time)
                {
                    multiplier = -multiplier;
                    randpos = new Vector3(transform.position.x, transform.position.y + 10 * multiplier, 0);
                    randtimestamp = Time.time + 0.1f;
                }
                transform.position = Vector3.Lerp(transform.position, randpos, Speed * Time.deltaTime);
                return;
            }
            transform.position = Vector3.Lerp(transform.position, Target.position, Speed * Time.deltaTime);
            if (Vector3.Distance(transform.position, Target.position) < 1)
            {
                // GameManager.instance.leaderBoard.bounce();
                Destroy(gameObject);
            }

        }
    }
}
