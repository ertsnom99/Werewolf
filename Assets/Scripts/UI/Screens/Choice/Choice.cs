using System;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
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

		[SerializeField]
		private LocalizeStringEvent _text;

		private bool _isSelected;

		public event Action<Choice> Selected;

		public struct ChoiceData
		{
			public Sprite Image;
			public LocalizedString Text;
		}

		public void Start()
		{
			SetSelected(false);

			_button.onClick.AddListener(() =>
			{
				SetSelected(!_isSelected);
				Selected?.Invoke(this);
			});
		}

		public void SetChoice(ChoiceData choice)
		{
			_image.sprite = choice.Image;
			_text.StringReference = choice.Text;
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