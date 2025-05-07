using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Werewolf.Managers;
using static Werewolf.Managers.GameHistoryManager;

namespace Werewolf.UI
{
	public class GameHistoryMenu : MonoBehaviour
	{
		[Header("Game History Buttons")]
		[SerializeField]
		private GameHistoryButton _gameHistoryButtonPrefab;

		[SerializeField]
		private Transform _gameHistoryButtonsContainer;

		[Header("Game History")]
		[SerializeField]
		private GameObject _noHistories;

		[SerializeField]
		private GameObject _selectToSee;

		[SerializeField]
		private GameHistory _gameHistory;

		[SerializeField]
		private Button _deleteButton;

		[SerializeField]
		private Button _deleteAllButton;

		public event Action ReturnClicked;

		private readonly List<GameHistoryButton> _gameHistoryButtons = new();
		private GameHistoryButton _selectedGameHistoryButton;

		private GameHistoryManager _gameHistoryManager;

		private void Awake()
		{
			_gameHistoryManager = GameHistoryManager.Instance;
		}

		private void OnEnable()
		{
			RefreshHistory();
		}

		private void OnClickedGameHistoryButton(GameHistoryButton gameHistoryButton)
		{
			if (_selectedGameHistoryButton == gameHistoryButton)
			{
				return;
			}
			else if (_selectedGameHistoryButton)
			{
				_selectedGameHistoryButton.SetSelected(false);
			}

			_selectedGameHistoryButton = gameHistoryButton;
			
			if (!_gameHistoryManager.LoadGameHistorySaveFromFile(_selectedGameHistoryButton.FilePath, out GameHistorySave gameHistorySave))
			{
				Debug.LogError($"Couldn't load the GameHistory at {_selectedGameHistoryButton.FilePath}");
				return;
			}

			_noHistories.SetActive(false);
			_selectToSee.SetActive(false);
			_deleteButton.interactable = true;

			_gameHistory.DisplayGameHistory(gameHistorySave);
		}

		public void OnClickedDeleteHistoryButton()
		{
			if (!_selectedGameHistoryButton)
			{
				return;
			}

			if (!_gameHistoryManager.DeleteGameHistory(_selectedGameHistoryButton.FilePath))
			{
				return;
			}

			RefreshHistory();
		}

		public void OnClickedDeleteAllHistoryButton()
		{
			_gameHistoryManager.DeleteAllGameHistory();
			RefreshHistory();
		}

		private void RefreshHistory()
		{
			GameHistoryButton gameHistoryButton;

			while (_gameHistoryButtons.Count > 0)
			{
				gameHistoryButton = _gameHistoryButtons[0];
				Destroy(gameHistoryButton.gameObject);
				_gameHistoryButtons.RemoveAt(0);
			}

			_gameHistory.ClearGameHistoryEntries();

			_selectedGameHistoryButton = null;

			string[] filePaths = _gameHistoryManager.GetSavedGameHistoryFilePaths();
			bool isOdd = true;

			foreach (string filePath in filePaths)
			{
				gameHistoryButton = Instantiate(_gameHistoryButtonPrefab, _gameHistoryButtonsContainer);
				gameHistoryButton.Initialize(filePath, isOdd);
				gameHistoryButton.Clicked += OnClickedGameHistoryButton;
				_gameHistoryButtons.Add(gameHistoryButton);

				isOdd = !isOdd;
			}

			bool hasHistory = filePaths.Length > 0;

			_noHistories.SetActive(!hasHistory);
			_selectToSee.SetActive(hasHistory);
			_deleteButton.interactable = false;
			_deleteAllButton.interactable = hasHistory;
		}

		private void OnDisable()
		{
			GameHistoryButton gameHistoryButton;

			while (_gameHistoryButtons.Count > 0)
			{
				gameHistoryButton = _gameHistoryButtons[0];
				gameHistoryButton.Clicked -= OnClickedGameHistoryButton;
				Destroy(gameHistoryButton.gameObject);
				_gameHistoryButtons.RemoveAt(0);
			}
		}

		public void OnReturn()
		{
			ReturnClicked?.Invoke();
		}
	}
}