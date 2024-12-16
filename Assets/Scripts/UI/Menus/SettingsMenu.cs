using System;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace Werewolf.UI
{
	public class SettingsMenu : MonoBehaviour
	{
		[Header("UI")]
		[SerializeField]
		private TMP_Dropdown _gameSpeedDropdown;

		[Header("Language")]
		[SerializeField]
		private Locale[] _locales;

		public event Action ReturnClicked;

		private void Awake()
		{
			if (_locales.Length < _gameSpeedDropdown.options.Count)
			{
				Debug.LogError($"Not enough locales set in the settings menu. Need at least {_gameSpeedDropdown.options.Count}");
			}
		}

		public void OnChangeLanguage(int language)
		{
			LocalizationSettings.SelectedLocale = _locales[language];
		}

		public void OnReturn()
		{
			ReturnClicked?.Invoke();
		}
	}
}
