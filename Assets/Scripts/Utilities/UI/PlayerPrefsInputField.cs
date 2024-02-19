using TMPro;
using UnityEngine;

[RequireComponent(typeof(TMP_InputField))]
public class PlayerPrefsInputField : MonoBehaviour
{
	[field: SerializeField]
	public string PrefKey { get; private set; }

	private TMP_InputField _inputField;

	private void Awake()
	{
		_inputField = GetComponent<TMP_InputField>();
	}

	private void Start()
	{
		string defaultName = string.Empty;

		if (_inputField != null)
		{
			if (PlayerPrefs.HasKey(PrefKey))
			{
				defaultName = PlayerPrefs.GetString(PrefKey);
				_inputField.text = defaultName;
			}
		}
	}

	public void SetPrefString(string value)
	{
		PlayerPrefs.SetString(PrefKey, value);
	}
}