using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ithappy.Battle_Stadium
{

    [CustomEditor(typeof(SplinePatrolCharacter))]

    public class SplinePatrolCharacterEditor : UnityEditor.Editor
    {
        private SplinePatrolCharacter _splinePatrol;

        private bool _isAddingPoints = false;
        private PlacementMode _placementMode = PlacementMode.FixedHeight;
        private float _placementHeight = 1f;
        private LayerMask _meshLayerMask = -1;

        private bool _isEditingTangents = false;
        private TangentEditMode _tangentEditMode = TangentEditMode.OutTangent;

        private SplinePatrolWayPoint _currentEditingPoint;
        private Vector3 _dragStartPosition;
        private bool _isDragging = false;

        private enum PlacementMode
        {
            FixedHeight,
            OnMesh
        }

        private enum TangentEditMode
        {
            InTangent,
            OutTangent
        }

        private void OnEnable()
        {
            _splinePatrol = (SplinePatrolCharacter)target;
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Waypoint Editing", EditorStyles.boldLabel);
            
            _placementMode = (PlacementMode)EditorGUILayout.EnumPopup("Placement Mode", _placementMode);

            if (_placementMode == PlacementMode.FixedHeight)
            {
                _placementHeight = EditorGUILayout.FloatField("Placement Height", _placementHeight);
            }
            else
            {
                _meshLayerMask = LayerMaskField("Mesh Layer Mask", _meshLayerMask);
            }
            
            EditorGUILayout.BeginHorizontal();
            if (_isAddingPoints)
            {
                if (GUILayout.Button("Stop Adding Points"))
                {
                    StopAddPoint();
                }
            }
            else
            {
                if (GUILayout.Button("Start Adding Points"))
                {
                    StartAddPoint();
                }
            }

            if (GUILayout.Button("Clear All Points"))
            {
                ClearAllPoints();
            }

            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            if (_isEditingTangents)
            {
                if (GUILayout.Button("Stop Editing Tangents"))
                {
                    StopEditTangents();
                }
            }
            else
            {
                if (GUILayout.Button("Start Editing Tangents"))
                {
                    StartEditTangents();
                }
            }

            if (_isEditingTangents)
            {
                _tangentEditMode = (TangentEditMode)EditorGUILayout.EnumPopup(_tangentEditMode);
            }

            EditorGUILayout.EndHorizontal();
            
            if (_isAddingPoints)
            {
                EditorGUILayout.HelpBox("Adding Points Mode: Click in scene view to place waypoints", MessageType.Info);
            }
            else if (_isEditingTangents)
            {
                EditorGUILayout.HelpBox(
                    $"Editing Tangents Mode: Click on waypoints and drag to adjust {_tangentEditMode}",
                    MessageType.Info);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"Waypoints Count: {_splinePatrol.WayPoints?.Count ?? 0}");
        }

        private void OnSceneGUI()
        {
            if (_isAddingPoints)
            {
                HandlePointPlacement();
            }
            else if (_isEditingTangents)
            {
                HandleTangentEditing();
            }

            DrawSceneGUI();
        }

        private void HandlePointPlacement()
        {
            Event currentEvent = Event.current;
            int controlID = GUIUtility.GetControlID(FocusType.Passive);

            switch (currentEvent.type)
            {
                case EventType.MouseDown:
                    if (currentEvent.button == 0 && !currentEvent.alt)
                    {
                        Ray ray = HandleUtility.GUIPointToWorldRay(currentEvent.mousePosition);
                        Vector3 placementPosition = GetPlacementPosition(ray);

                        if (placementPosition != Vector3.zero)
                        {
                            CreateWayPoint(placementPosition);
                            currentEvent.Use();
                        }
                    }

                    break;

                case EventType.KeyDown:
                    if (currentEvent.keyCode == KeyCode.Escape)
                    {
                        StopAddPoint();
                        currentEvent.Use();
                    }

                    break;
            }

            HandleUtility.AddDefaultControl(controlID);
        }

        private void HandleTangentEditing()
        {
            Event currentEvent = Event.current;

            if (currentEvent.type == EventType.MouseDown && currentEvent.button == 0)
            {
                Ray ray = HandleUtility.GUIPointToWorldRay(currentEvent.mousePosition);
                if (Physics.Raycast(ray, out RaycastHit hit))
                {
                    SplinePatrolWayPoint waypoint = hit.collider.GetComponent<SplinePatrolWayPoint>();
                    if (waypoint != null && _splinePatrol.WayPoints.Contains(waypoint))
                    {
                        _currentEditingPoint = waypoint;
                        _dragStartPosition = hit.point;
                        _isDragging = true;
                        currentEvent.Use();
                    }
                }
            }
            else if (currentEvent.type == EventType.MouseDrag && _isDragging && _currentEditingPoint != null)
            {
                Ray ray = HandleUtility.GUIPointToWorldRay(currentEvent.mousePosition);
                Plane dragPlane = new Plane(Vector3.up, _currentEditingPoint.transform.position);

                if (dragPlane.Raycast(ray, out float distance))
                {
                    Vector3 worldPosition = ray.GetPoint(distance);

                    if (_tangentEditMode == TangentEditMode.InTangent)
                    {
                        _currentEditingPoint.SetInControlPoint(worldPosition);
                    }
                    else
                    {
                        _currentEditingPoint.SetOutControlPoint(worldPosition);
                    }

                    SceneView.RepaintAll();
                    currentEvent.Use();
                }
            }
            else if (currentEvent.type == EventType.MouseUp && _isDragging)
            {
                _isDragging = false;
                _currentEditingPoint = null;
                currentEvent.Use();
            }
            else if (currentEvent.type == EventType.KeyDown && currentEvent.keyCode == KeyCode.Escape)
            {
                StopEditTangents();
                currentEvent.Use();
            }
        }

        private void DrawSceneGUI()
        {
            Handles.BeginGUI();
            GUILayout.BeginArea(new Rect(10, 10, 300, 200));

            GUILayout.BeginVertical("Box");

            if (_isAddingPoints)
            {
                GUILayout.Label("Adding Points Mode", EditorStyles.boldLabel);
                GUILayout.Label($"Mode: {_placementMode}");
                GUILayout.Label("Click: Place waypoint");
                GUILayout.Label("ESC: Exit mode");
            }
            else if (_isEditingTangents)
            {
                GUILayout.Label("Editing Tangents Mode", EditorStyles.boldLabel);
                GUILayout.Label($"Editing: {_tangentEditMode}");
                GUILayout.Label("Click on waypoint: Select");
                GUILayout.Label("Drag: Adjust tangent");
                GUILayout.Label("ESC: Exit mode");
            }

            GUILayout.EndVertical();
            GUILayout.EndArea();
            Handles.EndGUI();
        }

        private Vector3 GetPlacementPosition(Ray ray)
        {
            switch (_placementMode)
            {
                case PlacementMode.FixedHeight:
                    Plane placementPlane = new Plane(Vector3.up, new Vector3(0, _placementHeight, 0));
                    if (placementPlane.Raycast(ray, out float distance))
                    {
                        return ray.GetPoint(distance);
                    }

                    break;

                case PlacementMode.OnMesh:
                    if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, _meshLayerMask))
                    {
                        return hit.point;
                    }

                    break;
            }

            return Vector3.zero;
        }

        private void CreateWayPoint(Vector3 position)
        {
            GameObject waypointObject = new GameObject($"WayPoint_{_splinePatrol.WayPoints.Count}");
            waypointObject.transform.position = position;
            waypointObject.transform.SetParent(_splinePatrol.transform);

            SplinePatrolWayPoint waypoint = waypointObject.AddComponent<SplinePatrolWayPoint>();
            _splinePatrol.AddWayPoint(waypoint);

            Undo.RegisterCreatedObjectUndo(waypointObject, "Create Waypoint");
            EditorUtility.SetDirty(_splinePatrol);
        }

        private void StartAddPoint()
        {
            _isAddingPoints = true;
            _isEditingTangents = false;
            Tools.current = Tool.None;
            SceneView.RepaintAll();
        }

        private void StopAddPoint()
        {
            _isAddingPoints = false;
            Tools.current = Tool.Move;
            SceneView.RepaintAll();
        }

        private void StartEditTangents()
        {
            _isEditingTangents = true;
            _isAddingPoints = false;
            Tools.current = Tool.None;
            SceneView.RepaintAll();
        }

        private void StopEditTangents()
        {
            _isEditingTangents = false;
            _currentEditingPoint = null;
            _isDragging = false;
            Tools.current = Tool.Move;
            SceneView.RepaintAll();
        }

        private void ClearAllPoints()
        {
            if (EditorUtility.DisplayDialog("Clear All Points",
                    "Are you sure you want to clear all waypoints?", "Yes", "No"))
            {
                foreach (var waypoint in _splinePatrol.WayPoints)
                {
                    if (waypoint != null)
                    {
                        Undo.DestroyObjectImmediate(waypoint.gameObject);
                    }
                }

                _splinePatrol.ClearWayPoints();
                EditorUtility.SetDirty(_splinePatrol);
            }
        }
        
        private LayerMask LayerMaskField(string label, LayerMask layerMask)
        {
            List<string> layers = new List<string>();
            List<int> layerNumbers = new List<int>();

            for (int i = 0; i < 32; i++)
            {
                string layerName = LayerMask.LayerToName(i);
                if (layerName != "")
                {
                    layers.Add(layerName);
                    layerNumbers.Add(i);
                }
            }

            int maskWithoutEmpty = 0;
            for (int i = 0; i < layerNumbers.Count; i++)
            {
                if (((1 << layerNumbers[i]) & layerMask.value) > 0)
                    maskWithoutEmpty |= (1 << i);
            }

            maskWithoutEmpty = EditorGUILayout.MaskField(label, maskWithoutEmpty, layers.ToArray());

            int mask = 0;
            for (int i = 0; i < layerNumbers.Count; i++)
            {
                if ((maskWithoutEmpty & (1 << i)) > 0)
                    mask |= (1 << layerNumbers[i]);
            }

            layerMask.value = mask;
            return layerMask;
        }
    }
}