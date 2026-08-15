#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace ithappy.Battle_Stadium
{
    [CustomEditor(typeof(SplinePatrolWayPoint))]
    public class SplinePatrolWayPointEditor : UnityEditor.Editor
    {
        private const float HANDLE_SIZE = 0.1f;
        private bool _isEditingInTangent = false;
        private bool _isEditingOutTangent = false;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Visual Editing", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(_isEditingInTangent ? "Stop Edit In Tangent" : "Edit In Tangent"))
            {
                _isEditingInTangent = !_isEditingInTangent;
                _isEditingOutTangent = false;
            }

            if (GUILayout.Button(_isEditingOutTangent ? "Stop Edit Out Tangent" : "Edit Out Tangent"))
            {
                _isEditingOutTangent = !_isEditingOutTangent;
                _isEditingInTangent = false;
            }

            EditorGUILayout.EndHorizontal();

            if (_isEditingInTangent || _isEditingOutTangent)
            {
                EditorGUILayout.HelpBox("Drag the colored spheres in Scene view to adjust tangents", MessageType.Info);
            }

            SceneView.RepaintAll();
        }

        private void OnSceneGUI()
        {
            SplinePatrolWayPoint waypoint = (SplinePatrolWayPoint)target;
            
            EditorGUI.BeginChangeCheck();
            Vector3 newPosition = Handles.PositionHandle(waypoint.transform.position, waypoint.transform.rotation);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(waypoint.transform, "Move Waypoint");
                waypoint.transform.position = newPosition;
            }
            
            Handles.color = waypoint.InTangentColor;
            Vector3 inControl = waypoint.InControlPoint;

            if (_isEditingInTangent)
            {
                EditorGUI.BeginChangeCheck();
                Vector3 newInControl = Handles.PositionHandle(inControl, Quaternion.identity);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(waypoint, "Move In Tangent");
                    waypoint.SetInControlPoint(newInControl);
                }
            }

            Handles.SphereHandleCap(0, inControl, Quaternion.identity,
                waypoint.ControlPointSize * HandleUtility.GetHandleSize(inControl), EventType.Repaint);
            
            Handles.color = waypoint.OutTangentColor;
            Vector3 outControl = waypoint.OutControlPoint;

            if (_isEditingOutTangent)
            {
                EditorGUI.BeginChangeCheck();
                Vector3 newOutControl = Handles.PositionHandle(outControl, Quaternion.identity);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(waypoint, "Move Out Tangent");
                    waypoint.SetOutControlPoint(newOutControl);
                }
            }

            Handles.SphereHandleCap(0, outControl, Quaternion.identity,
                waypoint.ControlPointSize * HandleUtility.GetHandleSize(outControl), EventType.Repaint);
            
            Handles.color = waypoint.InTangentColor;
            Handles.DrawDottedLine(waypoint.transform.position, inControl, 2f);
            Handles.color = waypoint.OutTangentColor;
            Handles.DrawDottedLine(waypoint.transform.position, outControl, 2f);
        }
    }
#endif
}