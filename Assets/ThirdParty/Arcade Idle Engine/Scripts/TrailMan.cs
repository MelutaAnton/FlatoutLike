using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace AIE
{
    public class TrailMan : MonoBehaviour
    {
        public Player TheP;
        public ParticleSystem TrailParticle;
        public Vector2 Prev;
        public Vector2 current;
        float TheTimestamp;
        float CheckTimestamp;
        float TurnDir;
        float Speed = 1;
        public bool UseLean;
        void Update()
        {
            current = new Vector2(TheP.X, TheP.V);
            if (Vector2.Distance(Prev, current) > 0.2f)
            {
                if (CheckTimestamp < Time.time)
                {
                    if (Vector2.SignedAngle(current, Prev) > 0)
                    {
                        if (TurnDir == 1)
                        {
                            Speed = 5;
                        }
                        TurnDir = -1;
                    }
                    else
                    {
                        if (TurnDir == -1)
                        {
                            Speed = 5;
                        }
                        TurnDir = 1;
                    }
                    Prev = current;

                    CheckTimestamp = Time.time + 0.1f;
                }

                //TheP.TurnDir =Vector2.SqrMagnitude(current) - Vector2.SqrMagnitude(Prev);
                TheTimestamp = Time.time + 0.5f;

            }
            else
            {
                if (CheckTimestamp < Time.time)
                {
                    TurnDir = 0;
                }
            }

            if (TheTimestamp > Time.time)
            {
                if (!TrailParticle.isPlaying)
                {
                    TrailParticle.loop = true;
                    TrailParticle.Play();

                }
            }
            else
            {
                TrailParticle.loop = false;
            }
            if (!UseLean)
            {
                TheP.TurnDir = 0;
            }
            else
            {
                if (Vector2.SqrMagnitude(current) > 0)
                {
                    Speed = Mathf.MoveTowards(Speed, 1, 1 * Time.deltaTime);
                }
                else
                {
                    Speed = Mathf.MoveTowards(Speed, 1, 10 * Time.deltaTime);
                }
                TheP.TurnDir = Mathf.MoveTowards(TheP.TurnDir, TurnDir, Speed * Time.deltaTime);
            }

        }
    }
}
