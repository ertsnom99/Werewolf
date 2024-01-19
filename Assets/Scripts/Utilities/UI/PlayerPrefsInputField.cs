using TMPro;
using UnityEngine;

[RequireComponent(typeof(TMP_InputField))]
public class PlayerPrefsInputField : MonoBehaviour
{
    [SerializeField]
    private string _prefKey;

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
            if (PlayerPrefs.HasKey(_prefKey))
            {
                defaultName = PlayerPrefs.GetString(_prefKey);
                _inputField.text = defaultName;
            }
        }
    }

    public void SetPrefString(string value)
    {
        PlayerPrefs.SetString(_prefKey, value);
    }
}