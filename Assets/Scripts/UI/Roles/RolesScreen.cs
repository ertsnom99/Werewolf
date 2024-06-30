using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Werewolf.Data;

namespace Werewolf.UI
{
	public class RolesScreen : MonoBehaviour
	{
		[SerializeField]
		private RectTransform _screen;

		[SerializeField]
		private GameObject _backgroundButton;

		[SerializeField]
		private Transform _roleButtonContainer;

		[SerializeField]
		private RoleButton _roleButtonPrefab;

		[SerializeField]
		private TextMeshProUGUI _roleDescription;

		private bool _areRolesDisplayed;
		private Dictionary<RoleData, RoleButton> _roleButtonByRoleData = new();
		private RoleButton _selectedRoleButton;

		private void Start()
		{
			List<RoleData> roles = GameplayDatabaseManager.Instance.GetGameplayData<RoleData>();

			foreach(RoleData role in roles)
			{
				RoleButton roleButton = Instantiate(_roleButtonPrefab, _roleButtonContainer);

				roleButton.SetRoleData(role);
				roleButton.Selected += ShowRoleDescription;

				_roleButtonByRoleData.Add(role, roleButton);
			}
		}

		private void ShowRoleDescription(RoleButton roleButton)
		{
			if (_selectedRoleButton && _selectedRoleButton != roleButton)
			{
				_selectedRoleButton.Unselect();
			}

			_selectedRoleButton = roleButton;
			_roleDescription.text = roleButton.RoleData.Description.GetLocalizedString();
		}

		public void SelectRole(RoleData role)
		{
			if (!role || !_roleButtonByRoleData.ContainsKey(role))
			{
				return;
			}

			if (!_areRolesDisplayed)
			{
				ToggleRoles();
			}

			_roleButtonByRoleData[role].Select();
		}

		public void ToggleRoles()
		{
			if (!_screen || !_backgroundButton)
			{
				return;
			}

			_areRolesDisplayed = !_areRolesDisplayed;
			_screen.anchoredPosition = new Vector3(_areRolesDisplayed ? 0 : _screen.rect.width, 0, 0);
			_backgroundButton.SetActive(_areRolesDisplayed);
		}
	}
}