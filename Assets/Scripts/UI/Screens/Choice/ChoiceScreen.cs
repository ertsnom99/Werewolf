using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using UnityEngine.Localization.SmartFormat.PersistentVariables;
using UnityEngine.UI;
using Werewolf.Data;

namespace Werewolf.UI
{
	public class ChoiceScreen : FadingScreen
	{
		[Header("UI")]
		[SerializeField]
		private LocalizeStringEvent _countdownText;

		[SerializeField]
		private LocalizeStringEvent _text;

		[SerializeField]
		private HorizontalLayoutGroup _choicesContainer;

		[SerializeField]
		private Choice _roleChoicePrefab;

		[SerializeField]
		private Button _confirmButton;

		[SerializeField]
		private LocalizeStringEvent _confirmButtonText;

		private GameConfig _config;

		private bool _mustChooseOne;

		private Choice[] _choices;

		private Choice _selectedChoice;
		private LocalizedString _choosedText;
		private LocalizedString _didNotChoosedText;

		private IntVariable _countdownVariable;

		private IEnumerator _countdownCoroutine;

		public event Action<int> ConfirmedChoice;

		protected override void Awake()
		{
			base.Awake();

			_countdownVariable = (IntVariable)_countdownText.StringReference["Time"];

			if (_countdownVariable == null)
			{
				Debug.LogError($"_countdownText must have a local int variable named Time");
			}
		}

		public void SetConfig(GameConfig config)
		{
			_config = config;
		}

		public void Initialize(Choice.ChoiceData[] choices, LocalizedString chooseText, LocalizedString choosedText, LocalizedString didNotChoosedText, bool mustChooseOne, float countdownDuration)
		{
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

			_text.StringReference = chooseText;
			_choosedText = choosedText;
			_didNotChoosedText = didNotChoosedText;

			_mustChooseOne = mustChooseOne;

			UpdateConfirmButton(hasChoiceSelected: false);

			_countdownCoroutine = Countdown(countdownDuration);
			StartCoroutine(_countdownCoroutine);
		}

		private void OnChoiceSelected(Choice choice)
		{
			bool isCurrentChoice = _selectedChoice == choice;

			if (!isCurrentChoice && _selectedChoice)
			{
				_selectedChoice.SetSelected(false);
			}

			_selectedChoice = isCurrentChoice ? null : choice;

			UpdateConfirmButton(hasChoiceSelected: !isCurrentChoice);
		}

		private void UpdateConfirmButton(bool hasChoiceSelected)
		{
			_confirmButton.interactable = hasChoiceSelected || !_mustChooseOne ? true : false;
			_confirmButtonText.StringReference = !hasChoiceSelected ? (_mustChooseOne ? _config.MustChooseText : _config.SkipChoiceText) : _config.ConfirmChoiceText;
		}

		public void OnConfirmChoice()
		{
			if (_mustChooseOne && _selectedChoice == null)
			{
				return;
			}

			_text.StringReference = _selectedChoice ? _choosedText : _didNotChoosedText;

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

			_confirmButton.interactable = false;

			ConfirmedChoice?.Invoke(_selectedChoice != null ? Array.IndexOf(_choices, _selectedChoice) : -1);
		}

		private IEnumerator Countdown(float countdownDuration)
		{
			float timeLeft = countdownDuration;

			while (timeLeft > 0)
			{
				yield return 0;

				timeLeft = Mathf.Max(timeLeft - Time.deltaTime, .0f);
				_countdownVariable.Value = Mathf.CeilToInt(timeLeft);
			}
		}

		public void DisableConfirmButton()
		{
			_confirmButton.interactable = false;
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