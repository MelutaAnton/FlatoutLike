using System;
using System.Collections.Generic;
using UnityEngine;

namespace ithappy.Battle_Stadium
{
    public class SplinePatrolCharacterState : CharacterStateBase
    {
        private const int ARC_LENGTH_SAMPLES = 50;
        
        private MovementBase _movement;
        private List<SplinePatrolWayPoint> _wayPoints;
        private int _currentSegmentIndex = 0;
        private bool _isWaiting = false;
        private float _currentTimer;
        
        private float _splineProgress;
        private float _segmentDuration;
        private bool _isMoving = false;
        
        private float[] _segmentLengths;
        private float _totalSegmentLength;
        
        private SplineSegment _currentSegment;
        
        public List<SplinePatrolWayPoint> WayPoints => _wayPoints;

        public SplinePatrolCharacterState(CharacterBase context, MovementBase movement,
            List<SplinePatrolWayPoint> wayPoints) :
            base(context)
        {
            _movement = movement;
            _wayPoints = wayPoints;
        }

        public override void Enter()
        {
            base.Enter();

            if (_wayPoints == null || _wayPoints.Count == 0)
                return;

            FindNearestSegment();
            StartMovementToCurrentSegment();
        }

        public override void Update()
        {
            for (int i = 0; i < _statesToTransition.Count; i++)
            {
                if (_statesToTransition[i].ShouldEnter())
                {
                    CharacterBase.TransitionToState(_statesToTransition[i]);
                    break;
                }
            }

            if (_isMoving)
            {
                UpdateMovement();
                UpdateRotation();
            }

            if (_isWaiting)
            {
                _currentTimer -= Time.deltaTime;
                if (_currentTimer <= 0)
                {
                    _isWaiting = false;
                    MoveToNextSegment();
                }
            }
        }

        public override void Exit()
        {
            _isMoving = false;
            _isWaiting = false;
        }

        public override bool ShouldEnter()
        {
            return true;
        }

        private void FindNearestSegment()
        {
            if (_wayPoints == null || _wayPoints.Count < 2)
                return;
            
            float shortestDistance = float.MaxValue;
            int nearestPointIndex = 0;

            for (int i = 0; i < _wayPoints.Count; i++)
            {
                if (_wayPoints[i] == null) continue;
            
                float distance = Vector3.Distance(_movement.MoveParent.position, _wayPoints[i].Position);
                if (distance < shortestDistance)
                {
                    shortestDistance = distance;
                    nearestPointIndex = i;
                }
            }
            
            float closestSegmentDistance = float.MaxValue;
            int closestSegmentIndex = 0;

            for (int i = 0; i < _wayPoints.Count; i++)
            {
                SplinePatrolWayPoint startPoint = _wayPoints[i];
                SplinePatrolWayPoint endPoint = GetEndPointForSegment(i);
            
                if (startPoint == null || endPoint == null) continue;
                
                SplineSegment tempSegment = new SplineSegment(
                    startPoint.Position,
                    startPoint.OutControlPoint,
                    endPoint.InControlPoint,
                    endPoint.Position
                );
                
                float segmentDistance = FindDistanceToSegment(tempSegment, _movement.MoveParent.position);
            
                if (segmentDistance < closestSegmentDistance)
                {
                    closestSegmentDistance = segmentDistance;
                    closestSegmentIndex = i;
                }
            }

            _currentSegmentIndex = closestSegmentIndex;
        }
        
        private float FindDistanceToSegment(SplineSegment segment, Vector3 point)
        {
            float minDistance = float.MaxValue;
            int sampleCount = 10;
        
            Vector3 previousPoint = segment.GetPoint(0);
            for (int i = 1; i <= sampleCount; i++)
            {
                float t = i / (float)sampleCount;
                Vector3 currentPoint = segment.GetPoint(t);
                
                float distance = DistanceToLineSegment(previousPoint, currentPoint, point);
                if (distance < minDistance)
                {
                    minDistance = distance;
                }
            
                previousPoint = currentPoint;
            }
        
            return minDistance;
        }
        
        private float DistanceToLineSegment(Vector3 a, Vector3 b, Vector3 point)
        {
            Vector3 ab = b - a;
            Vector3 ap = point - a;
        
            float dot = Vector3.Dot(ap, ab);
            float abLengthSqr = ab.sqrMagnitude;
            
            if (dot <= 0) return ap.magnitude;
            if (dot >= abLengthSqr) return (point - b).magnitude;
            
            Vector3 projection = a + (dot / abLengthSqr) * ab;
            return (point - projection).magnitude;
        }

        private void StartMovementToCurrentSegment()
        {
            if (_wayPoints == null || _wayPoints.Count < 2)
                return;

            SplinePatrolWayPoint startPoint = _wayPoints[_currentSegmentIndex];
            SplinePatrolWayPoint endPoint = GetEndPointForSegment(_currentSegmentIndex);
        
            if (startPoint == null || endPoint == null)
                return;

            _currentSegment = new SplineSegment(
                startPoint.Position,
                startPoint.OutControlPoint,
                endPoint.InControlPoint,
                endPoint.Position
            );
            
            PrecomputeArcLengthParameterization();
            
            _segmentDuration = Mathf.Max(0.1f, _totalSegmentLength / _movement.MoveSpeed);
        
            _splineProgress = 0f;
            _isMoving = true;
        }
        
