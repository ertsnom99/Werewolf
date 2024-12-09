using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
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
		private LocalizeStringEvent _roleName;

		[SerializeField]
		private Sprite _lostRoleImage;

		[SerializeField]
		private LocalizedString _lostRoleText;

		public void Initialize(Sprite roleImage, string nickname, LocalizedString roleName)
		{
			_role.sprite = roleImage;
			_nickname.text = nickname;
			_roleName.StringReference = roleName;
		}

		public void Initialize(string nickname)
		{
			_role.sprite = _lostRoleImage;
			_nickname.text = nickname;
			_roleName.StringReference = _lostRoleText;
		}
	}
}