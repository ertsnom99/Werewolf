using TMPro;
using UnityEngine;
using System.Collections;
using System;
using Werewolf.Data;
using UnityEngine.UI;

namespace Werewolf.UI
{
	public class VoteScreen : FadingScreen
	{
		[Header("UI")]
		[SerializeField]
		private TMP_Text _countdownText;

		[SerializeField]
		private RectTransform _lockedInDelay;

		[SerializeField]
		private GameObject _warningText;

		[SerializeField]
		private Button _lockInButton;

		[SerializeField]
		private TMP_Text _lockInText;

		private GameConfig _config;

		private IEnumerator _countdownCoroutine;
		private IEnumerator _lockedInDelayCountdownCoroutine;

		private float _lockedInDelayDuration;
		public bool IsLockedIn { get; private set; }

		public delegate bool CanLockInDelegate();
		private CanLockInDelegate _canLockIn;

		public event Action<bool> VoteLockChanged;

		public void SetConfig(GameConfig config)
		{
			_config = config;
		}

		public void SetLockedInDelayDuration(float lockedInDelayDuration)
		{
			_lockedInDelayDuration = lockedInDelayDuration;
		}

		public void Initialize(bool displayWarning, float countdownDuration, bool displayButton, CanLockInDelegate canLockInDelegate = null)
		{
			IsLockedIn = false;
			_canLockIn = canLockInDelegate;

			SetLockedInDelayActive(false);
			_warningText.SetActive(displayWarning);
			UpdateButtonText();

			if (_countdownCoroutine != null)
			{
				StopCoroutine(_countdownCoroutine);
			}

			SetLockinButtonVisible(displayButton);

			_countdownCoroutine = Countdown(countdownDuration);
			StartCoroutine(_countdownCoroutine);
		}

		private IEnumerator Countdown(float countdownDuration)
		{
			float timeLeft = countdownDuration;

			while (timeLeft > 0)
			{
				timeLeft = Mathf.Max(timeLeft - Time.deltaTime, .0f);
				_countdownText.text = string.Format(_config.CountdownText, (int)timeLeft);
				yield return 0;
			}
		}

		public void ToggleChoiceLock()
		{
			if (_canLockIn != null && !_canLockIn())
			{
				return;
			}

			IsLockedIn = !IsLockedIn;
			UpdateButtonText();
			VoteLockChanged?.Invoke(IsLockedIn);
		}

		private void UpdateButtonText()
		{
			_lockInText.text = IsLockedIn ? _config.LockedOutButtonText : _config.LockedInButtonText;
		}

		public void SetLockedInDelayActive(bool isActive)
		{
			_lockedInDelay.gameObject.SetActive(isActive);

			if (_lockedInDelayCountdownCoroutine != null)
			{
				StopCoroutine(_lockedInDelayCountdownCoroutine);
				_lockedInDelayCountdownCoroutine = null;
			}

			if (!isActive)
			{
				return;
			}

			SetLockedInDelayScale(1.0f);

			_lockedInDelayCountdownCoroutine = LockedInDelayCountdown(_lockedInDelayDuration);
			StartCoroutine(_lockedInDelayCountdownCoroutine);
		}

		private IEnumerator LockedInDelayCountdown(float maxDuration)
		{
			float timeLeft = maxDuration;

			while (timeLeft > 0)
			{
				timeLeft = Mathf.Max(timeLeft - Time.deltaTime, .0f);
				SetLockedInDelayScale(timeLeft / maxDuration);
				yield return 0;
			}
		}

		private void SetLockedInDelayScale(float scale)
		{
			_lockedInDelay.localScale = new(scale, _lockedInDelay.localScale.y, _lockedInDelay.localScale.z);
		}

		public void SetLockinButtonVisible(bool isVisible)
		{
			_lockInButton.gameObject.SetActive(isVisible);
		}

		protected override void OnFadeStarts(float targetOpacity)
		{
			if (targetOpacity >= 1)
			{
				return;
			}

			if (_countdownCoroutine == null)
			{
				return;
			}

			StopCoroutine(_countdownCoroutine);
			_countdownCoroutine = null;
		}
	}
}