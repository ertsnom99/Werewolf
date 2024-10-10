using System;
using TMPro;
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

		[SerializeField]
		private TMP_Text _nameText;

		private bool _isSelected;

		public event Action<Choice> Selected;

		public struct ChoiceData
		{
			public Sprite Image;
			public string Name;
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
			_nameText.text = choice.Name;
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