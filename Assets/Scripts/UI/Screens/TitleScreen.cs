using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using UnityEngine.Localization.SmartFormat.PersistentVariables;
using UnityEngine.UI;
using Werewolf.Data;

namespace Werewolf.UI
{
	public class TitleScreen : FadingScreen
	{
		[Header("UI")]
		[SerializeField]
		private LocalizeStringEvent _countdownText;

		[SerializeField]
		private Image _image;

		[SerializeField]
		private LocalizeStringEvent _localizedText;

		[SerializeField]
		private Button _confirmButton;

		[SerializeField]
		private LocalizeStringEvent _confirmButtonText;

		private IntVariable _countdownVariable;

		private IEnumerator _countdownCoroutine;

		public event Action ConfirmClicked;

		protected override void Awake()
		{
			base.Awake();

			_countdownVariable = (IntVariable)_countdownText.StringReference["Time"];

			if (_countdownVariable == null)
			{
				Debug.LogError($"_countdownText must have a local int variable named Time");
			}
		}

		public void Initialize(Sprite image, LocalizedString title, Dictionary<string, IVariable> variables = null, bool showConfirmButton = false, LocalizedString confirmButtonText = null, float countdownDuration = -1)
		{
			_image.gameObject.SetActive(image);
			_image.sprite = image;

			if (variables != null)
			{
				LocalizedString localizedString = new(title.TableReference, title.TableEntryReference.KeyId);

				foreach (KeyValuePair<string, IVariable> variable in variables)
				{
					localizedString.Add(variable.Key, variable.Value);
				}

				_localizedText.StringReference = localizedString;
			}
			else
			{
				_localizedText.StringReference = title;
			}

			_confirmButton.gameObject.SetActive(showConfirmButton);
			_confirmButtonText.StringReference = confirmButtonText;
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
				_countdownVariable.Value = Mathf.CeilToInt(timeLeft);
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