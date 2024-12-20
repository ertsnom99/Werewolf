// Original code from: https://discussions.unity.com/t/localizing-ui-dropdown-options/792432/17

using System.Collections.Generic;
using TMPro;
using UnityEngine.Localization.Settings;
using System.Collections;

namespace UnityEngine.Localization
{
	[RequireComponent(typeof(TMP_Dropdown))]
	public class LocalizeDropdown : MonoBehaviour
	{
		[SerializeField]
		private List<LocalizedString> _dropdownOptions;

		private Locale _currentLocale;

		private TMP_Dropdown _tmpDropdown;

		private void Awake()
		{
			if (_tmpDropdown == null)
			{
				_tmpDropdown = GetComponent<TMP_Dropdown>();
			}

			LocalizationSettings.SelectedLocaleChanged += ChangedLocale;

			UpdateDropdownOptions();
		}

		private void ChangedLocale(Locale newLocale)
		{
			if (_currentLocale == newLocale)
			{
				return;
			}

			_currentLocale = newLocale;
			UpdateDropdownOptions();
		}

		private void UpdateDropdownOptions()
		{
			_tmpDropdown.options.Clear();

			for (int i = 0; i < _dropdownOptions.Count; i++)
			{
				_tmpDropdown.options.Add(new TMP_Dropdown.OptionData(_dropdownOptions[i].GetLocalizedString()));
			}

			if (!gameObject.activeInHierarchy)
			{
				return;
			}

			StartCoroutine(RefreshShownValue());
		}

		private IEnumerator RefreshShownValue()
		{
			yield return new WaitForEndOfFrame();
			_tmpDropdown.RefreshShownValue();
		}

		public LocalizedString GetLocalizedValue()
		{
			if (_tmpDropdown == null)
			{
				_tmpDropdown = GetComponent<TMP_Dropdown>();
			}

			return _dropdownOptions[_tmpDropdown.value];
		}

		private void OnDestroy()
		{
			StopAllCoroutines();
		}
	}
}