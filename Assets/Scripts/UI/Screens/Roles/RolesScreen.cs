using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization.Components;
using Utilities.GameplayData;
using Werewolf.Data;

namespace Werewolf.UI
{
	public class RolesScreen : FadingScreen
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
		private LocalizeStringEvent _roleNameText;

		[SerializeField]
		private LocalizeStringEvent _roleDescriptionText;

		private bool _areRolesDisplayed;
		private readonly Dictionary<RoleData, RoleButton> _roleButtonByRoleData = new();
		private RoleButton _selectedRoleButton;

		private void Start()
		{
			List<RoleData> roles = GameplayDataManager.Instance.TryGetGameplayData<RoleData>();

			foreach (RoleData role in roles)
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

			_roleNameText.StringReference = roleButton.RoleData.NameSingular;
			_roleDescriptionText.StringReference = roleButton.RoleData.Description;
		}

		public void SelectRole(RoleData role, bool openMenu)
		{
			if (!role || !_roleButtonByRoleData.TryGetValue(role, out RoleButton roleButton))
			{
				return;
			}

			if (openMenu && !_areRolesDisplayed)
			{
				ToggleRoles();
			}

			roleButton.Select();
		}

		public void ToggleRoles()
		{
			if (!_screen || !_backgroundButton)
			{
				return;
			}

			_areRolesDisplayed = !_areRolesDisplayed;
			_screen.anchoredPosition = new Vector3(_areRolesDisplayed ? -_screen.rect.width : 0, 0, 0);
			_backgroundButton.SetActive(_areRolesDisplayed);
		}

		protected override void OnFadeStarts(float targetOpacity) { }
	}
}