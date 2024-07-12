using System;
using System.Collections.Generic;
using UnityEngine;
using static Werewolf.GameHistoryManager;

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

		private List<GameHistoryButton> _gameHistoryButtons = new();

		private GameHistoryManager _gameHistoryManager;

		public event Action ReturnClicked;

		private void Awake()
		{
			_gameHistoryManager = GameHistoryManager.Instance;
		}

		private void OnEnable()
		{
			string[] filePaths = _gameHistoryManager.GetSavedGameHistoryFilePaths();
			GameHistoryButton gameHistoryButton;

			foreach (string filePath in filePaths)
			{
				gameHistoryButton = Instantiate(_gameHistoryButtonPrefab, _gameHistoryButtonsContainer);
				gameHistoryButton.Initialize(filePath);
				gameHistoryButton.Clicked += OnClickedGameHistoryButton;
				_gameHistoryButtons.Add(gameHistoryButton);
			}

			_noHistories.SetActive(filePaths.Length <= 0);
			_selectToSee.SetActive(filePaths.Length > 0);
		}

		private void OnClickedGameHistoryButton(string filePath)
		{
			if (!_gameHistoryManager.LoadGameHistorySaveFromFile(filePath, out GameHistorySave gameHistorySave))
			{
				Debug.LogError($"Couldn't load the GameHistory at {filePath}");
				return;
			}

			_noHistories.SetActive(false);
			_selectToSee.SetActive(false);

			_gameHistory.DisplayGameHistory(gameHistorySave);
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