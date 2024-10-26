using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Werewolf.Data;

namespace Werewolf.UI
{
	public class ChoiceScreen : FadingScreen
	{
		[Header("UI")]
		[SerializeField]
		private TMP_Text _countdownText;

		[SerializeField]
		private TMP_Text _text;

		[SerializeField]
		private HorizontalLayoutGroup _choicesContainer;

		[SerializeField]
		private Choice _roleChoicePrefab;

		[SerializeField]
		private Button _SkipButton;

		[SerializeField]
		private TMP_Text _buttonText;

		private GameConfig _config;

		private bool _mustChooseOne;

		private Choice[] _choices;

		private Choice _selectedChoice;
		private string _choosedText;
		private string _didNotChoosedText;

		private IEnumerator _countdownCoroutine;

		public event Action<int> ConfirmedChoice;

		public void SetConfig(GameConfig config)
		{
			_config = config;
		}

		public void Initialize(float countdownDuration, string chooseText, string choosedText, string didNotChoosedText, Choice.ChoiceData[] choices, bool mustChooseOne)
		{
			_text.text = chooseText;
			_choosedText = choosedText;
			_didNotChoosedText = didNotChoosedText;

			_mustChooseOne = mustChooseOne;

			foreach (Transform choice in _choicesContainer.transform)
			{
				choice.GetComponent<Choice>().Selected -= OnChoiceSelected;
				Destroy(choice.gameObject);
			}

			_choices = new Choice[choices.Length];

			for (int i = 0; i < choices.Length; i++)
			{
				Choice choice = Instantiate(_roleChoicePrefab, _choicesContainer.transform);
				choice.SetChoice(choices[i]);
				choice.Selected += OnChoiceSelected;

				_choices[i] = choice;
			}

			_SkipButton.onClick.RemoveAllListeners();

			if (!mustChooseOne)
			{
				_SkipButton.onClick.AddListener(OnConfirmChoice);
			}

			_SkipButton.interactable = !mustChooseOne;
			_buttonText.text = mustChooseOne ? _config.MustChooseText : _config.SkipChoiceText;

			_countdownCoroutine = Countdown(countdownDuration);
			StartCoroutine(_countdownCoroutine);
		}

		private void OnChoiceSelected(Choice choice)
		{
			if (_selectedChoice)
			{
				if (_selectedChoice == choice)
				{
					_selectedChoice = null;
					_buttonText.text = _config.SkipChoiceText;
					return;
				}
				else
				{
					_selectedChoice.SetSelected(false);
				}
			}

			_selectedChoice = choice;
			_buttonText.text = _config.ConfirmChoiceText;
		}

		private void OnConfirmChoice()
		{
			if (_mustChooseOne && _selectedChoice == null)
			{
				return;
			}

			_text.text = _selectedChoice ? _choosedText : _didNotChoosedText;

			foreach (Choice choice in _choices)
			{
				choice.Selected -= OnChoiceSelected;

				if (choice == _selectedChoice)
				{
					choice.Disable();
					continue;
				}

				Destroy(choice.gameObject);
			}

			DisableConfirmButton();

			ConfirmedChoice?.Invoke(_selectedChoice != null ? Array.IndexOf(_choices, _selectedChoice) : -1);
		}

		private IEnumerator Countdown(float countdownDuration)
		{
			float timeLeft = countdownDuration;

			while (timeLeft > 0)
			{
				yield return 0;

				timeLeft = Mathf.Max(timeLeft - Time.deltaTime, .0f);

				_countdownText.text = string.Format(_config.CountdownText, Mathf.CeilToInt(timeLeft));
			}
		}

		public void DisableConfirmButton()
		{
			_SkipButton.onClick.RemoveAllListeners();
			_SkipButton.interactable = false;
		}

		public void StopCountdown()
		{
			if (_countdownCoroutine == null)
			{
				return;
			}

			StopCoroutine(_countdownCoroutine);
			_countdownCoroutine = null;
		}

		protected override void OnFadeStarts(float targetOpacity)
		{
			if (targetOpacity >= 1)
			{
				return;
			}

			StopCountdown();
		}
	}
}