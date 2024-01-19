using UnityEngine;

namespace Werewolf
{
    public class Player : MonoBehaviour
    {
        [field: SerializeField]
        public Card Card { get; private set; }

#if UNITY_EDITOR
        private void Awake()
        {
            if (!Card)
            {
                Debug.LogError($"_card of the player {gameObject.name} is null");
            }
        }
#endif
    }
}