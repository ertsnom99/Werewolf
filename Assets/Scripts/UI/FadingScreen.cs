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

        public event Action OnFadeOver = delegate { };

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

            _coroutine = TransitionUI(targetOpacity, transitionDuration);
            StartCoroutine(_coroutine);
        }

        private IEnumerator TransitionUI(float targetOpacity, float transitionDuration)
        {
            float startingOpacity = _canvasGroup.alpha;
            float transitionProgress = .0f;

            while (transitionProgress < transitionDuration)
            {
                transitionProgress += Time.deltaTime;
                float progressRatio = Mathf.Clamp01(transitionProgress / transitionDuration);
                _canvasGroup.alpha = Mathf.Lerp(startingOpacity, targetOpacity, progressRatio);

                yield return 0;
            }

            _coroutine = null;

            OnFadeOver();
        }

        public void SetFade(float opacity)
        {
            _canvasGroup.alpha = Mathf.Clamp01(opacity);
        }
    }
}