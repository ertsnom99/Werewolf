using System;
using UnityEngine;

namespace Assets.Scripts.Data.Tags
{
    public class GameplayTag : ScriptableObject, IComparable<GameplayTag>, IEquatable<GameplayTag>
    {
        [SerializeField]
        private int _compactTagId;

        public int CompactTagId
        {
            get => _compactTagId;
            set => _compactTagId = value;
        }

        public int CompareTo(GameplayTag other) =>
            string.Compare(name, other.name, StringComparison.InvariantCultureIgnoreCase);

        public bool Equals(GameplayTag other) => other != null && _compactTagId == other._compactTagId;

        public bool IsInCategory(GameplayTag tag) => IsInCategory(tag, out _);

        public bool IsInCategory(GameplayTag tag, out int depth)
        {
            depth = 0;
            if (tag == null || tag._compactTagId <= 0)
                return false;

            int compactTagId = _compactTagId;

            while (compactTagId >= tag._compactTagId)
            {
                if (compactTagId == tag._compactTagId)
                    return true;

                compactTagId = RemoveLastLevelBits(compactTagId);
                ++depth;
            }

            depth = -1;
            return false;
        }
    
        private static int RemoveLastLevelBits(int compactTagId)
        {
            int count = 1;
            int upperValue = compactTagId >> GameplayTagManager.LEVEL_BITS;

            while (upperValue > GameplayTagManager.LEVEL_BITS_MASK)
            {
                upperValue >>= GameplayTagManager.LEVEL_BITS;
                ++count;
            }

            upperValue <<= GameplayTagManager.LEVEL_BITS * count;

            return upperValue == 0 ? 0 : compactTagId - upperValue;
        }
    }
}