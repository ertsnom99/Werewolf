using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Werewolf.UI
{
    public abstract class FadingScreen : MonoBehaviour
    {
        private float _opacity;
        private IEnumerator _coroutine;

        public event Action OnFadeOver = delegate { };

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
            float startingOpacity = _opacity;
            float transitionProgress = .0f;

            while (transitionProgress < transitionDuration)
            {
                transitionProgress += Time.deltaTime;
                float progressRatio = Mathf.Clamp01(transitionProgress / transitionDuration);
                _opacity = Mathf.Lerp(startingOpacity, targetOpacity, progressRatio);

                UpdateFade(_opacity);

                yield return 0;
            }

            _coroutine = null;

            OnFadeOver();
        }

        protected abstract void UpdateFade(float opacity);

        protected void SetTextOpacity(TMP_Text text, float opacity)
        {
            text.color = new Color { r = text.color.r, g = text.color.g, b = text.color.b, a = opacity };
        }

        protected void SetImageOpacity(Image image, float opacity)
        {
            image.color = new Color { r = image.color.r, g = image.color.g, b = image.color.b, a = opacity };
        }

        public void SetFade(float opacity)
        {
            _opacity = Mathf.Clamp01(opacity);
            UpdateFade(_opacity);
        }
    }
}