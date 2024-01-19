using UnityEngine;
using Werewolf.Data;

namespace Werewolf
{
    public class Card : MonoBehaviour
    {
        [SerializeField]
        private float _thickness = 0.026f;

        [SerializeField]
        private SpriteRenderer _roleImage;

        [field: SerializeField]
        [field: ReadOnly]
        public bool IsFaceUp { get; private set; }

        [field: SerializeField]
        [field: ReadOnly]
        public RoleData Role { get; private set; }

        [field: SerializeField]
        [field: ReadOnly]
        public RoleBehavior Behavior { get; private set; }

#if UNITY_EDITOR
        private void Awake()
        {
            if (!_roleImage)
            {
                Debug.LogError($"_roleImage of the player {gameObject.name} is null");
            }

            IsFaceUp = false;
        }
#endif
        public void SetRole(RoleData role)
        {
            Role = role;
            _roleImage.sprite = role.Image;
        }

        public void SetBehavior(RoleBehavior behavior)
        {
            Behavior = behavior;
        }

        public void Flip()
        {
            Vector3 direction;

            if (IsFaceUp)
            {
                direction = Vector3.up;
            }
            else
            {
                direction = Vector3.down;
            }

            transform.position += direction * _thickness;
            transform.rotation *= Quaternion.AngleAxis(180, Vector3.forward);

            IsFaceUp = !IsFaceUp;
        }
    }
}