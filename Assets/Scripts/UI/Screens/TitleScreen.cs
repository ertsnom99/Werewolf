using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
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
		private LocalizeStringEvent _localizedText;

		[SerializeField]
		private TMP_Text _text;

		[SerializeField]
		private Button _confirmButton;

		[SerializeField]
		private TMP_Text _confirmButtonText;

		private GameConfig _config;

		private IEnumerator _countdownCoroutine;

		public event Action ConfirmClicked;

		public void SetConfig(GameConfig config)
		{
			_config = config;
		}

		public void Initialize(Sprite image, LocalizedString title, float countdownDuration = -1, bool showConfirmButton = false, string confirmButtonText = "")
		{
			_image.gameObject.SetActive(image);
			_image.sprite = image;

			_localizedText.StringReference = title;

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

		public void StopCountdown()
		{
			if (_countdownCoroutine == null)
			{
				return;
			}

			StopCoroutine(_countdownCoroutine);
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
			ConfirmClicked?.Invoke();
		}

		protected override void OnFadeStarts(float targetOpacity)
		{
			if (targetOpacity >= 1 || _countdownCoroutine == null)
			{
				return;
			}

			StopCoroutine(_countdownCoroutine);
			_countdownCoroutine = null;
		}
	}
}