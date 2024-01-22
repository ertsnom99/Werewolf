using System;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Data.Tags
{
    [Serializable]
    public class GameplayTagInfo
    {
        [SerializeField]
        private string _name;

        public string Name
        {
            get => _name;
            set => _name = value;
        }

        [SerializeField]
        private int _level;

        public int Level
        {
            get => _level;
            set => _level = value;
        }

        [SerializeField]
        private int _parentIndex;

        public int ParentIndex
        {
            get => _parentIndex;
            set => _parentIndex = value;
        }

        [SerializeField]
        private int _levelId;

        public int LevelId
        {
            get => _levelId;
            set => _levelId = value;
        }

        [SerializeField]
        private int _compactTagId;

        public int CompactTagId
        {
            get => _compactTagId;
            set => _compactTagId = value;
        }

        [SerializeField]
        private List<int> _childIndex;

        public List<int> ChildIndex
        {
            get => _childIndex;
            set => _childIndex = value;
        }
    }
}