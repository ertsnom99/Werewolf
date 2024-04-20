using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Werewolf.Data;

namespace Werewolf.UI
{
	public class TitleScreen : FadingScreen
	{
		[Header("UI")]
		[SerializeField]
		private TMP_Text _countdownText;

		[SerializeField]
		private Image _image;

		[SerializeField]
		private TMP_Text _text;

		[SerializeField]
		private Button _confirmButton;

		[SerializeField]
		private TMP_Text _confirmButtonText;

		private GameConfig _config;

		private IEnumerator _countdownCoroutine;

		public event Action Confirm;

		public void SetConfig(GameConfig config)
		{
			_config = config;
		}

		public void Initialize(Sprite image, string title, float countdownDuration = -1, bool showConfirmButton = false, string confirmButtonText = "")
		{
			_image.gameObject.SetActive(image);
			_image.sprite = image;

			_text.text = title;

			_confirmButton.gameObject.SetActive(showConfirmButton);
			_confirmButtonText.text = confirmButtonText;
			_confirmButton.interactable = true;

			if (_countdownCoroutine != null)
			{
				StopCoroutine(_countdownCoroutine);
			}

			bool displayCountdown = countdownDuration > -1;
			_countdownText.gameObject.SetActive(displayCountdown);

			if (!displayCountdown)
			{
				return;
			}

			_countdownCoroutine = Countdown(countdownDuration);
			StartCoroutine(_countdownCoroutine);
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

		public void SetConfirmButtonInteractable(bool isInteractable)
		{
			_confirmButton.interactable = isInteractable;
		}

		public void OnConfirm()
		{
			_confirmButton.interactable = false;
			Confirm?.Invoke();
		}

		protected override void OnFadeStarts(float targetOpacity)
		{
			if (targetOpacity >= 1)
			{
				return;
			}

			_countdownText.text = string.Format(_config.CountdownText, 0);

			if (_countdownCoroutine == null)
			{
				return;
			}

			StopCoroutine(_countdownCoroutine);
			_countdownCoroutine = null;
		}
	}
}