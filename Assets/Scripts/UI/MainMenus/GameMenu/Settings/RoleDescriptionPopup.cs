using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using UnityEngine.UI;

namespace Werewolf.UI
{
	public class RoleDescriptionPopup : MonoBehaviour
	{
		[Header("Popup")]
		[SerializeField]
		private RectTransform _popup;

		[SerializeField]
		private LocalizeStringEvent _roleDescriptionText;

		public void Display(LocalizedString roleDescription, Vector3 popupTargetPosition)
		{
			_roleDescriptionText.StringReference = roleDescription;

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
