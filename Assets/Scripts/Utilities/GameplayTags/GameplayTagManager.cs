using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Assets.Scripts.Data.Tags
{
    [CreateAssetMenu(fileName = nameof(GameplayTagManager), menuName = "ScriptableObjects/Tags/" + nameof(GameplayTagManager))]
    public partial class GameplayTagManager : ScriptableObject
    {
        public const int LEVEL_BITS = 8;
        public const int LEVEL_BITS_MASK = 0xFF;
        public const string GAMEPLAY_TAGS_FIELD_NAME = nameof(_gameplayTags);

        [SerializeField]
        private List<GameplayTag> _gameplayTags = new List<GameplayTag>();
    
        public IReadOnlyList<GameplayTag> GameplayTags => _gameplayTags;

        public GameplayTag FindGameplayTag(int id)
        {
            return id == 0 ? null : _gameplayTags.FirstOrDefault(t => t.CompactTagId == id);
        }
    }
}