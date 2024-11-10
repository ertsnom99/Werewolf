using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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
		private TMP_Text _messageText;

		private int _minNicknameCharacterCount;

		public event Action JoinSessionClicked;
		public event Action ReturnClicked;

		public void Initialize(string message, int minNicknameCharacterCount)
		{
			_minNicknameCharacterCount = minNicknameCharacterCount;
			_nicknameInputField.interactable = true;
			_sessionNameInputField.interactable = true;
			UpdateButtonState();
			_messageText.text = message;
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
			_messageText.text = "Joining session...";

			JoinSessionClicked?.Invoke();
		}

		public void OnReturn()
		{
			ReturnClicked?.Invoke();
		}
	}
}