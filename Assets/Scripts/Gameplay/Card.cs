using Fusion;
using TMPro;
using UnityEngine;
using Werewolf.Data;

namespace Werewolf
{
    public class Card : MonoBehaviour, MouseDetectionListener
    {
        [SerializeField]
        private float _thickness = 0.026f;

        [SerializeField]
        private Transform _card;

        [SerializeField]
        private SpriteRenderer _roleImage;

        [SerializeField]
        private TMP_Text _nicknameText;

        [field: SerializeField]
        [field: ReadOnly]
        public bool IsFaceUp { get; private set; }

        [field: SerializeField]
        [field: ReadOnly]
        public PlayerRef Player { get; private set; }

        [field: SerializeField]
        [field: ReadOnly]
        public RoleData Role { get; private set; }

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

        public void SetPlayer(PlayerRef player)
        {
            Player = player;
        }

        public void SetRole(RoleData role)
        {
            Role = role;
            _roleImage.sprite = role.Image;
        }

        public void SetNickname(string nickname)
        {
            _nicknameText.text = nickname;
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

            _card.position += direction * _thickness;
            _card.rotation *= Quaternion.AngleAxis(180, Vector3.forward);

            IsFaceUp = !IsFaceUp;
        }

        #region MouseDetectionListener methods
        public void MouseEntered() { }

        public void MouseOver(Vector3 MousePosition) { }

        public void MouseExited() { }

        public void MousePressed(Vector3 MousePosition) { }

        public void MouseReleased(Vector3 MousePosition) { }
        #endregion
    }
}