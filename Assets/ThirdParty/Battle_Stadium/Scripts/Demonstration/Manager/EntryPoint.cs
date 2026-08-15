using System;
using System.Collections.Generic;
using UnityEngine;

namespace ithappy.Battle_Stadium
{
    public class EntryPoint : MonoBehaviour
    {
        private class CharacterPositionData
        {
            public CharacterBase Character;
            public Vector3 Position;
            public Quaternion Rotation;
        }
        
        [SerializeField] private bool _shouldInitOnStart = true;
        [SerializeField] private CharacterBase[] _characters;
        [SerializeField] private GameObject _targetObject;

        private List<CharacterPositionData> _startPositions = new List<CharacterPositionData>();
        
        public CharacterBase[] Characters
        {
            get => _characters;
            set => _characters = value;
        }

        public GameObject TargetObject
        {
            get => _targetObject;
            set => _targetObject = value;
        }

        private void Start()
        {
            if (!_shouldInitOnStart)
            {
                return;
            }
            
            Initialize();
            Show();
        }

        public void Initialize()
        {
            SaveStartPositions();
            DistributePrioritiesEvenly();
        }

        public void Show()
        {
            for (int i = 0; i < _characters.Length; i++)
            {
                _characters[i].Initialize();
            }
        }

        public void Hide()
        {
            for (int i = 0; i < _characters.Length; i++)
            {
                _characters[i].Dispose();
            }
        }

        public void Restart()
        {
            foreach (var item in _startPositions)
            {
                item.Character.transform.localPosition = item.Position;
                item.Character.transform.rotation = item.Rotation;
            }
        }

        public void Dispose()
        {
            _startPositions.Clear();
        }

        private void SaveStartPositions()
        {
            for (int i = 0; i < _characters.Length; i++)
            {
                _startPositions.Add(new CharacterPositionData
                {
                    Character = _characters[i],
                    Position = _characters[i].transform.localPosition,
                    Rotation = _characters[i].transform.rotation
                });
            }
        }

        private void DistributePrioritiesEvenly()
        {
            if (_characters.Length == 0) return;

            float priorityStep = 99f / (_characters.Length - 1);

            for (int i = 0; i < _characters.Length; i++)
            {
                if (_characters[i] == null) continue;
                _characters[i].SetPriority(Mathf.RoundToInt(i * priorityStep));
            }
        }
        
        public void FindCharactersInChildren()
        {
            if (_targetObject == null)
            {
                Debug.LogWarning("Target Object is not assigned!");
                return;
            }

            CharacterBase[] foundCharacters = _targetObject.GetComponentsInChildren<CharacterBase>();
            
            if (foundCharacters.Length == 0)
            {
                Debug.LogWarning($"No CharacterBase components found in children of {_targetObject.name}");
                return;
            }

            _characters = foundCharacters;
            Debug.Log($"Found {foundCharacters.Length} CharacterBase components in children of {_targetObject.name}");
            
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }
        
        public void FindCharactersInChildren(GameObject target)
        {
            if (target == null)
            {
                Debug.LogWarning("Target GameObject is null!");
                return;
            }

            CharacterBase[] foundCharacters = target.GetComponentsInChildren<CharacterBase>();
            
            if (foundCharacters.Length == 0)
            {
                Debug.LogWarning($"No CharacterBase components found in children of {target.name}");
                return;
            }

            _characters = foundCharacters;
            _targetObject = target;
            Debug.Log($"Found {foundCharacters.Length} CharacterBase components in children of {target.name}");

#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }
    }
}