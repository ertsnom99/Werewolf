using UnityEngine;
using System.Collections;
using UnityEngine.Localization.Components;
using UnityEngine.Localization;
using UnityEngine.Localization.SmartFormat.PersistentVariables;

namespace Werewolf.UI
{
	public class VoteScreen : FadingScreen
	{
		[Header("UI")]
		[SerializeField]
		private LocalizeStringEvent _titleText;

		[SerializeField]
		private LocalizeStringEvent _countdownText;

		[SerializeField]
		private RectTransform _confirmVoteDelay;

		[SerializeField]
		private GameObject _warningText;

		private IntVariable _countdownVariable;

		private float _confirmVoteDelayDuration;

		private IEnumerator _countdownCoroutine;
		private IEnumerator _confirmVoteDelayCountdownCoroutine;

		protected override void Awake()
		{
			base.Awake();

			_countdownVariable = (IntVariable)_countdownText.StringReference["Time"];

			if (_countdownVariable == null)
			{
				Debug.LogError($"{nameof(_countdownText)} must have a local int variable named Time");
			}
		}

		public void SetConfirmVoteDelayDuration(float confirmVoteDelayDuration)
		{
			_confirmVoteDelayDuration = confirmVoteDelayDuration;
		}

		public void Initialize(LocalizedString title, bool displayWarning, float countdownDuration)
		{
			_titleText.StringReference = title;

			SetConfirmVoteDelayActive(false);
			_warningText.SetActive(displayWarning);

			StopCountdown();

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
				_countdownVariable.Value = Mathf.CeilToInt(timeLeft);
			}
		}

		public void SetConfirmVoteDelayActive(bool isActive)
		{
			_confirmVoteDelay.gameObject.SetActive(isActive);

			if (_confirmVoteDelayCountdownCoroutine != null)
			{
				StopCoroutine(_confirmVoteDelayCountdownCoroutine);
				_confirmVoteDelayCountdownCoroutine = null;
			}

			if (!isActive)
			{
				return;
			}

			SetConfirmVoteDelayScale(1.0f);

			_confirmVoteDelayCountdownCoroutine = ConfirmVoteDelayCountdown(_confirmVoteDelayDuration);
			StartCoroutine(_confirmVoteDelayCountdownCoroutine);
		}

		private IEnumerator ConfirmVoteDelayCountdown(float maxDuration)
		{
			float timeLeft = maxDuration;

			while (timeLeft > 0)
			{
				yield return 0;

				timeLeft = Mathf.Max(timeLeft - Time.deltaTime, .0f);

				SetConfirmVoteDelayScale(timeLeft / maxDuration);
			}
		}

		private void SetConfirmVoteDelayScale(float scale)
		{
			_confirmVoteDelay.localScale = new(scale, _confirmVoteDelay.localScale.y, _confirmVoteDelay.localScale.z);
		}

		protected override void OnFadeStarts(float targetOpacity)
		{
			if (targetOpacity <= 0)
			{
				StopCountdown();
			}
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
	}
}