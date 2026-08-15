using UnityEngine;

namespace ithappy.Battle_Stadium
{
    public class SplinePatrolWayPoint : MonoBehaviour
    {
        [Header("Basic Settings")] public float WaitTime = 0f;
        public bool ShouldCharacterRotate = true;
        public bool ShouldRotate = false;

        [Header("Spline Control Points")] public Vector3 InTangent = Vector3.left;
        public Vector3 OutTangent = Vector3.right;
        public float TangentLength = 2f;

        [Header("Look Direction")] public Vector3 LookDirection = Vector3.forward;
        public bool UseWorldSpace = false;

        [Header("Gizmo Settings")] public float WaypointSize = 0.5f;
        public float ControlPointSize = 0.3f;
        public Color WaypointColor = Color.blue;
        public Color InTangentColor = Color.red;
        public Color OutTangentColor = Color.green;
        public Color LookDirectionColor = Color.cyan;
        public Color SplineColor = Color.white;
        
        public Vector3 InControlPoint =>
            transform.position + transform.TransformDirection(InTangent.normalized) * TangentLength;

        public Vector3 OutControlPoint =>
            transform.position + transform.TransformDirection(OutTangent.normalized) * TangentLength;

        public Vector3 Position => transform.position;
        
        public Vector3 GetLookDirection()
        {
            if (UseWorldSpace)
            {
                return LookDirection.normalized;
            }
            else
            {
                return transform.TransformDirection(LookDirection).normalized;
            }
        }
        
        public void GetSplinePointsTo(SplinePatrolWayPoint nextPoint, out Vector3 p0, out Vector3 p1, out Vector3 p2,
            out Vector3 p3)
        {
            p0 = Position;
            p1 = OutControlPoint;
            p2 = nextPoint.InControlPoint;
            p3 = nextPoint.Position;
        }
        
        private void OnDrawGizmos()
        {
            DrawSplineToNextPoint();
            
            Gizmos.color = WaypointColor;
            Gizmos.DrawWireSphere(transform.position, WaypointSize);
            
            Gizmos.color = InTangentColor;
            Vector3 inControl = InControlPoint;
            Gizmos.DrawLine(transform.position, inControl);
            Gizmos.DrawSphere(inControl, ControlPointSize);
            
            Gizmos.color = OutTangentColor;
            Vector3 outControl = OutControlPoint;
            Gizmos.DrawLine(transform.position, outControl);
            Gizmos.DrawSphere(outControl, ControlPointSize);
            
            if (ShouldRotate)
            {
                Gizmos.color = LookDirectionColor;
                Vector3 direction = GetLookDirection();
                Gizmos.DrawRay(transform.position, direction * 2f);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(transform.position, WaypointSize * 1.2f);
            
            Gizmos.color = InTangentColor;
            Gizmos.DrawLine(transform.position, InControlPoint);
            Gizmos.color = OutTangentColor;
            Gizmos.DrawLine(transform.position, OutControlPoint);
            
            DrawSplineToNextPoint(true);
        }

        private void DrawSplineToNextPoint(bool selected = false)
        {
            SplinePatrolWayPoint nextPoint = FindNextWayPoint();
            if (nextPoint != null)
            {
                GetSplinePointsTo(nextPoint, out Vector3 p0, out Vector3 p1, out Vector3 p2, out Vector3 p3);
                
                DrawBezierCurve(p0, p1, p2, p3, selected ? 1.5f : 1f);
            }
        }

        private SplinePatrolWayPoint FindNextWayPoint()
        {
            Transform parent = transform.parent;
            if (parent == null)
                return null;

            int currentIndex = transform.GetSiblingIndex();
            int nextIndex = currentIndex + 1;
            
            if (nextIndex >= parent.childCount)
            {
                nextIndex = 0;
            }

            Transform nextTransform = parent.GetChild(nextIndex);
            return nextTransform.GetComponent<SplinePatrolWayPoint>();
        }

        private void DrawBezierCurve(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float widthMultiplier = 1f)
        {
            Gizmos.color = SplineColor;

            int segments = 20;
            Vector3 previousPoint = p0;

            for (int i = 1; i <= segments; i++)
            {
                float t = i / (float)segments;
                Vector3 currentPoint = CalculateBezierPoint(t, p0, p1, p2, p3);

                Gizmos.DrawLine(previousPoint, currentPoint);
                previousPoint = currentPoint;
            }
        }

        private Vector3 CalculateBezierPoint(float t, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
        {
            float u = 1 - t;
            float uu = u * u;
            float uuu = uu * u;
            float tt = t * t;
            float ttt = tt * t;

            return uuu * p0 +
                   3 * uu * t * p1 +
                   3 * u * tt * p2 +
                   ttt * p3;
        }
        
        public void SetInControlPoint(Vector3 worldPosition)
        {
            Vector3 localDirection = transform.InverseTransformDirection(worldPosition - transform.position);
            InTangent = localDirection.normalized;
            TangentLength = localDirection.magnitude;
        }

        public void SetOutControlPoint(Vector3 worldPosition)
        {
            Vector3 localDirection = transform.InverseTransformDirection(worldPosition - transform.position);
            OutTangent = localDirection.normalized;
            TangentLength = localDirection.magnitude;
        }
    }
}