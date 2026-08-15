using UnityEngine;

namespace ithappy.Battle_Stadium
{
    public class OscillateRotation : MonoBehaviour
    {
        public Vector3 rotationAxis = Vector3.up;
        public float rotationAngle = 45f;
        public float duration = 2f;
        public bool useRandomDelay = false; // Toggle random delay
        public float maxRandomDelay = 1f; // Maximum random delay
        public bool useLocalAxes = true; // Toggle local or global axes

        public enum CurveType
        {
            Linear,
            EaseIn,
            EaseOut,
            EaseInOut
        }

        public CurveType selectedCurveType = CurveType.EaseInOut;

        private Quaternion startRotation;
        private float timeElapsed = 0f;
        private bool isReversing = false;
        private float randomDelay = 0f;

        void Start()
        {
            startRotation = useLocalAxes ? transform.localRotation : transform.rotation;

            if (useRandomDelay)
            {
                randomDelay = Random.Range(0f, maxRandomDelay);
            }
        }

        void Update()
        {
            if (timeElapsed < randomDelay)
            {
                timeElapsed += Time.deltaTime;
                return;
            }

            float progress = (timeElapsed - randomDelay) / (duration / 2f);
            progress = Mathf.Clamp01(progress);

            progress = ApplySelectedCurve(progress);

            float currentAngle = rotationAngle * (isReversing ? (1 - progress) : progress);
            Quaternion currentRotation = startRotation * Quaternion.AngleAxis(currentAngle, rotationAxis);

            if (useLocalAxes)
            {
                transform.localRotation = currentRotation;
            }
            else
            {
                transform.rotation = currentRotation;
            }

            timeElapsed += Time.deltaTime;

            if (timeElapsed >= duration / 2f + randomDelay)
            {
                timeElapsed = randomDelay;
                isReversing = !isReversing;
            }
        }

        private float ApplySelectedCurve(float t)
        {
            switch (selectedCurveType)
            {
                case CurveType.Linear:
                    return t;
                case CurveType.EaseIn:
                    return EaseIn(t);
                case CurveType.EaseOut:
                    return EaseOut(t);
                case CurveType.EaseInOut:
                default:
                    return EaseInOut(t);
            }
        }

        private float EaseIn(float t)
        {
            return t * t * t;
        }

        private float EaseOut(float t)
        {
            return 1 - Mathf.Pow(1 - t, 3);
        }

        private float EaseInOut(float t)
        {
            return t < 0.5f ? 4 * t * t * t : 1 - Mathf.Pow(-2 * t + 2, 3) / 2;
        }
    }
}