        private void PrecomputeArcLengthParameterization()
        {
            _segmentLengths = new float[ARC_LENGTH_SAMPLES + 1];
            _segmentLengths[0] = 0f;
        
            Vector3 previousPoint = _currentSegment.GetPoint(0);
        
            for (int i = 1; i <= ARC_LENGTH_SAMPLES; i++)
            {
                float t = i / (float)ARC_LENGTH_SAMPLES;
                Vector3 currentPoint = _currentSegment.GetPoint(t);
            
                float segmentLength = Vector3.Distance(previousPoint, currentPoint);
                _segmentLengths[i] = _segmentLengths[i - 1] + segmentLength;
                previousPoint = currentPoint;
            }
        
            _totalSegmentLength = _segmentLengths[ARC_LENGTH_SAMPLES];
        }

        private SplinePatrolWayPoint GetEndPointForSegment(int segmentIndex)
        {
            if (_wayPoints == null || _wayPoints.Count == 0)
                return null;

            int endPointIndex = segmentIndex + 1;
            
            if (endPointIndex >= _wayPoints.Count)
            {
                endPointIndex = 0;
            }

            return _wayPoints[endPointIndex];
        }

        private void UpdateMovement()
        {
            if (!_isMoving || _currentSegment == null) return;
            
            float distanceTraveled = _splineProgress * _totalSegmentLength;
            distanceTraveled += (_movement.MoveSpeed * Time.deltaTime);
        
            _splineProgress = distanceTraveled / _totalSegmentLength;
            _splineProgress = Mathf.Clamp01(_splineProgress);
            
            float uniformT = ConvertLengthToParameter(_splineProgress * _totalSegmentLength);
            
            Vector3 position = _currentSegment.GetPoint(uniformT);
            _movement.MoveParent.position = position;

            if (_splineProgress >= 1f)
            {
                _isMoving = false;
                OnReachedWayPoint();
            }
        }

        private void UpdateRotation()
        {
            if (!_isMoving || _currentSegment == null) return;

            SplinePatrolWayPoint endPoint = GetEndPointForSegment(_currentSegmentIndex);
            if (endPoint == null) return;
            
            Vector3 tangent = _currentSegment.GetTangent(_splineProgress);

            if (tangent != Vector3.zero && tangent.magnitude > 0.01f && endPoint.ShouldCharacterRotate)
            {
                Quaternion targetRotation = Quaternion.LookRotation(tangent.normalized);
                _movement.MoveParent.rotation = Quaternion.Slerp(
                    _movement.MoveParent.rotation,
                    targetRotation,
                    Time.deltaTime * _movement.RotateSpeed
                );
            }
        }
        
        private float ConvertLengthToParameter(float targetLength)
        {
            for (int i = 1; i <= ARC_LENGTH_SAMPLES; i++)
            {
                if (_segmentLengths[i] >= targetLength)
                {
                    float prevLength = _segmentLengths[i - 1];
                    float currentLength = _segmentLengths[i];
                    float localT = (targetLength - prevLength) / (currentLength - prevLength);
                
                    float prevParam = (i - 1) / (float)ARC_LENGTH_SAMPLES;
                    float currentParam = i / (float)ARC_LENGTH_SAMPLES;
                
                    return Mathf.Lerp(prevParam, currentParam, localT);
                }
            }
        
            return 1f;
        }

        private void OnReachedWayPoint()
        {
            SplinePatrolWayPoint reachedPoint = GetEndPointForSegment(_currentSegmentIndex);
            if (reachedPoint == null) return;

            _isWaiting = true;
            _currentTimer = reachedPoint.WaitTime;
            
            if (reachedPoint.ShouldRotate)
            {
                RotateToWayPointDirection(reachedPoint);
            }
        }

        private void RotateToWayPointDirection(SplinePatrolWayPoint wayPoint)
        {
            Vector3 lookDirection = wayPoint.GetLookDirection();
            if (lookDirection != Vector3.zero && lookDirection.magnitude > 0.01f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(lookDirection);
                _movement.MoveParent.rotation = targetRotation;
            }
        }

        private void MoveToNextSegment()
        {
            if (_wayPoints == null || _wayPoints.Count < 2)
                return;

            _currentSegmentIndex++;
            
            if (_currentSegmentIndex >= _wayPoints.Count)
            {
                _currentSegmentIndex = 0;
            }

            StartMovementToCurrentSegment();
        }
        
        private class SplineSegment
        {
            public Vector3 P0 { get; }
            public Vector3 P1 { get; }
            public Vector3 P2 { get; }
            public Vector3 P3 { get; }

            public SplineSegment(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
            {
                P0 = p0;
                P1 = p1;
                P2 = p2;
                P3 = p3;
            }

            public Vector3 GetPoint(float t)
            {
                float u = 1 - t;
                float uu = u * u;
                float uuu = uu * u;
                float tt = t * t;
                float ttt = tt * t;

                return uuu * P0 +
                       3 * uu * t * P1 +
                       3 * u * tt * P2 +
                       ttt * P3;
            }

            public Vector3 GetTangent(float t)
            {
                float u = 1 - t;
                float uu = u * u;
                float tt = t * t;

                return 3 * uu * (P1 - P0) +
                       6 * u * t * (P2 - P1) +
                       3 * tt * (P3 - P2);
            }
        }
    }
}