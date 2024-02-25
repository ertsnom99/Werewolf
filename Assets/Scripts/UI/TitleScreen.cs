using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Werewolf.UI
{
	public class TitleScreen : FadingScreen
	{
		[Header("UI")]
		[SerializeField]
		private Image _image;

		[SerializeField]
		private TMP_Text _text;

		[SerializeField]
		private Button _confirmButton;

		[SerializeField]
		private TMP_Text _confirmButtonText;

		public event Action OnConfirm = delegate { };

		public void Initialize(Sprite image, string title, bool showConfirmButton = false, string confirmButtonText = "")
		{
			_image.sprite = image;
			_text.text = title;
			_confirmButton.gameObject.SetActive(showConfirmButton);
			_confirmButtonText.text = confirmButtonText;
		}

		public void Confirm()
		{
			OnConfirm();
		}
	}
}