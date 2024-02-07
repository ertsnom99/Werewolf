using UnityEngine;
using UnityEngine.UI;
using Werewolf.Data;

namespace Werewolf.UI
{
    public class Choice : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField]
        private Image _image;

        [SerializeField]
        private Button _button;

        private int _roleID;

        public void SetRole(RoleData role)
        {
            _roleID = role.GameplayTag.CompactTagId;
            _image.sprite = role.Image;
        }
    }
}