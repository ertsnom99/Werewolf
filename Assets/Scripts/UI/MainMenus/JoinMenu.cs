using System;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using UnityEngine.UI;
using Werewolf.Data;

namespace Werewolf.UI
{
	public class JoinMenu : MonoBehaviour
	{
		[Header("UI")]
		[SerializeField]
		private TMP_InputField _nicknameInputField;

		[SerializeField]
		private TMP_InputField _sessionNameInputField;

		[SerializeField]
		private Button _joinBtn;

		[SerializeField]
		private LocalizeStringEvent _messageText;

		[Header("Localization")]
		[SerializeField]
		private LocalizedString _joiningLocalizedString;

		public event Action JoinSessionClicked;
		public event Action ReturnClicked;

		private int _minNicknameCharacterCount;

		public void Initialize(LocalizedString message, int minNicknameCharacterCount)
		{
			_minNicknameCharacterCount = minNicknameCharacterCount;
			_nicknameInputField.characterLimit = GameConfig.MAX_NICKNAME_CHARACTER_COUNT;
			_nicknameInputField.interactable = true;
			_sessionNameInputField.interactable = true;

			UpdateButtonState();

			_messageText.gameObject.SetActive(message != null);
			_messageText.StringReference = message;
		}

		public void UpdateButtonState()
		{
			string nickname = GetNickname();
			bool enteredValidNickname = !string.IsNullOrEmpty(nickname) && nickname.Length >= _minNicknameCharacterCount;
			_joinBtn.interactable = enteredValidNickname;
		}

		public string GetNickname()
		{
			if (!_nicknameInputField)
			{
				return "";
			}

			return _nicknameInputField.text;
		}

		public void SetNickname(string nickname)
		{
			_nicknameInputField.text = nickname;
		}

		public string GetSessionName()
		{
			if (!_sessionNameInputField)
			{
				return "";
			}

			return _sessionNameInputField.text;
		}

		public void SetSessionName(string sessionName)
		{
			_sessionNameInputField.text = sessionName;
		}

		public void OnJoinSession()
		{
			_nicknameInputField.interactable = false;
			_sessionNameInputField.interactable = false;
			_joinBtn.interactable = false;
			_messageText.StringReference = _joiningLocalizedString;

			JoinSessionClicked?.Invoke();
		}

		public void OnReturn()
		{
			ReturnClicked?.Invoke();
		}
	}
}