using System;
using UnityEngine;

namespace Werewolf.UI
{
	public class RulesMenu : MonoBehaviour
	{
		[SerializeField]
		private GameObject _rules;

		[SerializeField]
		private GameObject _roles;

		public event Action ReturnClicked;

		public void OnShowRules()
		{
			_rules.SetActive(true);
			_roles.SetActive(false);
		}

		public void OnShowRoles()
		{
			_rules.SetActive(false);
			_roles.SetActive(true);
		}

		public void OnReturn()
		{
			ReturnClicked?.Invoke();
		}
	}
}