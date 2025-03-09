using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.UI;
using Werewolf.Data;

namespace Werewolf.UI
{
	public class RoleDescriptionPopup : MonoBehaviour
	{
		[Header("Popup")]
		[SerializeField]
		private RectTransform _popup;

		[SerializeField]
		private LocalizeStringEvent _roleNameText;

		[SerializeField]
		private LocalizeStringEvent _roleDescriptionText;

		public void Display(RoleData roleData, Vector3 popupTargetPosition)
		{
			_roleNameText.StringReference = roleData.MandatoryAmount > 1 ? roleData.NamePlural : roleData.NameSingular;
			_roleDescriptionText.StringReference = roleData.Description;

			gameObject.SetActive(true);
			LayoutRebuilder.ForceRebuildLayoutImmediate(_popup);

			Vector2 popupSizeDelta = _popup.sizeDelta;
			float x = popupTargetPosition.x < Screen.width / 2 ? popupTargetPosition.x : popupTargetPosition.x - popupSizeDelta.x;
			float y = popupTargetPosition.y >= Screen.height / 2 ? popupTargetPosition.y : popupTargetPosition.y + popupSizeDelta.y;
			_popup.position = new Vector3(x, y, 0);
		}

		public void Hide()
		{
			gameObject.SetActive(false);
		}
	}
}
