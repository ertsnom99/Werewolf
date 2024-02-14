using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Werewolf.UI
{
    public class ChoiceScreen : FadingScreen
    {
        [Header("UI")]
        [SerializeField]
        private TMP_Text _text;

        [SerializeField]
        private HorizontalLayoutGroup _choicesContainer;

        [SerializeField]
        private Choice _roleChoicePrefab;

        [SerializeField]
        private Button _confirmButton;

        private Choice[] _choices;

        private Choice _SelectedChoice;
        private string _choosedText;
        private string _didNotChoosedText;

        public event Action<int> OnConfirmChoice = delegate { };

        public void Config(string chooseText, string choosedText, string didNotChoosedText, Choice.ChoiceData[] choices, bool mustChooseOne)
        {
            _text.text = chooseText;
            _choosedText = choosedText;
            _didNotChoosedText = didNotChoosedText;

            foreach (Transform choice in _choicesContainer.transform)
            {
                choice.GetComponent<Choice>().OnSelected -= OnChoiceSelected;
                Destroy(choice.gameObject);
            }

            _choices = new Choice[choices.Length];

            for (int i = 0; i < choices.Length; i++)
            {
                Choice choice = Instantiate(_roleChoicePrefab, _choicesContainer.transform);
                choice.SetChoice(choices[i]);
                choice.OnSelected += OnChoiceSelected;

                _choices[i] = choice;
            }

            _confirmButton.onClick.AddListener(() =>
            {
                if (mustChooseOne && _SelectedChoice == null)
                {
                    return;
                }

                ConfirmChoice();
            });

            _confirmButton.interactable = true;
        }

        private void OnChoiceSelected(Choice choice)
        {
            if (_SelectedChoice)
            {
                if (_SelectedChoice == choice)
                {
                    _SelectedChoice = null;
                    return;
                }
                else
                {
                    _SelectedChoice.SetHighlighted(false);
                }
            }

            _SelectedChoice = choice;
        }

        public void ConfirmChoice()
        {
            _text.text = _SelectedChoice ? _choosedText : _didNotChoosedText;

            foreach (Choice choice in _choices)
            {
                choice.OnSelected -= OnChoiceSelected;

                if (choice == _SelectedChoice)
                {
                    choice.Disable();
                    continue;
                }

                Destroy(choice.gameObject);
            }

            _confirmButton.onClick.RemoveAllListeners();
            _confirmButton.interactable = false;

            OnConfirmChoice(_SelectedChoice != null ? Array.IndexOf(_choices, _SelectedChoice) : -1);
        }
    }
}