using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Werewolf.UI
{
	public class EndGamePlayer : MonoBehaviour
	{
		[Header("UI")]
		[SerializeField]
		private Image _role;

		[SerializeField]
		private TMP_Text _nickname;

		[SerializeField]
		private TMP_Text _roleName;

		[SerializeField]
		private Sprite _lostRole;

		public void Initialize(Sprite roleImage, string nickname, string roleName)
		{
			_role.sprite = roleImage;
			_nickname.text = nickname;
			_roleName.text = roleName;
		}

		public void Initialize(string nickname)
		{
			_role.sprite = _lostRole;
			_nickname.text = nickname;
			_roleName.text = "Lost their role";
		}
	}
}