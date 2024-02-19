using TMPro;
using UnityEngine;
using System.Collections;
using System;
using Werewolf.Data;

namespace Werewolf.UI
{
	public class VoteScreen : FadingScreen
	{
		[Header("UI")]
		[SerializeField]
		private TMP_Text _timerText;

		[SerializeField]
		private RectTransform _lockedInDelay;

		[SerializeField]
		private TMP_Text _lockInText;

		private GameConfig _config;

		private IEnumerator _voteCountdownCoroutine;
		private IEnumerator _lockedInDelayCountdownCoroutine;

		private float _lockedInDelayDuration;
		private bool _isLockedIn;

		public event Action<bool> OnVoteLockChanged = delegate { };

		public void SetConfig(GameConfig config)
		{
			_config = config;
		}

		public void SetLockedInDelayDuration(float lockedInDelayDuration)
		{
			_lockedInDelayDuration = lockedInDelayDuration;
		}

		public void Initialize(float maxDuration)
		{
			_isLockedIn = false;

			SetLockedInDelayActive(false);
			UpdateButtonText();

			if (_voteCountdownCoroutine != null)
			{
				StopCoroutine(_voteCountdownCoroutine);
			}

			_voteCountdownCoroutine = VoteCountdown(maxDuration);
			StartCoroutine(_voteCountdownCoroutine);
		}

		private IEnumerator VoteCountdown(float maxDuration)
		{
			float timeLeft = maxDuration;

			while (timeLeft > 0)
			{
				timeLeft = Mathf.Max(timeLeft - Time.deltaTime, .0f);
				_timerText.text = string.Format(_config.VoteCountdownText, (int)timeLeft);
				yield return 0;
			}
		}

		public void ToggleChoiceLock()
		{
			_isLockedIn = !_isLockedIn;
			UpdateButtonText();
			OnVoteLockChanged(_isLockedIn);
		}

		private void UpdateButtonText()
		{
			_lockInText.text = _isLockedIn ? _config.LockedOutButtonText : _config.LockedInButtonText;
		}

		public void StopTimer()
		{
			if (_voteCountdownCoroutine == null)
			{
				return;
			}

			StopCoroutine(_voteCountdownCoroutine);
			_voteCountdownCoroutine = null;
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
	}
}