using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Werewolf
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

		public event Action JoinSession;
		public event Action Return;

		private readonly int MIN_NICKNAME_CHARACTER_COUNT = 3;

		public void Initialize(string message)
		{
			_nicknameInputField.interactable = true;
			_sessionNameInputField.interactable = true;
			UpdateButtonState();
			_messageText.text = message;
		}

		public void UpdateButtonState()
		{
			string nickname = GetNickname();
			bool enteredValidNickname = !string.IsNullOrEmpty(nickname) && nickname.Length >= MIN_NICKNAME_CHARACTER_COUNT;
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

			JoinSession?.Invoke();
		}

		public void OnReturn()
		{
			Return?.Invoke();
		}
	}
}