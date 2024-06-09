using System;
using System.Collections;
using UnityEngine;

namespace Werewolf.UI
{
	[RequireComponent(typeof(CanvasGroup))]
	public abstract class FadingScreen : MonoBehaviour
	{
		private IEnumerator _coroutine;

		private CanvasGroup _canvasGroup;

		public event Action FadeFinished;

		private void Awake()
		{
			_canvasGroup = GetComponent<CanvasGroup>();
		}

		public void FadeIn(float transitionDuration)
		{
			StartFade(1.0f, transitionDuration);
		}

		public void FadeOut(float transitionDuration)
		{
			StartFade(.0f, transitionDuration);
		}

		private void StartFade(float targetOpacity, float transitionDuration)
		{
			if (_coroutine != null)
			{
				StopCoroutine(_coroutine);
			}

			if (_canvasGroup.alpha == targetOpacity)
			{
				_coroutine = null;
				FadeFinished?.Invoke();
				return;
			}

			OnFadeStarts(targetOpacity);

			_coroutine = TransitionUI(targetOpacity, transitionDuration);
			StartCoroutine(_coroutine);
		}

		protected abstract void OnFadeStarts(float targetOpacity);

		private IEnumerator TransitionUI(float targetOpacity, float transitionDuration)
		{
			if (targetOpacity > .0f)
			{
				_canvasGroup.blocksRaycasts = true;
			}

			float startingOpacity = _canvasGroup.alpha;
			float transitionProgress = .0f;

			while (transitionProgress < transitionDuration)
			{
				yield return 0;

				transitionProgress += Time.deltaTime;
				float progressRatio = Mathf.Clamp01(transitionProgress / transitionDuration);

				_canvasGroup.alpha = Mathf.Lerp(startingOpacity, targetOpacity, progressRatio);
			}

			if (targetOpacity <= .0f)
			{
				_canvasGroup.blocksRaycasts = false;
			}

			_coroutine = null;

			FadeFinished?.Invoke();
		}

		public void SetFade(float opacity)
		{
			_canvasGroup.alpha = Mathf.Clamp01(opacity);
			_canvasGroup.blocksRaycasts = opacity > .0f;
		}
	}
}