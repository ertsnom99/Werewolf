using System;
using UnityEngine;
using UnityEngine.UI;

namespace Werewolf.UI
{
    public class Choice : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField]
        private Image _image;

        [SerializeField]
        private GameObject _highlight;

        [SerializeField]
        private Button _button;

		private bool _isSelected;

		public int Value { get; private set; }

        public event Action<Choice> OnSelected = delegate { };

        public struct ChoiceData
        {
            public Sprite Image;
            public int Value;
        }

        public void Start()
        {
            SetSelected(false);

            _button.onClick.AddListener(() =>
            {
                SetSelected(!_isSelected);
                OnSelected(this);
            });
        }

        public void SetChoice(ChoiceData choice)
        {
            _image.sprite = choice.Image;
            Value = choice.Value;
        }

        public void SetSelected(bool isSelected)
		{
			_isSelected = isSelected;
			_highlight.SetActive(isSelected);
        }

        public void Disable()
        {
            _button.onClick.RemoveAllListeners();
            _button.interactable = false;
        }
    }
}