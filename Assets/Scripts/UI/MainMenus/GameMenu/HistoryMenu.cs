using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Werewolf.Managers.GameHistoryManager;
using Werewolf.Managers;

namespace Werewolf.UI
{
	public class HistoryMenu : MonoBehaviour
	{
		[Header("Game History")]
		[SerializeField]
		private GameHistory _gameHistory;

		[SerializeField]
		private GameObject _couldNotDisplay;

		[SerializeField]
		private TMP_InputField _saveNameInputField;

		[SerializeField]
		private Button _saveButton;

		private string _gameHistoryData;

		private GameHistoryManager _gameHistoryManager;

		public void Initialize(string gameHistory)
		{
			_gameHistoryManager = GameHistoryManager.Instance;
			_gameHistoryData = gameHistory;

			if (!string.IsNullOrEmpty(gameHistory) && _gameHistoryManager.LoadGameHistorySaveFromJson(gameHistory, out GameHistorySave gameHistorySave))
			{
				_saveNameInputField.text = $"{DateTime.Now:yyyy'_'MM'_'dd'_'HH'_'mm}";
				_gameHistory.DisplayGameHistory(gameHistorySave);
			}
			else
			{
				_saveNameInputField.interactable = false;
				_saveButton.interactable = false;
				_couldNotDisplay.SetActive(true);
			}
		}

		public void OnSaveGameHistory()
		{
			if (!string.IsNullOrEmpty(_gameHistoryData) && !string.IsNullOrEmpty(_saveNameInputField.text))
			{
				_gameHistoryManager.SaveGameHistoryToFile(_saveNameInputField.text, _gameHistoryData);
				_saveNameInputField.interactable = false;
				_saveButton.interactable = false;
			}
		}
	}
}
